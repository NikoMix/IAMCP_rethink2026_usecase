using System.Globalization;
using System.Text;

namespace ProposalGenerator.Agent.StructuredOutput;

/// <summary>
/// Converts the paths used in <c>check_plausibility</c> output into JSON Pointers relative to the document.
/// Accepts JSON Pointer (<c>/pricing/total</c>) and simple JSONPath (<c>$.pricing.rate_card[0].subtotal</c>,
/// <c>pricing.total</c>).
/// </summary>
internal static class DocumentPath
{
    public static bool TryParse(string path, out IReadOnlyList<string> segments)
    {
        segments = [];
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.StartsWith('/'))
        {
            segments = path.Split('/').Skip(1).Select(s => s.Replace("~1", "/").Replace("~0", "~")).ToArray();
            return segments.All(s => s.Length > 0);
        }

        var text = path.StartsWith("$.", StringComparison.Ordinal) ? path[2..] : path;
        var result = new List<string>();
        var current = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '.')
            {
                if (current.Length == 0 && (i == 0 || text[i - 1] != ']'))
                {
                    return false;
                }

                Flush();
            }
            else if (c == '[')
            {
                Flush();
                var end = text.IndexOf(']', i);
                if (end < 0 || !int.TryParse(text.AsSpan(i + 1, end - i - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                {
                    return false;
                }

                result.Add(index.ToString(CultureInfo.InvariantCulture));
                i = end;
            }
            else
            {
                current.Append(c);
            }
        }

        Flush();
        segments = result;
        return result.Count > 0;

        void Flush()
        {
            if (current.Length > 0)
            {
                result.Add(current.ToString());
                current.Clear();
            }
        }
    }

    public static string ToPointer(IEnumerable<string> segments) =>
        string.Concat(segments.Select(s => "/" + s.Replace("~", "~0").Replace("/", "~1")));
}
