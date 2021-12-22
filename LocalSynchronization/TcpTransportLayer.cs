using LocalSynchronization;
using System.Diagnostics;
using System.Net.Sockets;

public class TcpTransportLayer : ITransportLayer
{
    private TcpClient tcpClient;
    private CancellationTokenSource tokenSource = new CancellationTokenSource();
    private readonly int readTimeoutSeconds = 30;

    public TcpTransportLayer(TcpClient tcpClient)
    {
        if (tcpClient == null || !tcpClient.Connected)
            throw new ArgumentException("Passed TCPClient is not valid");
        //TODO: tcpClient.Connected cant always be used to determine connection state
        this.tcpClient = tcpClient;
    }

    public async Task SendMessage(TransportLayerMessage message)
    {
        Debug.WriteLine("Sending message {0} | {1} | {2}", BitConverter.ToString(new byte[] { message.StartByte }), message.Length, BitConverter.ToString(message.Data.ToArray()));
        var stream = tcpClient.GetStream();
        var sendBufferLength = 5 + message.Data.Length;
        var sendBuffer = new byte[sendBufferLength];
        // copy contents of message to buffer
        //TODO: move serialization/deserialization to TransportLayerMessage
        sendBuffer[0] = message.StartByte;
        BitConverter.GetBytes(message.Length).CopyTo(sendBuffer, 1);
        //if ((message.StartByte & 0b10) != 0) //TODO: extract handling of different startBytes
        //{
        //    await stream.WriteAsync(new ReadOnlyMemory<byte>(sendBuffer, 0, 5), tokenSource.Token);
        //    return;
        //}
        message.Data.CopyTo(new Memory<byte>(sendBuffer, 5, message.Data.Length));
        ReadOnlyMemory<byte> sendMemory = new ReadOnlyMemory<byte>(sendBuffer);
        await stream.WriteAsync(sendMemory, tokenSource.Token);
    }

    public async Task<TransportLayerMessage> ReceiveMessage()
    {
        var stream = tcpClient.GetStream();
        var TimeoutTask = Task.Delay(TimeSpan.FromSeconds(readTimeoutSeconds));
        byte[]? receiveBuffer = null;
        var readBuffer = new byte[2048];
        int dataLength = 0;
        int receiveBufferIndex = 0;
        byte startByte = 0;
        do
        {
            var readTask = stream.ReadAsync(readBuffer, 0, readBuffer.Length, tokenSource.Token);
            var race = await Task.WhenAny(readTask, TimeoutTask).ConfigureAwait(false);
            if (race != readTask)
            {
                Console.WriteLine("Client Timeout");
                throw new TimeoutException("Receive timed out");
            }
            var readLength = await readTask;
            Debug.WriteLine("Read {0} Bytes", readLength);
            if (readLength == 0) throw new Exception("Received 0 bytes of data");
            int fragmentLength;
            if (receiveBuffer == null)
            {
                startByte = readBuffer[0];
                dataLength = BitConverter.ToInt32(readBuffer, 1);   // get data length from first fragment
                if ((startByte & 0b10) != 0)
                {
                    Console.WriteLine("Received ack for {0} bytes", dataLength);
                    return new TransportLayerMessage(startByte, dataLength, new ReadOnlyMemory<byte>());
                }

                receiveBuffer = new byte[dataLength];
                Debug.WriteLine("Prepare to receive {0} bytes of data", dataLength);

                fragmentLength = readLength - 5;
                Array.Copy(readBuffer, 5, receiveBuffer, receiveBufferIndex, fragmentLength);
            }
            else
            {
                fragmentLength = readLength;
                Array.Copy(readBuffer, 0, receiveBuffer, receiveBufferIndex, fragmentLength);
            }

            receiveBufferIndex += fragmentLength;
        } while (receiveBufferIndex < dataLength);

        return new TransportLayerMessage(startByte, dataLength, new ReadOnlyMemory<byte>(receiveBuffer));
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