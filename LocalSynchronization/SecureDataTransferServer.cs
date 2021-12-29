using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace LocalSynchronization;

public class SecureDataTransferServer : SecureDataTransferBase
{
    private bool pairing = false;
    private bool certificateImported = false;
    private ReadOnlyMemory<byte> pairingSecret;
    private X509Certificate2? pairingCertificate;   // temporary

    public byte[] SharedSecret
    {
        get
        {
            if (pairing) return pairingSecret.ToArray();
            return Array.Empty<byte>();
        }
    }

    public SecureDataTransferServer(string ipString, int port) : this(ipString, port, new CertificateStore()) { }

    internal SecureDataTransferServer(string ipString, int port, CertificateStore certStore) : base(ipString, port, certStore, "testserver")
    {
    }

    public override void ImportRemoteCertificate(string base64EncodedCertificate)
    {
        base.ImportRemoteCertificate(base64EncodedCertificate);
        pairing = false;
        certificateImported = true;
    }

    public void PreparePair()
    {
        // genarate secret and start pairing
        pairingSecret = certificateStore.GenerateSecretBytes();
        pairing = true;
    }

    public async Task AcceptPairRequest()
    {
        //Pairing Process: establish connection -> receive and verify secret -> connected
        await WaitForConnection();
        if (transportLayer == null) throw new InvalidOperationException("Invalid object state, no client connected");

        var secretMessage = await transportLayer.ReceiveMessage().ConfigureAwait(false);

        bool hasError = false;
        try
        {
            if ((secretMessage.StartByte & 0b100) == 0) throw new Exception("Did not receive a pairing request");

            var secret = secretMessage.Payload;
            if (!secret.Span.SequenceEqual(pairingSecret.Span)) throw new AuthenticationException("Pairing failed, secrets did not match");
        }
        catch (Exception ex)
        {
            hasError = true;
        }

        TransportLayerMessage ack;
        if (!hasError)
        {
            ack = new TransportLayerMessage(MessageType.PairingResponse, secretMessage.Length);
        }
        else
        {
            ack = new TransportLayerMessage(MessageType.Error, secretMessage.Length);
        }
        await transportLayer.SendMessage(ack);
        CompletePairing();

    }

    public async Task<bool> AcceptConnection()
    {
        if (!certificateImported) throw new InvalidOperationException("Server is not ready for connection, a certificate has to be imported first");
        var success = await WaitForConnection().ConfigureAwait(false);
        if (success) connected = true;
        return connected;
    }

    public void CloseConnection()
    {
        connected = false;
        if (transportLayer == null || tcpClient == null) return;
        transportLayer.CancelRunningOperations();
        tcpClient.Close();
    }

    public override void Dispose()
    {
        tokenSource.Cancel();

        transportLayer?.Dispose();
        tcpClient?.Dispose();
    }


    protected override ITcpClient BuildClient(TcpClient tcpClient)
    {
        RemoteCertificateValidationCallback certificateValidation = new((object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors) =>
        {
            if (pairing)
            {
                // in pairing mode, the client certificate is not known
                // store the received certificate
                if (pairingCertificate == null)
                    pairingCertificate = new X509Certificate2(certificate);
                return true;
            }

            return acceptedRemoteCertificate != null && acceptedRemoteCertificate.Equals(certificate);
        }
        );
        return new TlsTcpClientAdapter(tcpClient, certificateValidation, localCertificate);
    }

    private async Task<bool> WaitForConnection()
    {
        try
        {
            Console.WriteLine("Listening on {0}:{1}", IPAddress.ToString(), Port);
            var listener = new TcpListener(IPAddress, Port);
            listener.Start();
            TcpClient rawClient = await listener.AcceptTcpClientAsync(tokenSource.Token).ConfigureAwait(false);
            tcpClient = BuildClient(rawClient);
            transportLayer = new TcpTransportLayer(tcpClient);
            Console.WriteLine("Client connected");
            return true;
        }
        catch (AggregateException ex)
        {
            var firstException = ex.InnerExceptions.First();
            if (firstException != null && firstException is AuthenticationException)
                Debug.WriteLine("Invalid certificate encountered");
            return false;
        }
    }

    private void CompletePairing()
    {
        if (pairingCertificate == null) return;
        acceptedRemoteCertificate = pairingCertificate; //TODO: add to certstore for persistence
        certificateImported = true;
        connected = true;
        Console.WriteLine("Client successfuly paired");
    }
}

