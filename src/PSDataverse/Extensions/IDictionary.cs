namespace PSDataverse.Extensions;

using System.Collections;

public static class IDictionaryExtensions
{
    public static bool TryGetValue(this IDictionary dictionary, object key, out object value)
    {
        if (dictionary.Contains(key))
        {
            value = dictionary[key];
            return true;
        }
        value = null;
        return false;
    }
}
