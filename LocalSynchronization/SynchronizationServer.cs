using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace LocalSynchronization;

public class SynchronizationServer : SynchronizationBase
{
    private bool pairing = true;
    private TcpListener? listener;
    private CancellationTokenSource tokenSource = new CancellationTokenSource();
    private X509Certificate2? pairingCertificate;   // temporary

    public SynchronizationServer(string ipString, int port) : this(ipString, port, new CertificateStore()) { }

    internal SynchronizationServer(string ipString, int port, CertificateStore certStore) : base(ipString, port, certStore, "testserver")
    {
    }

    public async Task StartListening()
    {
        Console.WriteLine("Listening on {0}:{1}", IPAddress.ToString(), Port);
        listener = new TcpListener(IPAddress, Port);
        listener.Start();
        var token = tokenSource.Token;
        while (!token.IsCancellationRequested)
        {
            var tcpClient = await listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
            var transportLayer = new TcpTransportLayer(BuildClient(tcpClient));
            HandleConnection(transportLayer, token);
        }
    }

    public override void ImportRemoteCertificate(string base64EncodedCertificate)
    {
        base.ImportRemoteCertificate(base64EncodedCertificate);
        pairing = false;
    }

    public void CompletePairing()
    {
        if (pairingCertificate == null) return;
        acceptedRemoteCertificate = pairingCertificate; //TODO: add to certstore for persistence
    }

    private async Task HandleConnection(ITransportLayer transportLayer, CancellationToken token)
    {
        Console.WriteLine("Client connected");

        using (transportLayer)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var message = await transportLayer.ReceiveMessage().ConfigureAwait(false);
                    Console.WriteLine("Received {0} bytes: {1}", message.Length, Encoding.UTF8.GetString(message.Payload.ToArray()));
                    var response = new TransportLayerMessage(0b11, message.Length, new ReadOnlyMemory<byte>());
                    await transportLayer.SendMessage(response).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    //ignore errors for now
                    break;
                }
            }
        }
        Console.WriteLine("Client disconnected");

    }

    public void Stop()
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
}


