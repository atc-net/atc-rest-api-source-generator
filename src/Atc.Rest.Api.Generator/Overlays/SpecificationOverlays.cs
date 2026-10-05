namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// Applies a specification's overlays, in order, before it is parsed - the step the source generators and the CLI
/// share, so both read the same effective specification.
/// </summary>
public static class SpecificationOverlays
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>
    /// Applies overlays to a specification's text.
    /// </summary>
    /// <param name="specificationContent">The specification (YAML or JSON).</param>
    /// <param name="overlays">The overlay files, in the order they are applied.</param>
    /// <returns>The effective specification as JSON text (<see langword="null"/> when an overlay has an error), and what was reported per overlay file.</returns>
    public static (string? Content, IReadOnlyList<OverlayFileProblem> Problems) Apply(
        string specificationContent,
        IReadOnlyList<OverlayFile> overlays)
    {
        if (specificationContent is null)
        {
            throw new ArgumentNullException(nameof(specificationContent));
        }

        if (overlays is null)
        {
            throw new ArgumentNullException(nameof(overlays));
        }

        JsonNode? document;
        try
        {
            document = OverlayDocumentReader.Read(specificationContent);
        }
        catch (FormatException)
        {
            // Not a document the overlays can work on: the parser reports it as it would without overlays
            return (specificationContent, []);
        }

        var problems = new List<OverlayFileProblem>();
        foreach (var overlay in overlays)
        {
            JsonNode? overlayNode;
            try
            {
                overlayNode = OverlayDocumentReader.Read(overlay.Content);
            }
            catch (FormatException ex)
            {
                problems.Add(new OverlayFileProblem(overlay.FilePath, new OverlayProblem(IsError: true, ActionIndex: null, $"It is no valid YAML or JSON: {ex.Message}")));
                return (null, problems);
            }

            var application = OverlayApplier.Apply(document, overlayNode);
            problems.AddRange(application.Problems.Select(p => new OverlayFileProblem(overlay.FilePath, p)));
            if (application.HasErrors)
            {
                return (null, problems);
            }

            document = application.Document;
        }

        return (document?.ToJsonString(Indented) ?? string.Empty, problems);
    }

    /// <summary>
    /// Applies overlays to a merged multi-part specification.
    /// </summary>
    /// <param name="document">The merged document.</param>
    /// <param name="overlays">The overlay files, in the order they are applied.</param>
    /// <returns>The effective specification as JSON text (<see langword="null"/> when an overlay has an error), and what was reported per overlay file.</returns>
    public static (string? Content, IReadOnlyList<OverlayFileProblem> Problems) Apply(
        OpenApiDocument document,
        IReadOnlyList<OverlayFile> overlays)
        => Apply(Services.SpecificationService.SerializeToJson(document ?? throw new ArgumentNullException(nameof(document))), overlays);
}