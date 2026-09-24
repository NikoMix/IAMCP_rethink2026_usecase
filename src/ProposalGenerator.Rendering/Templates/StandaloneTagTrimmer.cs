using System.Text;
using System.Text.RegularExpressions;

namespace ProposalGenerator.Rendering.Templates;

/// <summary>
/// Removes the line break after template lines that contain only block tags (<c>{% ... %}</c>),
/// like Jinja's <c>trim_blocks</c> and <c>lstrip_blocks</c>. Without this, every loop or condition
/// on its own line leaves a blank line in the output, which ends a Markdown table or list early.
/// </summary>
/// <remarks>
/// Scriban's Liquid mode offers only greedy whitespace control (<c>{%- -%}</c>), which also removes
/// blank lines that separate Markdown blocks, so the templates keep plain tags and this pass
/// handles standalone lines. It records where each output line came from so that parser errors
/// still point at the original template line.
/// </remarks>
internal static partial class StandaloneTagTrimmer
{
    [GeneratedRegex(@"^[ \t]*(\{%(?:(?!%\}).)*%\}[ \t]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex StandaloneTagLine();

    public static TrimResult Trim(string text)
    {
        var lines = text.Split('\n');
        var output = new StringBuilder(text.Length);
        var map = new List<List<LineSegment>>();
        var current = new List<LineSegment>();
        var column = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var content = line.EndsWith('\r') ? line[..^1] : line;
            var isLast = i == lines.Length - 1;
            var standalone = !isLast && !content.Contains("{{", StringComparison.Ordinal) && StandaloneTagLine().IsMatch(content);

            if (standalone)
            {
                var trimmed = content.TrimStart(' ', '\t');
                current.Add(new LineSegment(column, i, content.Length - trimmed.Length));
                output.Append(trimmed);
                column += trimmed.Length;
                continue;
            }

            current.Add(new LineSegment(column, i, 0));
            output.Append(line);
            if (!isLast)
            {
                output.Append('\n');
                map.Add(current);
                current = [];
                column = 0;
            }
        }

        map.Add(current);
        return new TrimResult(output.ToString(), map);
    }

    /// <summary>A run of text in an output line that came from one original line.</summary>
    /// <param name="OutputColumn">0-based column in the output line where the run starts.</param>
    /// <param name="OriginalLine">0-based original line.</param>
    /// <param name="OriginalColumnOffset">Columns removed from the start of the original line.</param>
    internal readonly record struct LineSegment(int OutputColumn, int OriginalLine, int OriginalColumnOffset);

    internal sealed record TrimResult(string Text, IReadOnlyList<IReadOnlyList<LineSegment>> Map)
    {
        /// <summary>Maps a 0-based output position to a 0-based original line and column.</summary>
        public (int Line, int Column) ToOriginal(int line, int column)
        {
            if (line < 0 || line >= Map.Count)
            {
                return (line, column);
            }

            var segments = Map[line];
            var segment = segments[0];
            foreach (var candidate in segments)
            {
                if (candidate.OutputColumn <= column)
                {
                    segment = candidate;
                }
            }

            return (segment.OriginalLine, column - segment.OutputColumn + segment.OriginalColumnOffset);
        }
    }
}
