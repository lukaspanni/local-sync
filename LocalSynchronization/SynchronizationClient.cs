using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace LocalSynchronization;

public class SynchronizationClient : SynchronizationBase, IDisposable
{

    private ITcpClient? tcpClient;
    protected ITransportLayer? transportLayer;

    private string RemoteHost => acceptedRemoteCertificate?.GetNameInfo(X509NameType.SimpleName, false) ?? "";


    public SynchronizationClient(string ipString, int port) : this(ipString, port, new CertificateStore()) { }

    internal SynchronizationClient(string ipString, int port, CertificateStore certStore) : base(ipString, port, certStore, "testclient")
    {
    }

    public async Task Connect()
    {
        tcpClient = BuildClient(new TcpClient());
        IPEndPoint endpoint = new IPEndPoint(IPAddress, Port);
        await tcpClient.ConnectAsync(endpoint);
        transportLayer = new TcpTransportLayer(tcpClient);
    }

    public async Task Send(byte[] dataBuffer)
    {
        if (transportLayer == null) throw new InvalidOperationException();
        var message = new TransportLayerMessage(dataBuffer.Length, new ReadOnlyMemory<byte>(dataBuffer));
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

    protected override ITcpClient BuildClient(TcpClient tcpClient, bool useTls = true)
    {
        if (!useTls) return new TcpClientAdapter(tcpClient);

        if (acceptedRemoteCertificate == null) throw new InvalidOperationException("No server certificate has been imported");
        RemoteCertificateValidationCallback certificateValidation = new((object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors) =>
            acceptedRemoteCertificate != null && acceptedRemoteCertificate.Equals(certificate)
        );
        return new TlsTcpClientAdapter(tcpClient, certificateValidation, RemoteHost, localCertificate);
    }
}

