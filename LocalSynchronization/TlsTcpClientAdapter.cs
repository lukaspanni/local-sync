using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
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
    private X509Certificate2? serverCertificate;
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

    public TlsTcpClientAdapter(TcpClient client, string host = "testserver") : base(client)
    {
        // client: gets initialized with not connected socket and a target host
        // tls init can only happen after connect
        targetHost = host;
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
            secureStream = new SslStream(Client.GetStream(), false);
            await secureStream.AuthenticateAsServerAsync(serverCertificate, clientCertificateRequired: false, checkCertificateRevocation: true);
        }
        if (mode == TlsMode.Client)
        {
            secureStream = new SslStream(Client.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);
            await secureStream.AuthenticateAsClientAsync(targetHost);
        }
    }

    private bool ValidateServerCertificate(
      object sender,
      X509Certificate certificate,
      X509Chain chain,
      SslPolicyErrors sslPolicyErrors)
    {
        if (sslPolicyErrors == SslPolicyErrors.None)
            return true;

        Console.WriteLine("Certificate error: {0}", sslPolicyErrors);
        if (chain.ChainStatus[0].Status == X509ChainStatusFlags.UntrustedRoot)
        {
            return true; // allow self signed certificate
        }

        return false;
    }

    public static X509Certificate2 GenerateSelfSignedCertificate()
    {
        var ecdsa = ECDsa.Create();
        var request = new CertificateRequest("cn=testserver", ecdsa, HashAlgorithmName.SHA256);
        var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddSeconds(-5), DateTimeOffset.UtcNow.AddYears(1));
        return new X509Certificate2(cert.Export(X509ContentType.Pkcs12));
    }
}