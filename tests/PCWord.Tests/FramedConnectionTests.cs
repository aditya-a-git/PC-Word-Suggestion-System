using System.Buffers.Binary;
using PCWord.WindowsIntegration.Protocol;
using PCWord.WindowsIntegration.Transport;
using Xunit;

namespace PCWord.Tests;

public sealed class FramedConnectionTests
{
    private static AdapterMessage Pause() => new(1, AdapterMessageKind.Pause, Guid.NewGuid(), 1);

    [Fact]
    public async Task MultipleFramesRoundTripWithoutMixing()
    {
        using var stream = new MemoryStream();
        await using var connection = new FramedConnection(stream);
        var first = Pause(); var second = Pause();
        await connection.WriteAsync(first);
        await connection.WriteAsync(second);
        stream.Position = 0;
        Assert.Equal(first, await connection.ReadAsync());
        Assert.Equal(second, await connection.ReadAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(8193)]
    [InlineData(int.MaxValue)]
    public async Task InvalidLengthClosesConnectionBeforePayloadRead(int length)
    {
        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, length);
        var stream = new MemoryStream(header);
        await using var connection = new FramedConnection(stream);
        await Assert.ThrowsAsync<InvalidDataException>(() => connection.ReadAsync().AsTask());
        Assert.False(stream.CanRead);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public async Task TruncatedFrameClosesConnection(int bytes)
    {
        var data = new byte[bytes];
        if (bytes >= 4) BinaryPrimitives.WriteInt32LittleEndian(data, 100);
        var stream = new MemoryStream(data);
        await using var connection = new FramedConnection(stream);
        await Assert.ThrowsAsync<EndOfStreamException>(() => connection.ReadAsync().AsTask());
        Assert.False(stream.CanRead);
    }
}
