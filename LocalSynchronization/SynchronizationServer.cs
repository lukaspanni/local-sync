using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LocalSynchronization
{

    public class SynchronizationServer
    {
        private TcpListener listener;
        private CancellationTokenSource tokenSource = new CancellationTokenSource();

        public IPAddress IPAddress { get; private set; }
        public int Port { get; private set; } = 4820;

        public SynchronizationServer(string ipString, int port)
        {
            IPAddress = IPAddress.Parse(ipString);
            Port = port;
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
                        //ignore error for now
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

        private static ITcpClient BuildClient(TcpClient tcpClient, bool useTls = true)
        {
            if (!useTls) return new TcpClientAdapter(tcpClient);

            //TODO: provide certificate from a local keystore, or generate if nothing found
            var certificate = Keystore.GenerateSelfSignedCertificate("testserver");
            return new TlsTcpClientAdapter(tcpClient, certificate);
        }

    }

}
