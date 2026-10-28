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
    /// <param name="includePaths">The marker's <c>includePaths</c>: the rules about paths and operations judge only the operations it keeps (the generated ones).</param>
    /// <param name="excludePaths">The marker's <c>excludePaths</c>.</param>
    /// <returns>List of diagnostic messages to report.</returns>
    public static List<DiagnosticMessage> Validate(
        ValidateSpecificationStrategy strategy,
        OpenApiDocument document,
        IList<OpenApiError> diagnosticErrors,
        string sourceFilePath,
        string? sourceText = null,
        IList<OpenApiError>? diagnosticWarnings = null,
        IList<string>? includePaths = null,
        IList<string>? excludePaths = null)
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

        // The document the generators generate from: only the paths the marker's includePaths / excludePaths keep. A rule
        // about a path or an operation judges these - one left out is never generated, and an error there must not stop
        // the rest. A rule about the document itself (its components, servers, webhooks, version) judges it whole: every
        // component schema is generated whatever the filter keeps.
        var generated = PathFilterHelper.WithFilteredPaths(document, includePaths, excludePaths);

        // Two schema names that become one C# type (User / user, Foo-Bar / Foo_Bar) break generation under every
        // strategy - the generator fails on the duplicate file - so the collision is always an error
        diagnostics.AddRange(ValidateSchemaNameCollisions(document, sourceFilePath));
        diagnostics.AddRange(ValidateOperationIdCollisions(generated, sourceFilePath));
        diagnostics.AddRange(ValidateSecuritySchemeReferences(generated, sourceFilePath));

        // What the generated code would name and not find: a $ref to a component that is not there, a parameter
        // with no type, a schema with no type but null; and a document with nothing to generate from
        // An OpenAPI 2.0 (Swagger) document generates nothing: reported whatever the strategy
        diagnostics.AddRange(ValidateOpenApiVersion(document, sourceFilePath));
        diagnostics.AddRange(DanglingReferenceValidator.Validate(generated, sourceFilePath));
        diagnostics.AddRange(SignalRHubValidator.Validate(document, sourceFilePath));
        diagnostics.AddRange(ValidateParameterSchemas(generated, sourceFilePath));
        diagnostics.AddRange(ValidateNullOnlyTypes(document, sourceFilePath));
        diagnostics.AddRange(SchemaConstraintValidator.Validate(generated, sourceFilePath));

        // A document without operations - not one whose operations the filter leaves out
        diagnostics.AddRange(ValidateNoOperations(document, sourceFilePath));

        // A required readOnly/writeOnly property the generated record makes optional, as its schema is used both ways in
        // the document the models are generated from
        diagnostics.AddRange(ReadOrWriteOnlyRequiredValidator.Validate(generated, sourceFilePath));

        // An object schema with properties but no required list: every property generated nullable
        diagnostics.AddRange(ValidateSchemasWithoutRequired(document, sourceFilePath));

        // A path template the route or the generated code could not carry, a placeholder no parameter declares, and
        // a response for a status range (generated as its first status)
        diagnostics.AddRange(ValidatePaths(generated, sourceFilePath));
        diagnostics.AddRange(ValidatePathsTheReaderDropped(diagnosticErrors, sourceFilePath, includePaths, excludePaths));
        diagnostics.AddRange(ValidateResponseRanges(generated, sourceFilePath));
        diagnostics.AddRange(ValidateHeadResponses(generated, sourceFilePath));

        // A media type declared twice (a document already rewritten carries what it dropped, for every operation: those
        // of the generated ones)
        diagnostics.AddRange(MediaTypeNormalizer.DroppedDeclarations(generated).Where(dropped => IsAtAKeptOperation(dropped.Scope, generated)).Select(dropped => new DiagnosticMessage(
            RuleIdentifiers.MediaTypeDeclaredTwice,
            $"{char.ToUpperInvariant(dropped.Where[0])}{dropped.Where.Substring(1)} declares the media type '{dropped.Dropped}', which is the same media type as '{dropped.Kept}' declared before it, with another schema: '{dropped.Kept}' is generated, '{dropped.Dropped}' is not. Declare the media type once.",
            DiagnosticSeverity.Warning,
            sourceFilePath,
            Scope: dropped.Scope)));

        // A querystring parameter the generated code cannot carry beside another (OAS 3.2 forbids it), and a parameter
        // whose value it cannot bind or send at all
        diagnostics.AddRange(ValidateQueryStringParameters(generated, sourceFilePath));
        diagnostics.AddRange(ValidateParameterValues(generated, sourceFilePath));

        if (strategy == ValidateSpecificationStrategy.None)
        {
            return Located(SpecSuppressions.Apply(document, diagnostics), sourceFilePath, sourceText); // Skip all validation
        }

        // Standard validation: Report Microsoft.OpenApi parsing errors + schema reference validation
        if (strategy >= ValidateSpecificationStrategy.Standard)
        {
            diagnostics.AddRange(ValidateStandard(strategy, diagnosticErrors, generated, sourceFilePath));

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
            diagnostics.AddRange(ValidateStrict(generated, sourceFilePath));

            // ATC_API_VER0xx: constructs that do not mean what the author thinks at the declared
            // spec version, or that must change when the version is raised.
            SpecVersionMigrationValidator.Validate(diagnostics, sourceFilePath, document, sourceText);
        }

        return Located(SpecSuppressions.Apply(document, diagnostics), sourceFilePath, sourceText);
    }

    /// <summary>
    /// Whether a diagnostic's scope is not an operation, or an operation of a path the document keeps.
    /// </summary>
    private static bool IsAtAKeptOperation(
        string? scope,
        OpenApiDocument generated)
    {
        const string prefix = "operation:";
        if (scope is null || !scope.StartsWith(prefix, StringComparison.Ordinal))
        {
            return true;
        }

        var space = scope.IndexOf(' ', prefix.Length);
        return space < 0 || generated.Paths?.ContainsKey(scope.Substring(space + 1)) == true;
    }

    /// <summary>
    /// Puts a diagnostic found at an operation, a path or a schema on its line (Microsoft.OpenApi keeps none) - under
    /// every strategy.
    /// </summary>
    private static List<DiagnosticMessage> Located(
        List<DiagnosticMessage> diagnostics,
        string sourceFilePath,
        string? sourceText)
    {
        if (!string.IsNullOrEmpty(sourceText))
        {
            SpecLocations.Apply(diagnostics, [(sourceFilePath, sourceText!)]);
        }

        return diagnostics;
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