using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ProposalGenerator.Validation.Internal;

/// <summary>A JSON value together with its JSONPath relative to the document root.</summary>
internal readonly record struct Located(string Path, JsonElement Value);

internal static partial class JsonNavigator
{
    public const string RootPath = "$";

    /// <summary>Resolves a dot-separated path. Returns null when a segment is missing or the value is JSON null.</summary>
    public static Located? Find(JsonElement start, string startPath, string relativePath)
    {
        var current = start;
        var path = startPath;
        foreach (var segment in relativePath.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
            {
                return null;
            }

            current = next;
            path = AppendProperty(path, segment);
        }

        return current.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : new Located(path, current);
    }

    /// <summary>Resolves a path relative to an already located value.</summary>
    public static Located? Find(Located start, string relativePath) => Find(start.Value, start.Path, relativePath);

    /// <summary>Returns the items of the array at <paramref name="relativePath"/>, or nothing when it is absent or not an array.</summary>
    public static IReadOnlyList<Located> Items(JsonElement start, string startPath, string relativePath)
    {
        if (Find(start, startPath, relativePath) is not { } array || array.Value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var items = new List<Located>();
        var index = 0;
        foreach (var item in array.Value.EnumerateArray())
        {
            items.Add(new Located($"{array.Path}[{index}]", item));
            index++;
        }

        return items;
    }

    /// <summary>Enumerates every object property in the tree, depth first, in document order.</summary>
    public static IEnumerable<(Located Value, string Name)> Properties(JsonElement start, string startPath)
    {
        switch (start.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in start.EnumerateObject())
                {
                    var path = AppendProperty(startPath, property.Name);
                    yield return (new Located(path, property.Value), property.Name);
                    foreach (var nested in Properties(property.Value, path))
                    {
                        yield return nested;
                    }
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in start.EnumerateArray())
                {
                    foreach (var nested in Properties(item, $"{startPath}[{index}]"))
                    {
                        yield return nested;
                    }

                    index++;
                }

                break;
        }
    }

    private static string AppendProperty(string path, string name) =>
        SimpleName().IsMatch(name)
            ? $"{path}.{name}"
            : $"{path}['{name.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal)}']";

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SimpleName();
}

internal static partial class Values
{
    private static readonly NumberFormatInfo GermanNumbers = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        NumberGroupSizes = [3],
        NegativeSign = "-",
    };

    /// <summary>Reads a JSON number as decimal. Strings are not accepted, as in the schemas.</summary>
    public static bool TryGetNumber(Located? value, out decimal number)
    {
        number = 0;
        return value is { Value.ValueKind: JsonValueKind.Number } located && located.Value.TryGetDecimal(out number);
    }

    /// <summary>Reads a <c>YYYY-MM-DD</c> string as a calendar date.</summary>
    public static bool TryGetDate(Located? value, out DateOnly date)
    {
        date = default;
        return value is { Value.ValueKind: JsonValueKind.String } located
            && IsoDate().IsMatch(located.Value.GetString()!)
            && DateOnly.TryParseExact(located.Value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    /// <summary>Reads a non-empty string, trimmed.</summary>
    public static bool TryGetText(Located? value, out string text)
    {
        text = value is { Value.ValueKind: JsonValueKind.String } located ? located.Value.GetString()!.Trim() : string.Empty;
        return text.Length > 0;
    }

    /// <summary>Rounds a money amount to cents, commercially (away from zero).</summary>
    public static decimal Money(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    /// <summary>Formats an amount for German messages, for example <c>12.500,00</c>.</summary>
    public static string FormatAmount(decimal amount) => amount.ToString("#,##0.00", GermanNumbers);

    /// <summary>Formats a number for German messages without trailing zeros, for example <c>12,5</c>.</summary>
    public static string FormatNumber(decimal number) => number.ToString("#,##0.##########", GermanNumbers);

    /// <summary>Formats a date for German messages, for example <c>31.01.2026</c>.</summary>
    public static string FormatDate(DateOnly date) => date.ToString("dd'.'MM'.'yyyy", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex IsoDate();
}
