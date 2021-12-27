using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace LocalSynchronization;

//TODO: extract common base class
public class SynchronizationClient : IDisposable
{

    private const int startByte = 0x01;
    private CertificateStore clientCertificateStore;
    private ITcpClient? tcpClient;
    private ITransportLayer? transportLayer;
    private X509Certificate2 localCertificate;

    private X509Certificate2? acceptedRemoteCertificate;

    public IPAddress IPAddress { get; private set; }
    public int Port { get; private set; } = 4820;

    public byte[] PublicKeyBytes => localCertificate.Export(X509ContentType.Cert);

    private string RemoteHost => acceptedRemoteCertificate?.GetNameInfo(X509NameType.SimpleName, false) ?? "";


    public SynchronizationClient(string ipString, int port) : this(ipString, port, new CertificateStore()) { }

    internal SynchronizationClient(string ipString, int port, CertificateStore certStore)
    {
        IPAddress = IPAddress.Parse(ipString);
        Port = port;
        clientCertificateStore = certStore;

        localCertificate = clientCertificateStore.GetOrGenerateLocalCertificate("testclient");
    }

    public void ImportRemoteCertificate(string base64EncodedCertificate)
    {
        var imported = new X509Certificate2(Convert.FromBase64String(base64EncodedCertificate));
        if (imported == null || imported.HasPrivateKey) throw new ArgumentException("Provided certificate cannot be used for this operation");
        acceptedRemoteCertificate = imported;
    }

    public async Task Connect()
    {
        tcpClient = BuildClient(RemoteHost);
        IPEndPoint endpoint = new IPEndPoint(IPAddress, Port);
        await tcpClient.ConnectAsync(endpoint);
        transportLayer = new TcpTransportLayer(tcpClient);
    }

    public async Task Send(byte[] dataBuffer)
    {
        if (transportLayer == null) throw new InvalidOperationException();
        var message = new TransportLayerMessage(startByte, dataBuffer.Length, new ReadOnlyMemory<byte>(dataBuffer));
        await transportLayer.SendMessage(message);
    }

    public async Task<TransportLayerMessage> Receive()
    {
        if (transportLayer == null) throw new InvalidOperationException();
        return await transportLayer.ReceiveMessage();
    }


    public void Disconnect()
    {
        tcpClient?.Close();
    }

    public void Dispose()
    {
        Disconnect();
        transportLayer?.CancelRunningOperations();
        tcpClient?.Dispose();
    }

    private ITcpClient BuildClient(string targetHost,bool useTls = true)
    {
        if (!useTls) return new TcpClientAdapter(new TcpClient());

        if (acceptedRemoteCertificate == null) throw new InvalidOperationException("No server certificate has been imported");
        RemoteCertificateValidationCallback certificateValidation = new((object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors) =>
            acceptedRemoteCertificate != null && acceptedRemoteCertificate.Equals(certificate)
        );
        return new TlsTcpClientAdapter(new TcpClient(), certificateValidation, targetHost, localCertificate);
    }
}

