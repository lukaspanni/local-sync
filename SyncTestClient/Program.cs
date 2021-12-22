using System.Net;
using System.Net.Sockets;
using System.Text;

public class SyncClient : IDisposable
{
    private const int startByte = 0x01;
    private CancellationTokenSource tokenSource;
    private TcpClient tcpClient;

    public IPAddress IPAddress { get; private set; }
    public int Port { get; private set; } = 4820;

    public SyncClient(string ipString, int port)
    {
        IPAddress = IPAddress.Parse(ipString);
        Port = port;
        tokenSource = new CancellationTokenSource();
        tcpClient = new TcpClient();
    }

    public async Task Connect()
    {
        IPEndPoint endpoint = new IPEndPoint(IPAddress, Port);
        await tcpClient.ConnectAsync(endpoint);
    }

    public async Task Send(byte[] dataBuffer)
    {
        var stream = tcpClient.GetStream();
        var sendBufferLength = 5 + dataBuffer.Length;        
        var sendBuffer = new byte[sendBufferLength];
        sendBuffer[0] = startByte;
        BitConverter.GetBytes(dataBuffer.Length).CopyTo(sendBuffer, 1); // write data length to buffer 
        Array.Copy(dataBuffer, 0, sendBuffer, 5, dataBuffer.Length);
        await stream.WriteAsync(sendBuffer, 0, sendBuffer.Length, tokenSource.Token);
    }

    public async Task<ReadOnlyMemory<byte>> Receive()
    {
        var stream = tcpClient.GetStream();
        var buffer = new byte[4096];
        var readLength = await stream.ReadAsync(buffer, 0, buffer.Length, tokenSource.Token);
        return new ReadOnlyMemory<byte>(buffer, 0, readLength);
    }


    public void Disconnect()
    {
        tcpClient.Close();
    }

    public void Dispose()
    {
        Disconnect();
        tokenSource.Cancel();
        tcpClient.Dispose();
    }
}

public class Program
{

    public static async Task Main(string[] args)
    {
        using var client = new SyncClient("127.0.0.1", 4820);
        await client.Connect();
        Console.WriteLine("Connected to {0}:{1}", client.IPAddress.ToString(), client.Port);
        while (true)
        {
            Console.Write("Send: ");
            var message = Console.ReadLine();
            if (message == null) break;
            var sendBuffer = Encoding.UTF8.GetBytes(message);
            await client.Send(sendBuffer);
            var response = await client.Receive();
            if (response.Length != 0) Console.WriteLine("Response: Server received {0} bytes", BitConverter.ToInt32(response.ToArray(), 1));
        }

    }

}