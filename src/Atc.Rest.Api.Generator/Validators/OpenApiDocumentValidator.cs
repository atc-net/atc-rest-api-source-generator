// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable CommentTypo
// ReSharper disable InvertIf
// ReSharper disable LoopCanBeConvertedToQuery
namespace Atc.Rest.Api.Generator.Validators;

/// <summary>
/// Validates OpenAPI documents according to the configured strategy.
/// Returns DiagnosticMessage objects (Roslyn-free) instead of Roslyn Diagnostic objects.
/// </summary>
public static partial class OpenApiDocumentValidator
{
    /// <summary>
    /// Validates an OpenAPI document according to the specified strategy.
    /// </summary>
    /// <param name="strategy">The validation strategy to use.</param>
    /// <param name="document">The OpenAPI document to validate.</param>
    /// <param name="diagnosticErrors">Diagnostic errors from parsing (used in Standard validation).</param>
    /// <param name="sourceFilePath">Path to the source OpenAPI file for error reporting.</param>
    /// <param name="sourceText">
    /// The raw specification text, when the caller has it. Some rules cannot be expressed against the
    /// parsed document because the parser discards the construct they are about — <c>nullable</c> on a
    /// schema with no <c>type</c> is consumed and dropped in an OpenAPI 3.0 document, reaching neither
    /// the type flags nor <c>UnrecognizedKeywords</c>. Those rules read the text instead, which also
    /// lets them report a line number.
    /// </param>
    /// <param name="diagnosticWarnings">Diagnostic warnings from parsing (reported under Standard and Strict).</param>
    /// <returns>List of diagnostic messages to report.</returns>
    public static List<DiagnosticMessage> Validate(
        ValidateSpecificationStrategy strategy,
        OpenApiDocument document,
        IList<OpenApiError> diagnosticErrors,
        string sourceFilePath,
        string? sourceText = null,
        IList<OpenApiError>? diagnosticWarnings = null)
    {
        // A value of another kind where an object belongs (the reader leaves a null there, without an error): an
        // error under every strategy, and nothing else is checked - every later step would trip over the null
        var structure = OpenApiStructureValidator.Validate(document, sourceFilePath);
        if (structure.Count > 0)
        {
            return structure;
        }

        // A schema that contains itself (allOf, a bare $ref, an array alias's items): an error under every strategy,
        // and nothing else is checked - the later checks and the extractors inline the cycle without end
        var cycles = SchemaCycleValidator.Validate(document, sourceFilePath);
        if (cycles.Count > 0)
        {
            return cycles;
        }

        var diagnostics = new List<DiagnosticMessage>();

        // Two schema names that become one C# type (User / user, Foo-Bar / Foo_Bar) break generation under every
        // strategy - the generator fails on the duplicate file - so the collision is always an error
        diagnostics.AddRange(ValidateSchemaNameCollisions(document, sourceFilePath));
        diagnostics.AddRange(ValidateOperationIdCollisions(document, sourceFilePath));
        diagnostics.AddRange(ValidateSecuritySchemeReferences(document, sourceFilePath));

        // What the generated code would name and not find: a $ref to a component that is not there, a parameter
        // with no type, a schema with no type but null; and a document with nothing to generate from
        diagnostics.AddRange(DanglingReferenceValidator.Validate(document, sourceFilePath));
        diagnostics.AddRange(SignalRHubValidator.Validate(document, sourceFilePath));
        diagnostics.AddRange(ValidateParameterSchemas(document, sourceFilePath));
        diagnostics.AddRange(ValidateNullOnlyTypes(document, sourceFilePath));
        diagnostics.AddRange(SchemaConstraintValidator.Validate(document, sourceFilePath));
        diagnostics.AddRange(ValidateNoOperations(document, sourceFilePath));

        // A path template the route or the generated code could not carry, a placeholder no parameter declares, and
        // a response for a status range (generated as its first status)
        diagnostics.AddRange(ValidatePaths(document, sourceFilePath));
        diagnostics.AddRange(ValidatePathsTheReaderDropped(diagnosticErrors, sourceFilePath));
        diagnostics.AddRange(ValidateResponseRanges(document, sourceFilePath));
        diagnostics.AddRange(ValidateHeadResponses(document, sourceFilePath));
        diagnostics.AddRange(MediaTypeNormalizer.DroppedDeclarations(document).Select(dropped => new DiagnosticMessage(
            RuleIdentifiers.MediaTypeDeclaredTwice,
            $"{char.ToUpperInvariant(dropped.Where[0])}{dropped.Where.Substring(1)} declares the media type '{dropped.Dropped}', which is the same media type as '{dropped.Kept}' declared before it, with another schema: '{dropped.Kept}' is generated, '{dropped.Dropped}' is not. Declare the media type once.",
            DiagnosticSeverity.Warning,
            sourceFilePath)));

        // A querystring parameter the generated code cannot carry beside another (OAS 3.2 forbids it), and a parameter
        // whose value it cannot bind or send at all
        diagnostics.AddRange(ValidateQueryStringParameters(document, sourceFilePath));
        diagnostics.AddRange(ValidateParameterValues(document, sourceFilePath));

        if (strategy == ValidateSpecificationStrategy.None)
        {
            return SpecSuppressions.Apply(document, diagnostics); // Skip all validation
        }

        // Standard validation: Report Microsoft.OpenApi parsing errors + schema reference validation
        if (strategy >= ValidateSpecificationStrategy.Standard)
        {
            diagnostics.AddRange(ValidateStandard(strategy, diagnosticErrors, document, sourceFilePath));

            // The reader's warnings, but for the kinds a rule of its own reports as an error with its place
            foreach (var warning in diagnosticWarnings ?? [])
            {
                if (warning.Message.Contains("does not point to an existing", StringComparison.Ordinal) ||
                    warning.Message.StartsWith("Circular reference detected", StringComparison.Ordinal))
                {
                    continue;
                }

                diagnostics.Add(DiagnosticBuilder.ParsingWarning(warning.Message, warning.Pointer, sourceFilePath));
            }
        }

        // Strict validation: Standard + custom ATC rules
        if (strategy == ValidateSpecificationStrategy.Strict)
        {
            diagnostics.AddRange(ValidateStrict(document, sourceFilePath));

            // ATC_API_VER0xx: constructs that do not mean what the author thinks at the declared
            // spec version, or that must change when the version is raised.
            SpecVersionMigrationValidator.Validate(diagnostics, sourceFilePath, document, sourceText);
        }

        // A diagnostic found at an operation, a path or a schema is put on its line (Microsoft.OpenApi keeps none)
        var result = SpecSuppressions.Apply(document, diagnostics);
        if (!string.IsNullOrEmpty(sourceText))
        {
            SpecLocations.Apply(result, [(sourceFilePath, sourceText!)]);
        }

        return result;
    }

    /// <summary>
    /// Marks the diagnostics added since <paramref name="from"/> as found at <paramref name="scope"/>, where
    /// <c>x-atc-suppress</c> can switch their rules off (see <see cref="SpecSuppressions"/>).
    /// </summary>
    private static void InScope(
        List<DiagnosticMessage> diagnostics,
        int from,
        string scope)
    {
        for (var i = from; i < diagnostics.Count; i++)
        {
            if (diagnostics[i].Scope is null)
            {
                diagnostics[i] = diagnostics[i] with { Scope = scope };
            }
        }
    }

    private static List<DiagnosticMessage> ValidateStandard(
        ValidateSpecificationStrategy strategy,
        IList<OpenApiError> diagnosticErrors,
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (diagnosticErrors.Count > 0)
        {
            var isOpenApi32 = document.SpecVersion == OpenApiSpecVersion.OpenApi3_2;

            foreach (var error in diagnosticErrors)
            {
                // In OpenAPI 3.2, discriminator.propertyName is optional.
                // Suppress the parser's 3.0/3.1 rule "discriminator property must be in required"
                // when the discriminator at that path genuinely has no propertyName.
                if (isOpenApi32 &&
                    error.Pointer is not null &&
                    error.Pointer.EndsWith("/discriminator", StringComparison.Ordinal) &&
                    error.Message.Contains("discriminator", StringComparison.OrdinalIgnoreCase) &&
                    error.Message.Contains("required", StringComparison.OrdinalIgnoreCase) &&
                    IsDiscriminatorWithoutPropertyName(document, error.Pointer))
                {
                    continue;
                }

                // A path without its leading '/': reported as ATC_API_PTH001 under every strategy
                if (DroppedPath(error) is not null)
                {
                    continue;
                }

                diagnostics.Add(DiagnosticBuilder.ParsingError(
                    error.Message,
                    error.Pointer,
                    sourceFilePath));
            }
        }

        // ATC_API_SCH019: Warn on anonymous inline schema in components.mediaTypes
        ValidateComponentsMediaTypes(diagnostics, sourceFilePath, document);

        // ATC_API_SCH020: Warn when discriminator block lacks propertyName and auto-detect fails
        ValidateDiscriminatorPropertyNames(diagnostics, sourceFilePath, document);

        // ATC_API_SEC011: Info when a mutualTLS security scheme is declared
        ValidateMutualTlsSchemes(diagnostics, sourceFilePath, document);

        // ATC_API_STREAM001: Info when a streaming media type has unsupported prefixEncoding
        ValidateStreamingEncodings(diagnostics, sourceFilePath, document);

        // ATC_API_STREAM002 / ATC_API_STREAM003: a request body sent as a stream that would be dropped
        ValidateStreamedRequestBodies(diagnostics, sourceFilePath, document);

        // ATC_API_RL001/RL002: Warn on rate-limit partitioning that is silently ignored
        ValidateRateLimitPartitioning(diagnostics, sourceFilePath, document);

        // ATC_API_RL003: Warn when one policy name is declared with conflicting settings
        ValidateRateLimitPolicyConflicts(diagnostics, sourceFilePath, document);

        // ATC_API_RL004: Error when distinct policy names collide on the generated constant
        ValidateRateLimitPolicyNameCollisions(diagnostics, sourceFilePath, document);

        // ATC_API_RL005/RL006/RL008: Warn on values and placements the runtime rejects or ignores
        ValidateRateLimitValuesAndPlacement(diagnostics, sourceFilePath, document);

        // ATC_API_RL007: Info when the algorithm cannot supply a Retry-After value
        ValidateRateLimitRetryAfterSupport(diagnostics, sourceFilePath, document);

        // ATC_API_RTY001: Warn on an x-retry-* setting no generated policy reads
        diagnostics.AddRange(RetrySettingValidator.Validate(document, sourceFilePath));

        // ATC_API_OPR001: Warn when operationId is missing (Standard only — Strict mode reports it as an Error per-operation)
        if (strategy == ValidateSpecificationStrategy.Standard)
        {
            diagnostics.AddRange(ValidateMissingOperationIds(document, sourceFilePath));
        }

        return diagnostics;
    }

    /// <summary>
    /// Validates using custom ATC rules (Strict level).
    /// </summary>
    private static List<DiagnosticMessage> ValidateStrict(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        // Check OpenAPI version (must be 3.0.x)
        diagnostics.AddRange(ValidateOpenApiVersion(document, sourceFilePath));

        // Naming convention validations
        diagnostics.AddRange(ValidateNamingConventions(document, sourceFilePath));

        // Security validations
        diagnostics.AddRange(ValidateSecurityConfiguration(document, sourceFilePath));

        // Schema validations
        diagnostics.AddRange(ValidateSchemas(document, sourceFilePath));

        // Operation validations
        diagnostics.AddRange(ValidateOperations(document, sourceFilePath));

        // Server validations
        diagnostics.AddRange(ValidateServers(document, sourceFilePath));

        // Webhook validations (OpenAPI 3.1)
        diagnostics.AddRange(ValidateWebhooks(document, sourceFilePath));

        return diagnostics;
    }

    /// <summary>
    /// Validates the OpenAPI specification version (ATC_API_VAL002: OpenAPI 2.0 not supported).
    /// Recognizes 3.0 / 3.1 / 3.2 as supported and rejects Swagger/OpenAPI 2.0.
    /// The spec version is read from the parsed document's metadata (see
    /// <c>OpenApiDocument.SpecVersion</c>), not from <c>info.version</c>
    /// (which is the API's own semantic version).
    /// </summary>
    private static List<DiagnosticMessage> ValidateOpenApiVersion(
        OpenApiDocument document,
        string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticMessage>();

        if (document.SpecVersion == OpenApiSpecVersion.OpenApi2_0)
        {
            diagnostics.Add(new DiagnosticMessage(
                RuleIdentifiers.OpenApi20NotSupported,
                "OpenAPI 2.0 (Swagger) is not supported. Please use OpenAPI 3.0.x, 3.1.x or 3.2.x.",
                DiagnosticSeverity.Error,
                sourceFilePath));
        }

        return diagnostics;
    }
}