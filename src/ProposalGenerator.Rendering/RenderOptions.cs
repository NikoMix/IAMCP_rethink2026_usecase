namespace ProposalGenerator.Rendering;

/// <summary>
/// Options for a single render call.
/// </summary>
/// <remarks>
/// This type is the extension point for per-call settings that later features add, such as the
/// output locale (#27), file naming and document properties (#28), and draft watermarks (#29).
/// Add new settings as init-only properties with backward-compatible defaults.
/// </remarks>
public sealed record RenderOptions
{
    /// <summary>The default options.</summary>
    public static RenderOptions Default { get; } = new();

    /// <summary>
    /// When <see langword="true"/> (the default), the renderer validates the produced package with
    /// the Open XML SDK validator and throws <see cref="DocxValidationException"/> instead of
    /// returning a document that Word might have to repair.
    /// </summary>
    public bool ValidateOutput { get; init; } = true;
}
