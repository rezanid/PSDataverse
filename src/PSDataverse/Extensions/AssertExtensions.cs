namespace PSDataverse.Extensions;

using System;
using System.Globalization;

/// <summary>
/// Groups useful extension methods used for validation.
/// </summary>
public static class AssertExtensions
{
    public static void AssertArgumentNotNull(this object argument, string argumentName)
    {
        if (argument == null)
        { throw new ArgumentNullException(argumentName); }
    }

    public static void AssertArgumentNotEmpty(this string argument, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, Resources.AssertStringNotEmptyInvalidError, argumentName ?? Resources.AssertStringNotEmptyInvalidPrefix));
        }
    }

    public static void AssertPositive(this int argument, string argumentName)
    {
        if (argument <= 0)
        {
            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, Resources.AssertNumberPositiveInvalidError, argumentName ?? Resources.AssertNumberPositiveInvalidPrefix));
        }
    }

    public static void AssertPositive(this decimal argument, string argumentName)
    {
        if (argument <= 0)
        {
            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, Resources.AssertNumberPositiveInvalidError, argumentName ?? Resources.AssertNumberPositiveInvalidPrefix));
        }
    }
}
