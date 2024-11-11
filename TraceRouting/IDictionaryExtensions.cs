using System.Collections.Generic;

namespace TraceRouting
{
    public static class IDictionaryExtensions
    {
        public static TValue GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        {
            if (key == null) return default;

            if (!dictionary.TryGetValue(key, out TValue result))
            {
                return default;
            }

            return result;
        }
    }
}
