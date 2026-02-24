namespace JG.CacheKit.Internal;

internal static class CacheKey
{
    public static void Validate(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key cannot be null or whitespace.", nameof(key));
        }
    }
}
