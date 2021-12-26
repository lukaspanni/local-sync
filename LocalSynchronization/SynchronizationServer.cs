using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace LocalSynchronization
{

    public class SynchronizationServer
    {
        private Keystore serverKeystore;
        private TcpListener? listener;
        private CancellationTokenSource tokenSource = new CancellationTokenSource();
        private X509Certificate2 certificate;

        public IPAddress IPAddress { get; private set; }
        public int Port { get; private set; } = 4820;

        public byte[] PublicKeyBytes => certificate.Export(X509ContentType.Cert);

        public SynchronizationServer(string ipString, int port) : this(ipString, port, new Keystore()) { }

        internal SynchronizationServer(string ipString, int port, Keystore keystore)
        {
            IPAddress = IPAddress.Parse(ipString);
            Port = port;
            serverKeystore = keystore;

            certificate = serverKeystore.GetCertificateByCommonName("testserver");
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

        private ITcpClient BuildClient(TcpClient tcpClient, bool useTls = true)
        {
            if (!useTls) return new TcpClientAdapter(tcpClient);
            return new TlsTcpClientAdapter(tcpClient, new System.Net.Security.RemoteCertificateValidationCallback(serverKeystore.ValidateClientCertificate), certificate);
        }

    }

}
