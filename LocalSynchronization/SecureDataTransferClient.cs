using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace LocalSynchronization;

public class SecureDataTransferClient : SecureDataTransferBase
{

    private string RemoteHost => acceptedRemoteCertificate?.GetNameInfo(X509NameType.SimpleName, false) ?? "";

    public SecureDataTransferClient(string ipString, int port) : this(ipString, port, new CertificateStore()) { }

    public SecureDataTransferClient(string ipString, int port, CertificateStore certStore) : base(ipString, port, certStore, "testclient")
    {
    }

    public async Task<bool> Pair(ReadOnlyMemory<byte> secret)
    {
        if (transportLayer == null) throw new InvalidOperationException("Connection has to be established first");
        var message = new TransportLayerMessage(MessageType.PairingRequest, secret.Length, secret);
        await transportLayer.SendMessage(message);
        var ack = await transportLayer.ReceiveMessage();
        if (ack != null && ack.StartByte == (byte)MessageType.PairingResponse)
            return true;
        
        Disconnect(); // close connection on pair failure
        return false;
        //TODO: provide more data on failure, maybe exception
    }

    public async Task Connect()
    {
        tcpClient = BuildClient(new TcpClient());
        IPEndPoint endpoint = new IPEndPoint(IPAddress, Port);
        await tcpClient.ConnectAsync(endpoint);
        transportLayer = new TcpTransportLayer(tcpClient);
        connected = true;
    }

    public void Disconnect()
    {
        tcpClient?.Close();
        connected = false;
    }

    public override void Dispose()
    {
        Disconnect();
        transportLayer?.CancelRunningOperations();
        tcpClient?.Dispose();
    }

    protected override ITcpClient BuildClient(TcpClient tcpClient)
    {
        if (acceptedRemoteCertificate == null) throw new InvalidOperationException("No server certificate has been imported");
        RemoteCertificateValidationCallback certificateValidation = new((object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors) =>
            acceptedRemoteCertificate != null && acceptedRemoteCertificate.Equals(certificate)
        );
        return new TlsTcpClientAdapter(tcpClient, certificateValidation, RemoteHost, localCertificate);
    }

}

