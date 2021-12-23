using LocalSynchronization;
using System.Diagnostics;
using System.Net.Sockets;

namespace LocalSynchronization;

public class TcpTransportLayer : ITransportLayer
{
    private ITcpClient tcpClient;
    private CancellationTokenSource tokenSource = new CancellationTokenSource();
    private readonly int readTimeoutSeconds = 30;

    public TcpTransportLayer(ITcpClient tcpClient)
    {
        if (tcpClient == null || !tcpClient.Connected)
            throw new ArgumentException("Passed TCPClient is not valid");
        //TODO: tcpClient.Connected cant always be used to determine connection state
        this.tcpClient = tcpClient;
    }

    public async Task SendMessage(TransportLayerMessage message)
    {
        Debug.WriteLine("Sending message {0} | {1} | {2}", BitConverter.ToString(new byte[] { message.StartByte }), message.Length, BitConverter.ToString(message.Payload.ToArray()));
        await tcpClient.SendAsync(message.Serialize());
    }

    public async Task<TransportLayerMessage> ReceiveMessage()
    {
        var TimeoutTask = Task.Delay(TimeSpan.FromSeconds(readTimeoutSeconds));
        byte[]? receiveBuffer = null;
        var readBuffer = new byte[2048];
        int dataLength = 0;
        int receiveBufferIndex = 0;
        byte startByte = 0;
        do
        {
            var readTask = tcpClient.ReceiveAsync(readBuffer, 0, readBuffer.Length, tokenSource.Token);
            var race = await Task.WhenAny(readTask, TimeoutTask).ConfigureAwait(false);
            if (race != readTask)
            {
                Console.WriteLine("Client Timeout");
                throw new TimeoutException("Receive timed out");
            }
            var readLength = await readTask;
            Debug.WriteLine("Read {0} Bytes", readLength);
            if (readLength == 0) throw new Exception("Received 0 bytes of data");
            if (receiveBuffer == null)
            {
                dataLength = BitConverter.ToInt32(readBuffer, 1);   // get data length from first fragment
                receiveBuffer = new byte[dataLength + 5];
            }
            int readBytes = Math.Min(receiveBuffer.Length - receiveBufferIndex, readBuffer.Length);
            Array.Copy(readBuffer, 0, receiveBuffer, receiveBufferIndex, readBytes);
            receiveBufferIndex += readBuffer.Length;
        } while (receiveBufferIndex < receiveBuffer.Length);

        return TransportLayerMessage.Deserialize(receiveBuffer);
    }

    public void CancelRunningOperations()
    {
        tokenSource.Cancel();
    }

    public void Dispose()
    {
        tcpClient.Dispose();
    }
}