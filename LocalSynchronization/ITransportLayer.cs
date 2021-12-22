namespace LocalSynchronization;

public record TransportLayerMessage(byte StartByte, int Length, ReadOnlyMemory<byte> Data);

public interface ITransportLayer
{
    Task SendMessage(TransportLayerMessage message);
    Task<TransportLayerMessage> ReceiveMessage();
    void CancelRunningOperations();
}
