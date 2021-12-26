
namespace LocalSynchronization;

public class TlsTcpTransportLayer : ITransportLayer
{
    private CancellationTokenSource tokenSource = new CancellationTokenSource();

    public async Task<TransportLayerMessage> ReceiveMessage()
    {
        throw new NotImplementedException();
    }

    public async Task SendMessage(TransportLayerMessage message)
    {
        throw new NotImplementedException();
    }

    public void CancelRunningOperations()
    {
        tokenSource.Cancel();   
    }

    public void Dispose()
    {

    }
}

