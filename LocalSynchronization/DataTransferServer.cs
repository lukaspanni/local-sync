using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace LocalSynchronization;

public class DataTransferServer : DataTransferBase
{
    private bool pairing = false;
    private bool certificateImported = false;
    private bool connected = false;
    private ReadOnlyMemory<byte> pairingSecret;
    private TcpListener? listener;
    private ITcpClient? tcpClient;
    protected ITransportLayer? transportLayer;
    private CancellationTokenSource tokenSource = new CancellationTokenSource();
    private X509Certificate2? pairingCertificate;   // temporary

    public byte[] SharedSecret
    {
        get
        {
            if (pairing) return pairingSecret.ToArray();
            return Array.Empty<byte>();
        }
    }

    public DataTransferServer(string ipString, int port) : this(ipString, port, new CertificateStore()) { }

    internal DataTransferServer(string ipString, int port, CertificateStore certStore) : base(ipString, port, certStore, "testserver")
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

        try
        {
            if ((secretMessage.StartByte & 0b100) == 0) throw new Exception("Did not receive a pairing request");

            var secret = secretMessage.Payload;
            if (!secret.Span.SequenceEqual(pairingSecret.Span)) throw new AuthenticationException("Pairing failed, secrets did not match");
        }
        catch (Exception ex)
        {
            
        }

        //TODO: Send Pairing status to client
        await SendAckForMessage(secretMessage);
        CompletePairing();

    }

    public async Task<bool> AcceptConnection()
    {
        if (!certificateImported) throw new InvalidOperationException("Server is not ready for connection, a certificate has to be imported first");
        var success = await WaitForConnection().ConfigureAwait(false);
        if (success) connected = true;
        return connected;
    }

    public async Task<DataResponse> ReceiveData()
    {
        if (!connected || transportLayer == null) throw new InvalidOperationException("Not Connected");
        try
        {
            var message = await transportLayer.ReceiveMessage().ConfigureAwait(false);
            Console.WriteLine("Received {0} bytes: {1}", message.Length, Encoding.UTF8.GetString(message.Payload.ToArray()));
            await SendAckForMessage(message).ConfigureAwait(false);
            return new DataResponse(ResponseState.Ok, message.Payload);
        }
        catch (Exception ex)
        {
            //return error
            return new DataResponse(ResponseState.Error, null);
        }
    }

    public async Task<bool> SendData(ReadOnlyMemory<byte> data)
    {
        if (!connected || transportLayer == null) throw new InvalidOperationException("Not Connected");
        try
        {
            var message = new TransportLayerMessage(data.Length, data);
            Console.WriteLine("Sending {0} bytes", message.Length);
            await transportLayer.SendMessage(message).ConfigureAwait(false); // send data
            var ack = await transportLayer.ReceiveMessage().ConfigureAwait(false); // receive ack
            //TODO: check ack
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
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
        CancelRunningOperations();
        //TODO
        transportLayer?.Dispose();
        tcpClient?.Dispose();
    }

    public void CancelRunningOperations()
    {
        tokenSource.Cancel();
    }

    protected override ITcpClient BuildClient(TcpClient tcpClient, bool useTls = true)
    {
        if (!useTls) return new TcpClientAdapter(tcpClient);
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
            listener = new TcpListener(IPAddress, Port);
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

    protected async Task SendAckForMessage(TransportLayerMessage message)
    {
        if (transportLayer == null) throw new InvalidOperationException("Not Connected");
        var ack = new TransportLayerMessage(0b11, message.Length, new ReadOnlyMemory<byte>());
        await transportLayer.SendMessage(ack).ConfigureAwait(false);
    }

}

