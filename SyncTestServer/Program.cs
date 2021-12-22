using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System;

public class SyncServer
{
    private const int readTimeoutSeconds = 30;
    private TcpListener listener;
    private CancellationTokenSource tokenSource;

    public IPAddress IPAddress { get; private set; }
    public int Port { get; private set; } = 4820;

    public SyncServer(string ipString, int port)
    {
        IPAddress = IPAddress.Parse(ipString);
        Port = port;
        tokenSource = new CancellationTokenSource();
    }

    public async Task StartListening()
    {
        Console.WriteLine("Listening on {0}:{1}", IPAddress.ToString(), Port);
        listener = new TcpListener(IPAddress, Port);
        listener.Start();
        var token = tokenSource.Token;
        while (!token.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            HandleConnection(client, token);
        }
    }

    private async Task HandleConnection(TcpClient client, CancellationToken token)
    {
        Console.WriteLine("Client connected");

        using (client)
        {
            using var stream = client.GetStream();
            while (!token.IsCancellationRequested)
            {
                var receiveStream = await Receive(stream, token).ConfigureAwait(false);
                Console.WriteLine("Received {0}", Encoding.UTF8.GetString(receiveStream.ToArray()));
                var responseBuffer = new byte[5];
                responseBuffer[0] = 0x01;
                BitConverter.GetBytes(receiveStream.Length).CopyTo(responseBuffer, 1); // write received data length to buffer
                await stream.WriteAsync(new ReadOnlyMemory<byte>(responseBuffer), token).ConfigureAwait(false);
            }
        }
        Console.WriteLine("Client disconnected");

    }

    private static async Task<ReadOnlyMemory<byte>> Receive(NetworkStream stream, CancellationToken token)
    {
        var TimeoutTask = Task.Delay(TimeSpan.FromSeconds(readTimeoutSeconds));
        byte[]? receiveBuffer = null;
        var readBuffer = new byte[2048];
        int dataLength = 0;
        int receiveBufferIndex = 0;
        do
        {
            var readTask = stream.ReadAsync(readBuffer, 0, readBuffer.Length, token);
            var race = await Task.WhenAny(readTask, TimeoutTask).ConfigureAwait(false);
            if (race != readTask)
            {
                Console.WriteLine("Client Timeout");
                break;
            }
            var readLength = await readTask;
            Debug.WriteLine("Read {0} Bytes", readLength);
            if (readLength == 0) break;
            int fragmentLength;
            if (receiveBuffer == null)
            {
                dataLength = BitConverter.ToInt32(readBuffer, 1);   // get data length from first fragment
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

        return new ReadOnlyMemory<byte>(receiveBuffer, 0, dataLength);
    }

    public void Stop()
    {
        tokenSource.Cancel();
    }


}




public class Program
{
    public static async Task Main(string[] args)
    {
        var server = new SyncServer("0.0.0.0", 4820);
        Console.CancelKeyPress += delegate
        {
            Console.WriteLine("Stopping");
            server.Stop();
        };
        server.StartListening();
        while (true)
        { await Task.Delay(100); }

    }
}