using Scriban.Syntax;

namespace ProposalGenerator.Rendering.Templates;

/// <summary>
/// Rejects template constructs outside the supported template subset. Scriban's Liquid parser
/// accepts far more than the templates may use, and several of those constructs (filters,
/// loop variables such as <c>forloop.index</c>) either fail only at render time or render as an
/// empty string. Checking the syntax tree up front turns them into explicit errors.
/// </summary>
/// <remarks>
/// The supported subset (interim contract until the template-syntax ADR, #2):
/// <list type="bullet">
/// <item><c>{{ a.b.c }}</c> — output a value addressed by a dotted path.</item>
/// <item><c>{% for x in a.list %}...{% endfor %}</c> — without parameters such as <c>limit:</c>.</item>
/// <item><c>{% if cond %}...{% else %}...{% endif %}</c> — no <c>elsif</c>.</item>
/// <item>Conditions: a path, a string/number/boolean literal, <c>==</c>, <c>!=</c>, <c>and</c>, <c>or</c>.</item>
/// </list>
/// Filters (<c>| default</c>), function calls, loop variables, assignments and every other
/// statement are rejected. Extend this allow-list when the ADR widens the subset.
/// </remarks>
internal static class TemplateSubsetValidator
{
    private static readonly HashSet<string> LoopVariableNames = new(StringComparer.Ordinal) { "for", "forloop", "loop", "while", "tablerowloop" };

    private static readonly HashSet<ScriptBinaryOperator> AllowedOperators =
    [
        ScriptBinaryOperator.CompareEqual,
        ScriptBinaryOperator.CompareNotEqual,
        ScriptBinaryOperator.And,
        ScriptBinaryOperator.Or,
    ];

    /// <summary>Returns every violation as a 0-based position and message.</summary>
    public static IReadOnlyList<(int Line, int Column, string Message)> Validate(ScriptPage page)
    {
        var violations = new List<(int, int, string)>();
        Visit(page, violations);
        return violations;
    }

    private static void Visit(ScriptNode node, List<(int, int, string)> violations)
    {
        var problem = Check(node);
        if (problem is not null)
        {
            violations.Add((node.Span.Start.Line, node.Span.Start.Column, problem));
            return;
        }

        for (var i = 0; i < node.ChildrenCount; i++)
        {
            var child = node.GetChildren(i);
            if (child is not null)
            {
                Visit(child, violations);
            }
        }
    }

    private static string? Check(ScriptNode node) => node switch
    {
        ScriptPage or ScriptBlockStatement or ScriptRawStatement or ScriptEscapeStatement or
            ScriptExpressionStatement or ScriptElseStatement or ScriptEndStatement or
            ScriptKeyword or ScriptToken or ScriptMemberExpression or ScriptLiteral or ScriptNestedExpression => null,
        ScriptForStatement => null,
        ScriptIfStatement when node.Parent is ScriptIfStatement => "'elsif' is not supported; nest an 'if' inside 'else' instead.",
        ScriptIfStatement => null,
        ScriptVariable variable when LoopVariableNames.Contains(variable.Name) =>
            $"Loop variable '{(variable.Name == "for" ? "forloop" : variable.Name)}' is not supported.",
        ScriptVariable => null,
        ScriptBinaryExpression binary when AllowedOperators.Contains(binary.Operator) => null,
        ScriptBinaryExpression binary => $"Operator '{binary.Operator}' is not supported; use ==, !=, and, or.",
        ScriptPipeCall => "Filters ('|') are not supported; supply the final value in the data instead.",
        ScriptFunctionCall => "Function calls are not supported.",
        _ when IsScriptList(node) => null,
        _ => $"Construct '{node.GetType().Name}' is not supported.",
    };

    private static bool IsScriptList(ScriptNode node)
    {
        var type = node.GetType();
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ScriptList<>);
    }
}
