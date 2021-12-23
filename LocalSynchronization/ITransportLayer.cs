namespace LocalSynchronization;

public interface ITransportLayer : IDisposable
{
    Task SendMessage(TransportLayerMessage message);
    Task<TransportLayerMessage> ReceiveMessage();
    void CancelRunningOperations();
}
