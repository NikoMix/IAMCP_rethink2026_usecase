using System.Text.Json;

namespace ProposalGenerator.Rendering.Data;

/// <summary>Checks that document data supplies every required field path.</summary>
internal static class RequiredFieldValidator
{
    /// <summary>
    /// Returns every path in <paramref name="requiredPaths"/> that <paramref name="data"/> does not supply.
    /// A field is missing when it is absent, <c>null</c>, an empty or whitespace-only string, or an empty array.
    /// Paths are dotted object paths such as <c>supplier.address.street</c>.
    /// </summary>
    public static IReadOnlyList<MissingField> FindMissing(JsonElement data, IEnumerable<string> requiredPaths)
    {
        var missing = new List<MissingField>();
        foreach (var path in requiredPaths)
        {
            var reason = Check(data, path);
            if (reason is not null)
            {
                missing.Add(new MissingField(path, reason.Value));
            }
        }

        return missing;
    }

    private static MissingFieldReason? Check(JsonElement data, string path)
    {
        var current = data;
        foreach (var segment in path.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object)
            {
                return current.ValueKind == JsonValueKind.Null ? MissingFieldReason.Absent : MissingFieldReason.ParentNotObject;
            }

            if (!current.TryGetProperty(segment, out current))
            {
                return MissingFieldReason.Absent;
            }
        }

        return current.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => MissingFieldReason.Null,
            JsonValueKind.String when string.IsNullOrWhiteSpace(current.GetString()) => MissingFieldReason.Empty,
            JsonValueKind.Array when current.GetArrayLength() == 0 => MissingFieldReason.Empty,
            _ => null,
        };
    }
}
