using System.Net;
using System.Net.Sockets;

namespace LocalSynchronization;

internal class TcpClientAdapter : ITcpClient
{
    public TcpClient Client { get; init; }

    public TcpClientAdapter(TcpClient client)
    {
        Client = client;
    }

    public bool Connected => Client.Connected;

    protected virtual Stream? CommunicationStream => Client.GetStream();


    public void Close()
    {
        Client.Close();
    }

    public virtual Task ConnectAsync(IPEndPoint remoteEP)
    {
        return Client.ConnectAsync(remoteEP);
    }

    public virtual ValueTask ConnectAsync(IPEndPoint remoteEP, CancellationToken token)
    {
        return Client.ConnectAsync(remoteEP, token);
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (CommunicationStream == null) { throw new ArgumentNullException(); }
        return CommunicationStream.WriteAsync(buffer, cancellationToken);
    }

    public Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if(CommunicationStream == null) { throw new ArgumentNullException(); }
        return CommunicationStream.ReadAsync(buffer, offset, count, cancellationToken);
    }


    public void Dispose()
    {
        Client.Dispose();
    }
}
