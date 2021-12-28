using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace LocalSynchronization;

public class DataTransferClient : DataTransferBase
{

    private ITcpClient? tcpClient;
    protected ITransportLayer? transportLayer;

    private string RemoteHost => acceptedRemoteCertificate?.GetNameInfo(X509NameType.SimpleName, false) ?? "";

    public DataTransferClient(string ipString, int port) : this(ipString, port, new CertificateStore()) { }

    internal DataTransferClient(string ipString, int port, CertificateStore certStore) : base(ipString, port, certStore, "testclient")
    {
    }

    public async Task Pair(ReadOnlyMemory<byte> secret)
    {
        if (transportLayer == null) throw new InvalidOperationException("Connection has to be established first");
        var message = new TransportLayerMessage(0x01 | 1 << 2, secret.Length, secret);
        await transportLayer.SendMessage(message);
        var ack = await transportLayer.ReceiveMessage();

        //TODO: verify pairing success
    }

    public async Task Connect()
    {
        tcpClient = BuildClient(new TcpClient());
        IPEndPoint endpoint = new IPEndPoint(IPAddress, Port);
        await tcpClient.ConnectAsync(endpoint);
        transportLayer = new TcpTransportLayer(tcpClient);
    }

    //TODO: pull common methods up to base
    public async Task<bool> SendData(ReadOnlyMemory<byte> data)
    {
        if (transportLayer == null) throw new InvalidOperationException();
        var message = new TransportLayerMessage(data.Length, data);
        await transportLayer.SendMessage(message);
        var ack = await transportLayer.ReceiveMessage().ConfigureAwait(false); // receive ack
        //TODO: verify ack
        return true;
    }

    public async Task<DataResponse> ReceiveData()
    {
        if (transportLayer == null) throw new InvalidOperationException();
        var message = await transportLayer.ReceiveMessage();
        await SendAck(message);
        return new DataResponse(ResponseState.Ok, message.Payload);
    }


    public void Disconnect()
    {
        tcpClient?.Close();
    }

    public override void Dispose()
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

    protected async Task SendAck(TransportLayerMessage message)
    {
        if (transportLayer == null) throw new InvalidOperationException("Not Connected");
        var ack = new TransportLayerMessage(0b11, message.Length, new ReadOnlyMemory<byte>());
        await transportLayer.SendMessage(ack).ConfigureAwait(false);
    }
}

