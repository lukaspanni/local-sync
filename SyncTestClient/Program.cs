using LocalSynchronization;
using System.Text;
public class Program
{

    public static async Task Main(string[] args)
    {
        using var client = new SecureDataTransferClient("127.0.0.1", 8080);

        Console.Write("Enter server certificate: ");
        var base64EncodedCertificate = Console.ReadLine();
        if (base64EncodedCertificate == null) throw new ArgumentException("Invalid input");
        client.ImportRemoteCertificate(base64EncodedCertificate);

        Console.Write("Enter server secret: ");
        var base64EncodedSecret = Console.ReadLine();
        if (base64EncodedSecret== null) throw new ArgumentException("Invalid input");

        await client.Connect();
        await client.Pair(new ReadOnlyMemory<byte>(Convert.FromBase64String(base64EncodedSecret)));
        Console.WriteLine("Connected to {0}:{1}", client.IPAddress.ToString(), client.Port);
        while (true)
        {
            Console.Write("Send: ");
            var message = Console.ReadLine();
            if (message == null) break;
            var sendBuffer = Encoding.UTF8.GetBytes(message);
            await client.SendData(sendBuffer);
        }

    }

}