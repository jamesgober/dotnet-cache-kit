using System.Buffers.Binary;
using JG.CacheKit.Abstractions;

namespace JG.CacheKit.Internal;

internal static class CacheEnvelopeSerializer
{
    private const int HeaderSize = 8 + 8 + 8 + 8 + 4;

    public static byte[] Serialize(CacheEntryMetadata metadata, ReadOnlyMemory<byte> payload)
    {
        var payloadLength = payload.Length;
        var buffer = new byte[HeaderSize + payloadLength];
        var span = buffer.AsSpan();

        BinaryPrimitives.WriteInt64LittleEndian(span.Slice(0, 8), metadata.CreatedUtcTicks);
        BinaryPrimitives.WriteInt64LittleEndian(span.Slice(8, 8), metadata.AbsoluteExpirationUtcTicks);
        BinaryPrimitives.WriteInt64LittleEndian(span.Slice(16, 8), metadata.SlidingExpirationTicks);
        BinaryPrimitives.WriteInt64LittleEndian(span.Slice(24, 8), metadata.StaleTtlTicks);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(32, 4), payloadLength);

        if (payloadLength > 0)
        {
            payload.Span.CopyTo(span.Slice(HeaderSize));
        }

        return buffer;
    }

    public static bool TryDeserialize(ReadOnlyMemory<byte> data, out CacheEntry entry)
    {
        entry = null!;
        if (data.Length < HeaderSize)
        {
            return false;
        }

        var span = data.Span;
        var createdTicks = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(0, 8));
        var absoluteTicks = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(8, 8));
        var slidingTicks = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(16, 8));
        var staleTicks = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(24, 8));
        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(32, 4));

        if (payloadLength < 0 || HeaderSize + payloadLength > data.Length)
        {
            return false;
        }

        var payload = data.Slice(HeaderSize, payloadLength);
        var metadata = new CacheEntryMetadata(createdTicks, absoluteTicks, slidingTicks, staleTicks);
        entry = new CacheEntry(payload, metadata);
        return true;
    }
}
