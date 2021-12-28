using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace LocalSynchronization
{
    public abstract class DataTransferBase : IDisposable
    {
        protected X509Certificate2 localCertificate;
        protected X509Certificate2? acceptedRemoteCertificate;
        protected CertificateStore certificateStore;

        public IPAddress IPAddress { get; private set; }
        public int Port { get; private set; } = 4820;
        public byte[] PublicKeyBytes => localCertificate.Export(X509ContentType.Cert);

        protected DataTransferBase(string ipString, int port, CertificateStore certStore, string localCertificateName)
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

        protected abstract ITcpClient BuildClient(TcpClient tcpClient, bool useTls = true);

        public abstract void Dispose();
    }

    public enum ResponseState
    {
        Ok,
        Error
    }
    public record DataResponse(ResponseState State, ReadOnlyMemory<byte>? Data);
}