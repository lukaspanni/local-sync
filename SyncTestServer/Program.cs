using LocalSynchronization;

public class Program
{
    public static async Task Main(string[] args)
    {
        using var server = new SecureDataTransferServer("0.0.0.0", 8080);
        Console.CancelKeyPress += delegate
        {
            Console.WriteLine("Stopping");
            server.Dispose();
        };

        Console.WriteLine($"Server certificate: {Convert.ToBase64String(server.PublicKeyBytes)}");

        server.PreparePair();
        Console.WriteLine($"Secret bytes: { Convert.ToBase64String(server.SharedSecret)}");


        await server.AcceptPairRequest();

        await server.ReceiveData();

        server.CloseConnection();

    }
}