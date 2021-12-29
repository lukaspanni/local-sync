namespace LocalSynchronization;

internal interface ITransportLayer : IDisposable
{
    internal Task SendMessage(TransportLayerMessage message);
    internal Task<TransportLayerMessage> ReceiveMessage();
    void CancelRunningOperations();
}
