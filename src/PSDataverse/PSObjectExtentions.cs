using System;
using System.Collections;
using System.Management.Automation;

namespace PSDataverse;
public static class PSObjectExtentions
{

    internal static string GetPSPropertyInfoValue(this PSPropertyInfo property)
    {
        if (property == null)
        { throw new ArgumentNullException(nameof(property)); }

        try
        {
            return property.Value?.ToString();
        }
        catch (Exception)
        {
            // If we cannot read some value, treat it as null.
        }

        return null;
    }

    internal static string TryGetPropertyValue(this PSObject inputObject, string propertyName)
    {
        if (inputObject.BaseObject is IDictionary dictionary)
        {
            if (dictionary.Contains(propertyName))
            {
                return dictionary[propertyName].ToString();
            }
            else if (inputObject.Properties[propertyName] is PSPropertyInfo property)
            {
                return GetPSPropertyInfoValue(property);
            }
        }
        else if (inputObject.Properties[propertyName] is PSPropertyInfo property)
        {
            return GetPSPropertyInfoValue(property);
        }
        return null;
    }
}
