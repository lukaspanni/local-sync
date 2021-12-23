using System.Net;
using System.Net.Sockets;

namespace LocalSynchronization;

public interface ITcpClient : IDisposable
{
    bool Connected { get; }

    Task ConnectAsync(IPEndPoint remoteEP);
    ValueTask ConnectAsync(IPEndPoint remoteEP, CancellationToken token);

    ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default(CancellationToken));

    Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);

    void Close();

}


internal class TcpClientAdapter : ITcpClient
{
    public TcpClient Client { get; init; }

    public TcpClientAdapter(TcpClient client)
    {
        Client = client;
    }

    public bool Connected => Client.Connected;


    public void Close()
    {
        Client.Close();
    }

    public Task ConnectAsync(IPEndPoint remoteEP)
    {
        return Client.ConnectAsync(remoteEP);
    }

    public ValueTask ConnectAsync(IPEndPoint remoteEP, CancellationToken token)
    {
        return Client.ConnectAsync(remoteEP, token);
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return Client.GetStream().WriteAsync(buffer, cancellationToken);
    }

    public Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return Client.GetStream().ReadAsync(buffer, offset, count, cancellationToken);
    }


    public void Dispose()
    {
        Client.Dispose();
    }
}