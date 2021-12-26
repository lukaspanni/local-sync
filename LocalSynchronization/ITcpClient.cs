using System.Net;

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
