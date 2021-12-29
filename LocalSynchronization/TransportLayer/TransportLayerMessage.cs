namespace LocalSynchronization;

public enum MessageType
{
    Standard = 0b01,
    Ack = 0b11,
    Error = 0b00,
    PairingRequest = 0b101,
    PairingResponse = 0b111
}

// startByte & 0b01 always != 0
// startByte & 0b10 != 0 -> message without data = "ACK"
// startByte & 0b100 != 0 -> pairing request containing secret
public class TransportLayerMessage
{
    public byte StartByte { get; init; }
    public int Length { get; init; }
    public ReadOnlyMemory<byte> Payload { get; init; }

    public TransportLayerMessage(ReadOnlyMemory<byte> payload): this((byte)MessageType.Standard, payload.Length, payload) { }
    public TransportLayerMessage(MessageType type, int length): this((byte)type, length, new ReadOnlyMemory<byte>()) { }
    public TransportLayerMessage(MessageType type, int length, ReadOnlyMemory<byte> payload) : this((byte)type, length, payload) { }

    private TransportLayerMessage(byte startByte, int length, ReadOnlyMemory<byte> payload)
    {
        StartByte = startByte;
        Length = length;
        Payload = payload;
    }

    public ReadOnlyMemory<byte> Serialize()
    {
        byte[] serialized = new byte[1 + 4 + Payload.Length];
        serialized[0] = StartByte;
        BitConverter.GetBytes(Length).CopyTo(serialized, 1);
        Payload.CopyTo(new Memory<byte>(serialized, 5, Payload.Length));
        return new ReadOnlyMemory<byte>(serialized);
    }

    public static TransportLayerMessage Deserialize(ReadOnlyMemory<byte> serialized)
    {
        byte startByte = serialized.Span[0];
        int length = BitConverter.ToInt32(serialized.Span.Slice(1, 4));
        ReadOnlyMemory<byte> payload;
        if ((startByte & 0b10) != 0)    // flag set for only length (message used as ACK)
        {
            payload = new ReadOnlyMemory<byte>();
        }
        else
        {
            payload = serialized.Slice(5, length);
        }
        return new TransportLayerMessage(startByte, length, payload);
    }
}

