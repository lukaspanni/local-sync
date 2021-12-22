using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LocalSynchronization
{

    public class SynchronizationClient : IDisposable
    {
        private const int startByte = 0x01;
        private TcpClient tcpClient;
        private ITransportLayer? transportLayer;

        public IPAddress IPAddress { get; private set; }
        public int Port { get; private set; } = 4820;

        public SynchronizationClient(string ipString, int port)
        {
            IPAddress = IPAddress.Parse(ipString);
            Port = port;
            tcpClient = new TcpClient();
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
            transportLayer.CancelRunningOperations();
            tcpClient.Dispose();
        }
    }
}
