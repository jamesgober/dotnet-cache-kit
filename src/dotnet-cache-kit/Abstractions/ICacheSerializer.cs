namespace JG.CacheKit.Abstractions;

/// <summary>
/// Represents a serializer used to encode cache values.
/// </summary>
public interface ICacheSerializer
{
    /// <summary>
    /// Serializes a value into a byte array.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <returns>A UTF-8 encoded byte array representing the serialized value.</returns>
    byte[] Serialize<T>(T value);

    /// <summary>
    /// Deserializes a value from a byte payload.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="payload">The serialized payload.</param>
    /// <returns>The deserialized value, or <c>default</c> if the payload represents a null value.</returns>
    T? Deserialize<T>(ReadOnlyMemory<byte> payload);
}
