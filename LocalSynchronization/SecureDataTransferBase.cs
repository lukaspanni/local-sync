using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace LocalSynchronization
{
    public abstract class SecureDataTransferBase : IDisposable
    {
        protected X509Certificate2 localCertificate;
        protected X509Certificate2? acceptedRemoteCertificate;
        protected CertificateStore certificateStore;
        protected ITcpClient? tcpClient;
        protected ITransportLayer? transportLayer;
        protected CancellationTokenSource tokenSource = new CancellationTokenSource();
        protected bool connected = false;

        public IPAddress IPAddress { get; private set; }
        public int Port { get; private set; } = 4820;
        public byte[] PublicKeyBytes => localCertificate.Export(X509ContentType.Cert);

        protected SecureDataTransferBase(string ipString, int port, CertificateStore certStore, string localCertificateName)
        {
            IPAddress = IPAddress.Parse(ipString);
            Port = port;
            certificateStore = certStore;
            localCertificate = certificateStore.GetOrGenerateLocalCertificate(localCertificateName);

        }
        public virtual void ImportRemoteCertificate(string base64EncodedCertificate)
        {
            var imported = new X509Certificate2(Convert.FromBase64String(base64EncodedCertificate));
            if (imported == null || imported.HasPrivateKey) throw new ArgumentException("Provided certificate cannot be used for this operation");
            acceptedRemoteCertificate = imported;
        }



        public async Task<bool> SendData(ReadOnlyMemory<byte> data)
        {
            if (!connected || transportLayer == null) throw new InvalidOperationException("Not Connected");
            try
            {
                var message = new TransportLayerMessage(data.Length, data);
                Console.WriteLine("Sending {0} bytes", message.Length);
                await transportLayer.SendMessage(message).ConfigureAwait(false); // send data
                var ack = await transportLayer.ReceiveMessage().ConfigureAwait(false); // receive ack
                                                                                       //TODO: check ack
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<DataResponse> ReceiveData()
        {
            if (!connected || transportLayer == null) throw new InvalidOperationException("Not Connected");
            try
            {
                var message = await transportLayer.ReceiveMessage().ConfigureAwait(false);
                Console.WriteLine("Received {0} bytes: {1}", message.Length, Encoding.UTF8.GetString(message.Payload.ToArray()));
                await SendAckForMessage(message).ConfigureAwait(false);
                return new DataResponse(ResponseState.Ok, message.Payload);
            }
            catch (Exception ex)
            {
                //return error
                return new DataResponse(ResponseState.Error, null);
            }
        }
        public abstract void Dispose();

        protected async Task SendAckForMessage(TransportLayerMessage message)
        {
            if (transportLayer == null) throw new InvalidOperationException("Not Connected");
            var ack = new TransportLayerMessage(0b11, message.Length, new ReadOnlyMemory<byte>());
            await transportLayer.SendMessage(ack).ConfigureAwait(false);
        }

        protected abstract ITcpClient BuildClient(TcpClient tcpClient);

    }

    public enum ResponseState
    {
        Ok,
        Error
    }
    public record DataResponse(ResponseState State, ReadOnlyMemory<byte>? Data);
}