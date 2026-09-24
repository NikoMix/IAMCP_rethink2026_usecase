using System.Text.Json;
using System.Text.RegularExpressions;

namespace ProposalGenerator.Validation.RateCards;

/// <summary>A rate card: daily rates per role in one currency, valid for a closed date interval.</summary>
/// <param name="Id">Rate card identifier, for example <c>RC-2026</c>.</param>
/// <param name="Currency">ISO 4217 currency of all rates.</param>
/// <param name="ValidFrom">First day of validity (inclusive).</param>
/// <param name="ValidTo">Last day of validity (inclusive).</param>
/// <param name="Roles">Roles with their daily rates.</param>
/// <param name="Source">Where the rate card was loaded from, for example <c>rate-card.json</c>; used in messages.</param>
public sealed record RateCard(
    string Id,
    string Currency,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    IReadOnlyList<RateCardRole> Roles,
    string Source)
{
    /// <summary>True when <paramref name="date"/> lies within the validity interval, bounds included.</summary>
    public bool IsValidOn(DateOnly date) => ValidFrom <= date && date <= ValidTo;

    /// <summary>Finds a role by ID, name or alias, ignoring case and surrounding or repeated whitespace.</summary>
    public RateCardRole? FindRole(string name)
    {
        var key = RateCardRole.Normalize(name);
        return Roles.FirstOrDefault(role => role.Keys.Contains(key, StringComparer.Ordinal));
    }
}

/// <summary>A role with its daily rate.</summary>
/// <param name="Id">Stable role ID, for example <c>solution-architect</c>.</param>
/// <param name="Role">Display name, for example <c>Solution Architect</c>.</param>
/// <param name="Aliases">Alternative names that refer to the same role.</param>
/// <param name="DailyRate">Net daily rate in the rate card currency.</param>
public sealed record RateCardRole(string Id, string Role, IReadOnlyList<string> Aliases, decimal DailyRate)
{
    internal IEnumerable<string> Keys => new[] { Id, Role }.Concat(Aliases).Select(Normalize);

    internal static string Normalize(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
}

/// <summary>Supplies the rate cards the plausibility check compares daily rates with.</summary>
public interface IRateCardProvider
{
    /// <summary>Returns all known rate cards, including expired ones.</summary>
    ValueTask<IReadOnlyList<RateCard>> GetRateCardsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Rate card provider over a fixed list, for example rate cards loaded at startup.</summary>
public sealed class StaticRateCardProvider(IEnumerable<RateCard> rateCards) : IRateCardProvider
{
    private readonly IReadOnlyList<RateCard> _rateCards = rateCards.ToArray();

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<RateCard>> GetRateCardsAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(_rateCards);
}

/// <summary>
/// Loads rate cards in the format of <c>knowledge/rate-card.json</c> and rejects inconsistent content
/// with an explicit message instead of loading a partial rate card.
/// </summary>
public static partial class RateCardLoader
{
    /// <summary>Loads and validates a rate card file.</summary>
    /// <exception cref="InvalidDataException">The file content is not a valid rate card.</exception>
    public static async Task<RateCard> LoadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Parse(json, Path.GetFileName(path));
    }

    /// <summary>Parses and validates rate card JSON.</summary>
    /// <param name="json">Rate card JSON.</param>
    /// <param name="source">Name used in messages, for example the file name.</param>
    /// <exception cref="InvalidDataException">The content is not a valid rate card.</exception>
    public static RateCard Parse(string json, string source)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Rate card '{source}' is not valid JSON: {exception.Message}", exception);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw Invalid(source, "the root must be a JSON object");
            }

            var id = RequiredText(root, "rate_card_id", source);
            var currency = RequiredText(root, "currency", source);
            if (!CurrencyCode().IsMatch(currency))
            {
                throw Invalid(source, $"currency '{currency}' is not an ISO 4217 alphabetic code");
            }

            var validFrom = RequiredDate(root, "valid_from", source);
            var validTo = RequiredDate(root, "valid_to", source);
            if (validTo < validFrom)
            {
                throw Invalid(source, $"valid_to {validTo:yyyy-MM-dd} is before valid_from {validFrom:yyyy-MM-dd}");
            }

            if (!root.TryGetProperty("roles", out var rolesElement) || rolesElement.ValueKind != JsonValueKind.Array || rolesElement.GetArrayLength() == 0)
            {
                throw Invalid(source, "'roles' must be a non-empty array");
            }

            var roles = new List<RateCardRole>();
            var index = 0;
            foreach (var roleElement in rolesElement.EnumerateArray())
            {
                var where = $"roles[{index}]";
                if (roleElement.ValueKind != JsonValueKind.Object)
                {
                    throw Invalid(source, $"{where} must be an object");
                }

                var roleId = RequiredText(roleElement, "id", source, where);
                var roleName = RequiredText(roleElement, "role", source, where);
                if (!roleElement.TryGetProperty("daily_rate", out var rateElement) || rateElement.ValueKind != JsonValueKind.Number || !rateElement.TryGetDecimal(out var rate) || rate <= 0)
                {
                    throw Invalid(source, $"{where}.daily_rate must be a positive number");
                }

                var aliases = new List<string>();
                if (roleElement.TryGetProperty("aliases", out var aliasesElement))
                {
                    if (aliasesElement.ValueKind != JsonValueKind.Array || aliasesElement.EnumerateArray().Any(a => a.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(a.GetString())))
                    {
                        throw Invalid(source, $"{where}.aliases must be an array of non-empty strings");
                    }

                    aliases.AddRange(aliasesElement.EnumerateArray().Select(a => a.GetString()!.Trim()));
                }

                roles.Add(new RateCardRole(roleId, roleName, aliases, rate));
                index++;
            }

            var duplicate = roles
                .SelectMany(role => role.Keys.Distinct().Select(key => (key, role.Id)))
                .GroupBy(entry => entry.key, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count() > 1);
            if (duplicate is not null)
            {
                throw Invalid(source, $"the name '{duplicate.Key}' is used by more than one role ({string.Join(", ", duplicate.Select(entry => entry.Id).Distinct())})");
            }

            return new RateCard(id, currency, validFrom, validTo, roles, source);
        }
    }

    private static string RequiredText(JsonElement element, string name, string source, string? where = null)
    {
        if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return value.GetString()!.Trim();
        }

        throw Invalid(source, $"{(where is null ? string.Empty : where + ".")}{name} must be a non-empty string");
    }

    private static DateOnly RequiredDate(JsonElement element, string name, string source)
    {
        if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date))
        {
            return date;
        }

        throw Invalid(source, $"{name} must be a date in the format YYYY-MM-DD");
    }

    private static InvalidDataException Invalid(string source, string reason) =>
        new($"Rate card '{source}' is invalid: {reason}.");

    [GeneratedRegex("^[A-Z]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex CurrencyCode();
}
