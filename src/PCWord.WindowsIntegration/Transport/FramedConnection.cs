using System.Buffers.Binary;
using PCWord.WindowsIntegration.Protocol;

namespace PCWord.WindowsIntegration.Transport;

/// <summary>Bounded, length-prefixed transport. Authentication must complete before use.</summary>
public sealed class FramedConnection(Stream stream) : IAsyncDisposable
{
    private readonly SemaphoreSlim _writer = new(1, 1);
    private int _closed;

    public async ValueTask<AdapterMessage> ReadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            byte[] header = new byte[4];
            await stream.ReadExactlyAsync(header, cancellationToken);
            int length = BinaryPrimitives.ReadInt32LittleEndian(header);
            if (length is <= 0 or > AdapterMessageCodec.MaximumFrameBytes)
                throw new InvalidDataException("Invalid frame length.");
            byte[] payload = new byte[length];
            await stream.ReadExactlyAsync(payload, cancellationToken);
            return AdapterMessageCodec.Deserialize(payload);
        }
        catch
        {
            // A partial frame cannot safely be resumed as another message.
            await DisposeAsync();
            throw;
        }
    }

    public async ValueTask WriteAsync(AdapterMessage message, CancellationToken cancellationToken = default)
    {
        byte[] payload = AdapterMessageCodec.Serialize(message);
        await _writer.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
            byte[] frame = new byte[payload.Length + 4];
            BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
            payload.CopyTo(frame, 4);
            await stream.WriteAsync(frame, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
        finally { _writer.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _closed, 1) == 0) await stream.DisposeAsync();
    }
}
