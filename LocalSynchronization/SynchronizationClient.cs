using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace LocalSynchronization;

public class SynchronizationClient : IDisposable
{

    private const int startByte = 0x01;
    private CertificateStore clientCertificateStore;
    private ITcpClient? tcpClient;
    private ITransportLayer? transportLayer;
    private X509Certificate2 certificate;


    public IPAddress IPAddress { get; private set; }
    public int Port { get; private set; } = 4820;

    public byte[] PublicKeyBytes => certificate.Export(X509ContentType.Cert);

    public SynchronizationClient(string ipString, int port) : this(ipString, port, new CertificateStore()) { }

    internal SynchronizationClient(string ipString, int port, CertificateStore keystore)
    {
        IPAddress = IPAddress.Parse(ipString);
        Port = port;
        clientCertificateStore = keystore;

        certificate = clientCertificateStore.GetCertificateByCommonName("testclient");
    }

    public void ImportServerCertificate(string base64EncodedCertificate)
    {
        clientCertificateStore.SetAcceptedRemoteCertificate(base64EncodedCertificate);
    }

    public async Task Connect()
    {
        tcpClient = BuildClient(clientCertificateStore.RemoteHost);
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
        tcpClient.Close();
    }

    public void Dispose()
    {
        Disconnect();
        transportLayer?.CancelRunningOperations();
        tcpClient?.Dispose();
    }

    private ITcpClient BuildClient(string targetHost, bool useTls = true)
    {
        if (!useTls) return new TcpClientAdapter(new TcpClient());

        if (clientCertificateStore.AccpetedCertificate == null) throw new InvalidOperationException("No server certificate has been imported");
        RemoteCertificateValidationCallback certificateValidation = new((object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors) =>
            clientCertificateStore.AccpetedCertificate != null && clientCertificateStore.AccpetedCertificate.Equals(certificate)
        );
        return new TlsTcpClientAdapter(new TcpClient(), certificateValidation, targetHost, certificate);
    }
}

