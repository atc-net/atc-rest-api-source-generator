namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The report's Test layers tab: per feature area, the tests that prove it at each layer - unit tests, the generators run
/// in a real compilation, the scenarios (snapshots, the compile gate and the integration tests), the runtime matrices,
/// the TypeScript gates and the Playwright apps. The map names test classes (<c>Project:ClassName</c>, <c>*</c> as a
/// wildcard), scenarios, matrix cases (<c>key</c> or <c>key:group</c>) and Playwright spec files;
/// <see cref="FeatureLayerMapTests"/> checks every name still points at something.
/// </summary>
internal static class FeatureLayerMap
{
    /// <summary>The short project names the class selectors use.</summary>
    public static readonly IReadOnlyDictionary<string, string> Projects = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["OpenApi"] = "Atc.OpenApi.Tests",
        ["Generator"] = "Atc.Rest.Api.Generator.Tests",
        ["SourceGenerator"] = "Atc.Rest.Api.SourceGenerator.Tests",
        ["Cli"] = "Atc.Rest.Api.Generator.Cli.Tests",
        ["Integration"] = "Atc.Rest.Api.Generator.IntegrationTests",
    };

    /// <summary>
    /// The classes that run once per scenario: their tests count for an area through its scenarios, so no area names
    /// them (they would claim thousands of tests the area does not own).
    /// </summary>
    public static readonly IReadOnlySet<string> PerScenarioClasses = new HashSet<string>(StringComparer.Ordinal)
    {
        "GeneratorScenarioSnapshotTests",
        "GeneratorCompilationGateTests",
        "OverlayRoundTripTests",
        "ScenarioTests",
        "GeneratorComparisonTests",
        "TypeScriptScenarioTscMatrixGateTests",
    };

    public static readonly IReadOnlyList<Area> Areas =
    [
        new(
            "Models and schemas",
            "Records from schemas: properties, allOf / oneOf / anyOf, discriminators, nullability, tuples, inline schemas, names that clash with C# or .NET.",
            [
                "OpenApi:OpenApiSchemaExtensionsTests", "OpenApi:OpenApiSchemaReferenceExtensionsTests", "OpenApi:JsonSchemaTypeExtensionsTests", "OpenApi:TypeConflictRegistryTests", "OpenApi:CSharpKeywordsTests",
                "Generator:SchemaExtractor*", "Generator:PolymorphicTypeExtractorTests", "Generator:TupleExtractorTests", "Generator:InlineSchemaExtractorTests", "Generator:InlineAllOfFlattenerTests", "Generator:ModelsAndProperties*",
                "Generator:HostileSchemaKeywordsTests", "Generator:SystemTypeConflictResolverTests", "Generator:RecordContentGeneratorTests", "Generator:SchemaNameNormalizerTests", "Generator:SchemaTypeResolverTests", "Generator:SchemaLookupTests",
                "Generator:PropertyNameRegistryTests", "Generator:JsonPropertyNameHelperTests", "Generator:DefaultValueHelperTests", "Generator:RecordSerializationTests", "Generator:CSharpIdentifierHelperTests",
                "SourceGenerator:PolymorphismWireTests", "SourceGenerator:TupleWireTests", "SourceGenerator:ClientSchemaTypeTests", "SourceGenerator:ComponentsReuseTests", "SourceGenerator:DiscriminatorImprovementsTests",
                "SourceGenerator:InlineSchemaModelUsingTests", "SourceGenerator:ModelPropertyNameWireTests", "SourceGenerator:OptionalPropertyRuntimeTests", "SourceGenerator:RequiredNullablePropertyRuntimeTests", "SourceGenerator:RefSiblingTests", "SourceGenerator:UnusedComponentSchemaTests",
                "SourceGenerator:ResultNameClashTests", "SourceGenerator:EscaperPropertyTests",
                "Integration:JsonPropertyNameGenerationTests",
            ],
            ["AllOfMultiLevel", "AnyOfNoDiscriminator", "AnyOfProperties", "CircularSchemas", "ComponentsReuse", "DiscriminatorImprovements", "InlineSchemas", "ModelsAndProperties", "NullableTypeArrays", "Polymorphism", "ReadOnlyWriteOnly", "SchemaSummaryFields", "HostileSchemas", "HostileNames"],
            ["feature:polymorphism", "feature:values", "feature31:polymorphism", "feature31:values"],
            []),
        new(
            "Enums",
            "Enums from string and integer schemas: member names, the wire value in any casing, extensible (open) enums.",
            [
                "Generator:EnumExtractorTests", "Generator:InlineEnumExtractorTests", "Generator:EnumWireConverterExtractorTests", "Generator:GeneratedEnumDeclarationTests",
                "SourceGenerator:EnumMemberNameReadingTests", "SourceGenerator:EnumWireConverterFactoryCoverageTests", "SourceGenerator:EnumWireTests", "SourceGenerator:ExtensibleEnum*", "SourceGenerator:ServerEnumWireTests",
                "SourceGenerator:ServerExtensibleEnumRuntimeTests", "SourceGenerator:InlineEnumNameCollisionTests",
                "Cli:TypeScriptEnumExtractorTests",
            ],
            ["EnumNamingConflicts", "EnumRuntimeValues"],
            ["enumCasing", "enumCasingPlain", "enumCasingRdg", "enumCasingConverter", "enumCasingExtensible"],
            []),
        new(
            "Parameters and request binding",
            "Path, query, header and cookie parameters: arrays, objects, styles, defaults - bound on the server and sent by the clients.",
            [
                "OpenApi:OpenApiParameterExtensionsTests",
                "Generator:OperationParameter*", "Generator:WireBindingHelperTests", "Generator:WireValueHelperTests", "Generator:ParameterPropertyNamesTests", "Generator:MethodParameterBuilderTests",
                "Generator:ParameterBindingValidatorTests", "Generator:ParameterSchemaValidatorTests", "Generator:LargeArrayQueryParameterValidationTests", "Generator:PathTemplate*", "Generator:PathSegmentHelperTests", "Generator:PathExtensionsTests",
                "SourceGenerator:ArraySerializationWireTests", "SourceGenerator:BindingHardeningWireTests", "SourceGenerator:ClientQueryValueTests", "SourceGenerator:DateFormatWireTests", "SourceGenerator:ObjectQueryParameterTests",
                "SourceGenerator:OptionalParameterDefaultsTests", "SourceGenerator:ParameterSerializationRoundTripTests", "SourceGenerator:PathAndHeaderArrayTests", "SourceGenerator:QueryParameterKeyTests", "SourceGenerator:UnbindableParameterTests",
                "SourceGenerator:WrappedReferenceParameterTests", "SourceGenerator:HttpQueryRuntimeTests", "SourceGenerator:UrlEncodingTests", "SourceGenerator:PathValidationTests",
                "Cli:TypeScriptRequiredParameterTests", "Cli:TypeScriptRequestUrlGateTests", "Cli:TypeScriptWireNames*", "Cli:TypeScriptRequestRecordsGateTests",
            ],
            ["CookieParameters", "HeaderParameters", "HttpMethods", "ParameterSerialization"],
            ["requestBinding", "requestBindingPlain", "requestBindingOff", "requestBinding31"],
            ["users-query.spec.ts"]),
        new(
            "Request and response bodies",
            "Media types in and out: JSON, XML, multipart and forms, file uploads and downloads, text, a model or a file, response headers.",
            [
                "OpenApi:MediaTypeExtensionsTests", "OpenApi:ResponseBodyClassifierTests",
                "Generator:FormArgumentExpressionTests", "Generator:FormBodyHelper*", "Generator:XmlBodyHelperTests", "Generator:XmlBodyReachabilityTests", "Generator:XmlModelMapperExtractorTests", "Generator:MediaTypeNormalizerTests",
                "Generator:MediaTypeDeclaredTwiceValidatorTests", "Generator:ResultClassExtractorTests", "Generator:HeadResponse*", "Generator:ResponseRange*", "Generator:ResponseCodeValidationTests", "Generator:HttpStatusCodeHelperTests",
                "SourceGenerator:AntiforgeryOptionWireTests", "SourceGenerator:BodyMediaTypeGenerationTests", "SourceGenerator:BodyUploadGenerationTests", "SourceGenerator:ClientUrlEncodedFormWireTests", "SourceGenerator:FormFieldWithNoTextFormTests",
                "SourceGenerator:MediaTypeParametersTests", "SourceGenerator:ModelOrFile*", "SourceGenerator:Multipart*", "SourceGenerator:RawUploadContentTypeTests", "SourceGenerator:RequestBodyProblemsTests", "SourceGenerator:ResponseMetadataTests",
                "SourceGenerator:TypedClientEmptyBodyTests", "SourceGenerator:ClientResponseDisposalTests", "SourceGenerator:Xml*",
                "Cli:TypeScriptModelOrFile*", "Cli:TypeScriptDownload*", "Cli:TypeScriptXml*", "Cli:TypeScriptMultipartPartsGateTests", "Cli:TypeScriptSuccessBodyGateTests", "Cli:TypeScriptHeadGateTests", "Cli:TypeScriptResponseHeaders*",
            ],
            ["MediaTypeParameters", "NonJsonContent"],
            ["bodyMedia", "bodyMedia31"],
            ["files.spec.ts", "files-upload.spec.ts", "documents.spec.ts", "user-xml.spec.ts", "users-import.spec.ts"]),
        new(
            "Error responses",
            "What a failing call answers and what each client makes of it: ProblemDetails, declared error models, every status code.",
            [
                "Generator:CustomErrorResponseExtractorTests", "Generator:ErrorResponseShapeResolverTests", "Generator:BuiltInErrorContractNamesTests",
                "SourceGenerator:DeclaredErrorModelFactoryTests", "SourceGenerator:EndpointResultProblemTests", "SourceGenerator:ProblemDetails*", "SourceGenerator:ProblemPassThroughWireTests", "SourceGenerator:TypedClientApiExceptionTests",
                "SourceGenerator:XmlErrorModelTests", "SourceGenerator:XmlProblemDetailsTests",
                "Cli:TypeScriptErrorExtractorTests", "Cli:TypeScriptClientErrorContractGateTests", "Cli:TypeScriptApiResultExtractorTests",
            ],
            ["TypedClientResultStyle"],
            ["errorBodies", "errorBodiesPlain", "errorBodiesOff", "errorBodies31"],
            ["exception-testing.spec.ts"]),
        new(
            "Validation",
            "The constraints a schema declares (length, range, pattern, items) as validation on the server, and Zod in TypeScript.",
            [
                "Generator:LengthLimitAttributesExtractorTests", "Generator:SchemaConstraintValidatorTests", "Generator:ValidatorDependencyRegistrationExtractorTests",
                "SourceGenerator:FormValidationRuntimeTests", "SourceGenerator:LengthLimitAotTests", "SourceGenerator:TypedClientBoundsTests",
                "Cli:TypeScriptZodExtractorTests",
            ],
            ["ValidationConstraints", "ZodRuntimeValidate"],
            ["validation", "validationRdg", "feature:validation", "feature31:validation"],
            ["users-validation.spec.ts"]),
        new(
            "Security",
            "Authentication and authorization: API keys, bearer, OAuth 2 and OpenID Connect, roles and policies, on the server and in the clients.",
            [
                "OpenApi:OpenApiSecurityExtensionsTests",
                "Generator:OAuth*", "Generator:OpenIdConnect*", "Generator:Security*", "Generator:PolicyExtractorTests", "Generator:PolicyNamingHelperTests", "Generator:PolicyConstantsClassBuilderTests",
                "SourceGenerator:OAuth*", "SourceGenerator:RoleSchemesRuntimeTests", "SourceGenerator:SecurityBearerOnlyRuntimeTests", "SourceGenerator:SecurityValidationTests", "SourceGenerator:HealthCheckSecurityCompileTests",
                "Cli:TypeScriptSecurityGateTests",
            ],
            ["SecurityAtcExt", "SecurityBearerOnly", "SecurityHybrid", "SecurityOpenIdConnect", "SecurityStandard"],
            ["guard:authentication", "guard:authorization", "guard31:authentication", "guard31:authorization"],
            ["auth.spec.ts"]),
        new(
            "Rate limiting",
            "x-ratelimit-* policies: the limiter registered, a request over the limit answered 429 with Retry-After.",
            [
                "OpenApi:OpenApiRateLimitExtensionsTests",
                "Generator:RateLimit*",
                "SourceGenerator:RateLimit*",
            ],
            ["RateLimit"],
            ["guard:rate limit", "guard31:rate limit"],
            []),
        new(
            "Caching",
            "x-cache-* policies: output caching and HybridCache, their keys, tags and expirations.",
            [
                "OpenApi:OpenApiCache*",
                "Generator:HybridCachePoliciesExtractorTests", "Generator:OutputCache*",
                "SourceGenerator:SharedOutputCacheRuntimeTests",
            ],
            ["CachingHybrid", "CachingOutput"],
            [],
            []),
        new(
            "Client resilience",
            "x-retry-* policies: retries, timeouts and circuit breakers in the C# and TypeScript clients; cancellation and network failures.",
            [
                "OpenApi:OpenApiRetry*",
                "Generator:ResiliencePoliciesExtractorTests", "Generator:ResilienceRequestHelperTests",
                "SourceGenerator:RetryPolicyRuntimeTests", "SourceGenerator:RetrySettingValidationTests",
                "Cli:TypeScriptRetry*", "Cli:TypeScriptClientRetryGateTests", "Cli:TypeScriptTransportGateTests",
            ],
            ["Retry"],
            ["feature:network", "feature:cancellation", "feature31:network", "feature31:cancellation"],
            []),
        new(
            "Streaming and SignalR",
            "Server-Sent Events, JSON Lines and other record streams in both directions, async enumerables, SignalR hubs.",
            [
                "OpenApi:StreamedRequestBodyTests",
                "Generator:StreamingMediaTypeTests", "Generator:StreamReadersExtractorTests", "Generator:SequentialResultsExtractorTests",
                "SourceGenerator:JsonLines*", "SourceGenerator:SignalRHubTests", "SourceGenerator:StreamStartRuntimeTests", "SourceGenerator:StreamingWireFramingTests", "SourceGenerator:StreamValidationTests",
                "Cli:TypeScriptSignalRHubExtractorTests", "Cli:TypeScriptStream*", "Cli:TypeScriptStalledBodyGateTests",
            ],
            ["AsyncEnumerableHooks", "JsonLines", "StreamingItemSchema"],
            ["feature:streaming", "feature:streamed request bodies", "feature:stream formats on the wire", "feature:SignalR hubs", "feature31:streaming", "feature31:streamed request bodies", "feature31:stream formats on the wire", "feature31:SignalR hubs"],
            ["accounts-streaming.spec.ts", "live-price-feed.spec.ts", "live-streams.spec.ts", "notifications.spec.ts", "notification-subscriptions.spec.ts"]),
        new(
            "Pagination",
            "Paged results by convention or x-pagination: the page model, the client helpers, paged and infinite lists in the apps.",
            [
                "OpenApi:PaginationConventionsTests",
                "Generator:PaginationTypeResolverTests",
                "Cli:TypeScriptPaginationHelperExtractorTests",
            ],
            ["Pagination"],
            ["feature:pagination", "feature31:pagination"],
            ["accounts-paginated.spec.ts", "accounts-infinite.spec.ts"]),
        new(
            "Webhooks and callbacks",
            "Webhooks (3.1) and callbacks: the handlers and results generated for them.",
            [
                "Generator:WebhookExtractorTests", "Generator:WebhookResultExtractorTests",
                "SourceGenerator:WebhookValidationTests", "SourceGenerator:JsonLinesWebhookTests",
                "Cli:TypeScriptWebhookExtractorTests",
            ],
            ["Callbacks30"],
            [],
            ["webhook-demo.spec.ts"]),
        new(
            "Server: endpoints, handlers and hosting",
            "The server project: endpoints and their registration, handler interfaces and scaffolds, dependency injection, namespaces and usings, versioning.",
            [
                "OpenApi:OpenApiOperationExtensionsTests", "Generator:CodeGenerationService*", "Generator:Endpoint*", "Generator:Handler*", "Generator:DependencyInjectionExtractorTests", "Generator:WebApplicationExtensionsExtractorTests", "Generator:HealthCheckExtractorTests", "Generator:ApiOptionsExtractorTests",
                "Generator:ConstantsExtractorTests", "Generator:ServerFeaturesTests", "Generator:OperationFeatures*", "Generator:UsingStatementHelperTests", "Generator:GlobalUsingsHelperTests", "Generator:ImplicitUsingsHelperTests",
                "Generator:Namespace*", "Generator:GeneratedCodeAttributeHelperTests", "Generator:DeprecationDetectorTests", "Generator:OperationIdHelperTests", "Generator:UnifiedExtractorsNamingTests",
                "SourceGenerator:ApiEndpointInjectionGeneratorTests", "SourceGenerator:EndpointInjectionExtractorTests", "SourceGenerator:ApiServerDomainGeneratorExcludeFromCodeCoverageTests", "SourceGenerator:Handler*",
                "SourceGenerator:HostWiringGenerationTests", "SourceGenerator:ServerRequestDelegateGeneratorTests", "SourceGenerator:ServerWireDetailsTests", "SourceGenerator:ServerJsonAotTests", "SourceGenerator:ConsumerGlobalUsingsTests",
                "SourceGenerator:DomainGlobalUsingsHelperTests", "SourceGenerator:RootNamespaceResolutionTests", "SourceGenerator:DeprecationObsoleteTests", "SourceGenerator:IncrementalCachingTests", "SourceGenerator:EmptyDocumentGenerationTests",
                "SourceGenerator:NamespaceSegmentConflictRegressionTests", "SourceGenerator:CompilationVerificationTests", "SourceGenerator:GeneratedCodeAotGateTests",
            ],
            ["PetStoreSimple", "PetStoreFull", "Demo", "PizzaPlanet", "Showcase", "Deprecations", "VersioningHeader", "VersioningQueryString", "VersioningUrlSegment", "MultipleServers"],
            ["feature:conditional", "feature31:conditional"],
            ["home.spec.ts", "navigation.spec.ts", "tasks.spec.ts", "users.spec.ts", "users-crud.spec.ts", "accounts.spec.ts"]),
        new(
            "C# HTTP clients",
            "The generated C# clients: endpoint-per-operation and typed clients, their interfaces, result styles, serializer setup, AOT.",
            [
                "Generator:HttpClient*", "Generator:ClientGranularityConfigTests", "Generator:ServerUrlHelperTests", "Generator:HeaderBuilderTests", "Generator:CasingHelperClientTypeNameTests",
                "SourceGenerator:ApiClientGeneratorSingleGranularityTests", "SourceGenerator:ClientConfigureAwaitTests", "SourceGenerator:ClientInterfaceGenerationTests", "SourceGenerator:ClientOperationAotTests",
                "SourceGenerator:ClientSerializerRegistrationTests", "SourceGenerator:TypedClient*",
            ],
            ["SingleTypedClient", "TypedClientResultStyle"],
            [],
            []),
        new(
            "TypeScript clients",
            "The generated TypeScript: Fetch and Axios clients, models, React Query and SWR hooks, MSW handlers, the package scaffold - compiled by tsc and run against a server.",
            [
                "OpenApi:TypeScriptTypeMapperTests",
                "Cli:TypeScript*ExtractorTests", "Cli:TypeScriptClientGenerationServiceTests", "Cli:TypeScriptOperationHelper*", "Cli:TypeScriptHeaderBuilderTests", "Cli:TypeScriptHookHardeningTests", "Cli:TypeScriptSchemaCatalogTests",
                "Cli:TypeScript*BehaviorGateTests", "Cli:TypeScriptClientTscGateTests", "Cli:TypeScriptConvertDatesGateTests", "Cli:TypeScriptMsw*", "Cli:TypeScriptSwrHooksRuntimeGateTests", "Cli:NodeWorkspaceTests",
                "Cli:GenerateClientTypeScriptCommand*",
            ],
            ["HostileTypeScript", "StrictTscClient", "BrandedIds"],
            [],
            ["swr-tasks.spec.ts"]),
        new(
            "Specification rules",
            "The ATC_API_* rules a specification is checked against, their messages and severities, and that a broken specification is reported, never a crash.",
            [
                "Generator:*ValidatorTests", "Generator:*ValidationTests", "Generator:DiagnosticBuilderTests", "Generator:DiagnosticMessage*", "Generator:SpecLocationsTests", "Generator:CasingHelperTests",
                "SourceGenerator:*ValidationTests", "SourceGenerator:DiagnosticTitleTests", "SourceGenerator:ExceptionDescriptionTests", "SourceGenerator:RuleCoverageTests", "SourceGenerator:UnsupportedFeatureDiagnosticsTests",
                "SourceGenerator:GeneratorDiagnosticsTests", "SourceGenerator:GeneratorFailureDiagnosticsTests", "SourceGenerator:RuleSeverityConfigurationTests", "SourceGenerator:RuleCatalogCharacterizationTests",
                "SourceGenerator:GeneratorRobustnessFuzzTests", "SourceGenerator:CasingHelperTests",
                "Cli:SpecValidateCommand*",
            ],
            ["HostileSpec"],
            [],
            []),
        new(
            "Specifications: loading, parts and overlays",
            "Reading a specification: 3.0, 3.1 and 3.2, multi-part specifications merged, OpenAPI Overlays applied, the files a project holds.",
            [
                "OpenApi:OpenApiDocument*", "OpenApi:JsonKeywordReaderTests", "OpenApi:OpenApiSpecVersionExtensionsTests", "OpenApi:OpenApiExtension*",
                "Generator:Overlay*", "Generator:JsonPathComplianceTests", "Generator:PathFilterHelperTests", "Generator:Specification*", "Generator:MergeResultTests", "Generator:MultiPartDiagnosticsTests",
                "SourceGenerator:OverlayFileHelperTests", "SourceGenerator:OverlayGenerationTests", "SourceGenerator:MultiPartValidationTests", "SourceGenerator:MultipleSpecificationsTests", "SourceGenerator:SpecificationLoaderTests",
                "SourceGenerator:YamlFileHelperTests", "SourceGenerator:PathFilterCacheTests",
                "Cli:Spec*CommandTests", "Cli:OverlayCommandTests",
            ],
            ["MultiParts", "Overlays", "OverlaysMultiPart", "OpenApi31Features", "OpenApi32Features"],
            [],
            []),
        new(
            "Configuration and marker files",
            "The marker files and the options file: their settings, defaults, JSON schemas and the diagnostics for a bad value.",
            [
                "Generator:ConfigurationTests", "Generator:Marker*", "Generator:*TypeConverterTests", "Generator:RepositoryMarkerFilesTests", "Generator:RepositoryProjectReferencesTests", "Generator:VersionSourceTests",
                "SourceGenerator:MarkerFileHelperTests", "SourceGenerator:MarkerConfigDiagnosticTests", "SourceGenerator:GeneratorStartupTests",
                "Cli:ConfigurationSchemaTests", "Cli:ApiGeneratorOptionsTests", "Cli:ApiOptionsHelperTests", "Cli:Options*",
            ],
            [],
            [],
            []),
        new(
            "CLI",
            "The atc-rest-api-gen commands: generate, migrate, options and spec, their settings, output and project scaffolding; the packages installed into a fresh project.",
            [
                "Cli:*CommandTests", "Cli:*SettingsTests", "Cli:Migrate*", "Cli:GenerateCommandOptionsFileTests", "Generator:StatisticsCollectorTests", "Cli:CliConsistencyTests", "Cli:MarkupEscapingTests", "Cli:UpdateCheckTests", "Cli:CommandAppExtensionsTests", "Cli:NamingStrategyExtensionsTests",
                "Cli:CliVersionHelperTests", "Cli:ExecutableResolverTests", "Cli:ExternalToolTests", "Cli:FileTransactionTests", "Cli:MarkupInterpolationGuardTests", "Cli:MigrationPathResolverTests", "Cli:PathHelperTests",
                "Cli:ProjectNameHelperTests", "Cli:SerialRunnerTests", "Cli:SpecificationWatcherTests", "Cli:CallShapeDetectorTests", "Cli:ContractsNamespaceRewriterTests", "Cli:GeneratedCodeCleanerTests", "Cli:MarkerFileGeneratorTests",
                "Cli:MigrationValidatorTests", "Cli:ObsoletePackageTests", "Cli:ProjectFileModifierTests", "Cli:ProjectRenamerTests", "Cli:SpecificationValidatorTests", "Cli:NugetPackageVersionServiceTests",
                "Cli:ProjectScaffoldingServiceTests", "Cli:ReportServiceTests", "Cli:ServerScaffoldPlanTests",
                "Integration:PackageInstallTests",
            ],
            [],
            [],
            []),
    ];

    /// <summary>Whether a class name matches a selector's class part (<c>*</c> matches any run of characters).</summary>
    public static bool Matches(
        string glob,
        string className)
    {
        var parts = glob.Split('*');
        if (parts.Length == 1)
        {
            return string.Equals(glob, className, StringComparison.Ordinal);
        }

        if (!className.StartsWith(parts[0], StringComparison.Ordinal) || !className.EndsWith(parts[^1], StringComparison.Ordinal) || className.Length < parts[0].Length + parts[^1].Length)
        {
            return false;
        }

        var position = parts[0].Length;
        for (var i = 1; i < parts.Length - 1; i++)
        {
            var found = className.IndexOf(parts[i], position, StringComparison.Ordinal);
            if (found < 0)
            {
                return false;
            }

            position = found + parts[i].Length;
        }

        return position <= className.Length - parts[^1].Length;
    }

    /// <summary>Splits a selector (<c>Project:Class</c>, <c>key:group</c>) at its first colon.</summary>
    public static (string Head, string? Tail) Split(string selector)
    {
        var colon = selector.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? (selector, null) : (selector[..colon], selector[(colon + 1)..]);
    }

    /// <summary>One feature area and what proves it.</summary>
    /// <param name="Name">The area's name.</param>
    /// <param name="Covers">What the area is, in a sentence.</param>
    /// <param name="Classes">Test classes, as <c>Project:ClassName</c> with <c>*</c> wildcards.</param>
    /// <param name="Scenarios">Scenario folders under <c>test/Scenarios</c>.</param>
    /// <param name="Matrices">Runtime-matrix cases, as <c>key</c> or <c>key:group</c>.</param>
    /// <param name="Playwright">Playwright spec files under <c>test/Showcase.E2ETests/specs</c>.</param>
    internal sealed record Area(
        string Name,
        string Covers,
        IReadOnlyList<string> Classes,
        IReadOnlyList<string> Scenarios,
        IReadOnlyList<string> Matrices,
        IReadOnlyList<string> Playwright);
}