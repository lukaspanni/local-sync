namespace LocalSynchronization;

public record TransportLayerMessage(byte StartByte, int Length, ReadOnlyMemory<byte> Data);
// startByte & 0b01 always != 0
// startByte & 0b10 != 0 -> message without data

public interface ITransportLayer : IDisposable
{
    Task SendMessage(TransportLayerMessage message);
    Task<TransportLayerMessage> ReceiveMessage();
    void CancelRunningOperations();
}
