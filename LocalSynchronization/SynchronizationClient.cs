using System.Net;
using System.Net.Sockets;

namespace LocalSynchronization;

public class SynchronizationClient : IDisposable
{

    private const int startByte = 0x01;
    private ITcpClient tcpClient;
    private ITransportLayer? transportLayer;

    public IPAddress IPAddress { get; private set; }
    public int Port { get; private set; } = 4820;

    public SynchronizationClient(string ipString, int port, bool useTls = true)
    {
        IPAddress = IPAddress.Parse(ipString);
        Port = port;

        tcpClient = BuildClient(useTls);
    }


    public void ImportServerCertificate(string base64EncodedCertificate)
    {
        Keystore.SetAcceptedRemoteCertificate(base64EncodedCertificate);
    }

    public async Task Connect()
    {
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
        tcpClient.Dispose();
    }

    private static ITcpClient BuildClient(bool useTls = true)
    {
        if (!useTls) return new TcpClientAdapter(new TcpClient());
        return new TlsTcpClientAdapter(new TcpClient(), "testserver", Keystore.GenerateSelfSignedCertificate("testclient"));
    }
}

