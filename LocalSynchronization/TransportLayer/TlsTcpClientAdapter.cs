using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace LocalSynchronization;
internal enum TlsMode
{
    Server,
    Client
}
internal class TlsTcpClientAdapter : TcpClientAdapter
{
    private SslStream? secureStream;
    //TODO: maybe split client and server
    private X509Certificate2? serverCertificate;
    private X509Certificate2Collection clientCertificates = new X509Certificate2Collection();
    private string targetHost;

    protected override Stream? CommunicationStream => secureStream;

    public TlsTcpClientAdapter(TcpClient client, X509Certificate2? certificate) : base(client)
    {
        targetHost = string.Empty;
        // server: gets initialized with a connected socket and a certificate
        if (Connected && certificate != null)
        {
            serverCertificate = certificate;
            InitializeSecureStream(TlsMode.Server).Wait();   // make sure authentication is completed, waiting here is not ideal
        }
    }

    public TlsTcpClientAdapter(TcpClient client, string host = "testserver", X509Certificate? certificate = null) : base(client)
    {
        // client: gets initialized with not connected socket and a target host
        // tls init can only happen after connect
        targetHost = host;
        if(certificate != null) clientCertificates.Add(certificate);
    }

    public async override Task ConnectAsync(IPEndPoint remoteEP)
    {
        await base.ConnectAsync(remoteEP);
        await InitializeSecureStream(TlsMode.Client);
    }

    public async override ValueTask ConnectAsync(IPEndPoint remoteEP, CancellationToken token)
    {
        await base.ConnectAsync(remoteEP, token);
        await InitializeSecureStream(TlsMode.Client);
    }

    private async Task InitializeSecureStream(TlsMode mode)
    {
        if (mode == TlsMode.Server && serverCertificate != null)
        {
            secureStream = new SslStream(Client.GetStream(), false, new RemoteCertificateValidationCallback(Keystore.ValidateClientCertificate), null);
            await secureStream.AuthenticateAsServerAsync(serverCertificate, clientCertificateRequired: true, checkCertificateRevocation: true);
        }
        if (mode == TlsMode.Client)
        {
            secureStream = new SslStream(Client.GetStream(), false, new RemoteCertificateValidationCallback(Keystore.ValidateServerCertificate), null);
            await secureStream.AuthenticateAsClientAsync(targetHost, clientCertificates, true);
        }
    }
}