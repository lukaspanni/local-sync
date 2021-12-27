using LocalSynchronization;
using System.Text;
public class Program
{

    public static async Task Main(string[] args)
    {
        using var client = new SynchronizationClient("127.0.0.1", 8080);

        Console.Write("Enter server certificate: ");
        var base64EncodedCertificate = Console.ReadLine();
        if (base64EncodedCertificate == null) throw new ArgumentException("Invalid input");
        client.ImportRemoteCertificate(base64EncodedCertificate);

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
            Console.WriteLine("server received {1} bytes", BitConverter.ToString(new byte[] { response.StartByte }), response.Length);
        }

    }

}