using LocalSynchronization;

public class Program
{
    public static async Task Main(string[] args)
    {
        var server = new SynchronizationServer("0.0.0.0", 8080);
        Console.CancelKeyPress += delegate
        {
            Console.WriteLine("Stopping");
            server.Stop();
        };

        Console.WriteLine($"Server certificate: {Convert.ToBase64String(server.PublicKeyBytes)}");
        server.StartListening();
        while (true)
        { await Task.Delay(100); }
    }
}