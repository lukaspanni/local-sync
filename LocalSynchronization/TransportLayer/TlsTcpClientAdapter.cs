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
    private RemoteCertificateValidationCallback? validateRemoteCertificate;

    protected override Stream? CommunicationStream => secureStream;

    public TlsTcpClientAdapter(TcpClient client, RemoteCertificateValidationCallback certificateValidationCallback, X509Certificate2? certificate) : base(client)
    {
        validateRemoteCertificate = certificateValidationCallback;
        targetHost = string.Empty;
        // server: gets initialized with a connected socket and a certificate
        if (Connected && certificate != null)
        {
            serverCertificate = certificate;
            InitializeSecureStream(TlsMode.Server).Wait();   // make sure authentication is completed, waiting here is not ideal
        }
    }

    public TlsTcpClientAdapter(TcpClient client, RemoteCertificateValidationCallback certificateValidationCallback, string host, X509Certificate? certificate = null) : base(client)
    {
        validateRemoteCertificate = certificateValidationCallback;
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
        secureStream = new SslStream(Client.GetStream(), false, validateRemoteCertificate, null);
        if (mode == TlsMode.Server && serverCertificate != null)
        {
            await secureStream.AuthenticateAsServerAsync(serverCertificate, clientCertificateRequired: true, checkCertificateRevocation: true);
        }
        if (mode == TlsMode.Client)
        {
            await secureStream.AuthenticateAsClientAsync(targetHost, clientCertificates, true);
        }
    }
}