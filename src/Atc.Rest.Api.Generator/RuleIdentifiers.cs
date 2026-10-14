// ReSharper disable CommentTypo
// ReSharper disable InconsistentNaming
namespace Atc.Rest.Api.Generator;

/// <summary>
/// Contains rule identifier constants for all diagnostic rules.
/// Rule ID format: ATC_API_[3-letter category][3-digit number]
/// Categories: GEN (generation), DEP (dependencies), VAL (validation),
///             NAM (naming), SEC (security), SRV (server), SCH (schema),
///             PTH (path), OPR (operation), RL (rate limiting),
///             STREAM (streaming), CLT (client)
/// </summary>
public static class RuleIdentifiers
{
    /// <summary>
    /// Category for all Atc.Rest.Api.Generator diagnostics.
    /// </summary>
    public const string Category = "Atc.Rest.Api.Generator";

    // ========== Generation Rules (GEN) ==========

    /// <summary>
    /// ATC_API_GEN001: OpenAPI server generation error.
    /// </summary>
    public const string ServerGenerationError = "ATC_API_GEN001";

    /// <summary>
    /// ATC_API_GEN002: OpenAPI server parsing error.
    /// </summary>
    public const string ServerParsingError = "ATC_API_GEN002";

    /// <summary>
    /// ATC_API_GEN003: OpenAPI client generation error.
    /// </summary>
    public const string ClientGenerationError = "ATC_API_GEN003";

    /// <summary>
    /// ATC_API_GEN004: OpenAPI client parsing error.
    /// </summary>
    public const string ClientParsingError = "ATC_API_GEN004";

    /// <summary>
    /// ATC_API_GEN005: Handler scaffold generation error.
    /// </summary>
    public const string HandlerScaffoldGenerationError = "ATC_API_GEN005";

    /// <summary>
    /// ATC_API_GEN006: OpenAPI domain parsing error.
    /// </summary>
    public const string DomainParsingError = "ATC_API_GEN006";

    /// <summary>
    /// ATC_API_GEN007: Output directory not specified warning.
    /// </summary>
    public const string OutputDirectoryNotSpecified = "ATC_API_GEN007";

    /// <summary>
    /// ATC_API_GEN008: Endpoint injection generation error.
    /// </summary>
    public const string EndpointInjectionGenerationError = "ATC_API_GEN008";

    /// <summary>
    /// ATC_API_GEN009: No endpoints found for endpoint injection (warning).
    /// </summary>
    public const string NoEndpointsFoundForInjection = "ATC_API_GEN009";

    /// <summary>
    /// ATC_API_GEN010: Generation summary (info).
    /// </summary>
    public const string GenerationSummary = "ATC_API_GEN010";

    /// <summary>
    /// ATC_API_GEN011: Contracts namespace cannot be auto-detected — multiple sibling
    /// .atc-rest-api-server markers exist and none match the Domain project's
    /// directory-name convention (&lt;X&gt;.Domain ↔ &lt;X&gt;.Contracts).
    /// </summary>
    public const string ContractsNamespaceAmbiguous = "ATC_API_GEN011";

    /// <summary>
    /// ATC_API_GEN012: A hand-written handler shadows a scaffolded stub of the same name in
    /// the scaffold output folder; the stub is ignored and should be deleted.
    /// </summary>
    public const string HandlerStubShadowed = "ATC_API_GEN012";

    /// <summary>
    /// ATC_API_GEN013: A marker file could not be read - no JSON, or a setting with a value of the wrong kind (named);
    /// nothing is generated until it is fixed. Error.
    /// </summary>
    public const string MarkerConfigParseError = "ATC_API_GEN013";

    /// <summary>
    /// ATC_API_GEN014: A marker file has a setting the generator does not have, or a value a setting does not know.
    /// </summary>
    public const string MarkerConfigUnknownSetting = "ATC_API_GEN014";

    /// <summary>
    /// ATC_API_GEN015: An overlay the marker file lists is not an AdditionalFiles item of the project.
    /// </summary>
    public const string OverlayNotFound = "ATC_API_GEN015";

    /// <summary>
    /// ATC_API_GEN016: An overlay is invalid or one of its actions cannot be applied; nothing is generated.
    /// </summary>
    public const string OverlayInvalid = "ATC_API_GEN016";

    /// <summary>
    /// ATC_API_GEN017: An overlay action's target selects nothing, so the action changes nothing.
    /// </summary>
    public const string OverlayTargetSelectsNothing = "ATC_API_GEN017";

    /// <summary>
    /// ATC_API_GEN018: An overlay file is an AdditionalFiles item, but the marker file does not list it.
    /// </summary>
    public const string OverlayNotListed = "ATC_API_GEN018";

    /// <summary>
    /// ATC_API_GEN019: A handler scaffold cannot be written to disk (the folder is a file, the path is too long, no
    /// access); it is still part of the build's compilation.
    /// </summary>
    public const string HandlerScaffoldNotWritten = "ATC_API_GEN019";

    /// <summary>
    /// ATC_API_GEN020: A project has two marker files of one kind (two '.atc-rest-api-server', say): the generator
    /// takes one configuration per project, and names both files; nothing is generated until one is removed. Error.
    /// </summary>
    public const string MultipleMarkerFiles = "ATC_API_GEN020";

    /// <summary>
    /// ATC_API_GEN021: A project has two base specifications (two files that are no part of each other, 'Api.yaml' and
    /// 'Legacy.yaml'): the generator reads one specification per project. Nothing is generated.
    /// </summary>
    public const string MultipleSpecifications = "ATC_API_GEN021";

    /// <summary>
    /// ATC_API_GEN022: The marker's 'namespace' is no C# namespace ('Contoso Api', 'contoso-billing.v2') and was
    /// written as one ('ContosoApi', 'ContosoBilling.V2'). Information: the generated code is in the namespace the
    /// message names, not in the one the marker spells.
    /// </summary>
    public const string MarkerNamespaceRewritten = "ATC_API_GEN022";

    // ========== Dependency Rules (DEP) ==========

    /// <summary>
    /// ATC_API_DEP001: Server generator requires ASP.NET Core references.
    /// </summary>
    public const string ServerRequiresAspNetCore = "ATC_API_DEP001";

    /// <summary>
    /// ATC_API_DEP002: Domain generator requires ASP.NET Core references.
    /// </summary>
    public const string DomainRequiresAspNetCore = "ATC_API_DEP002";

    /// <summary>
    /// ATC_API_DEP003: Client EndpointPerOperation mode requires Atc.Rest.Client reference.
    /// </summary>
    public const string ClientRequiresAtcRestClient = "ATC_API_DEP003";

    /// <summary>
    /// ATC_API_DEP005: Resilience extensions require Microsoft.Extensions.Http.Resilience reference.
    /// </summary>
    public const string ResilienceRequiresPackage = "ATC_API_DEP005";

    /// <summary>
    /// ATC_API_DEP006: JWT Bearer security requires Microsoft.AspNetCore.Authentication.JwtBearer reference.
    /// </summary>
    public const string JwtBearerRequiresPackage = "ATC_API_DEP006";

    /// <summary>
    /// ATC_API_DEP007: UseMinimalApiPackage enabled but Atc.Rest.MinimalApi package not referenced.
    /// </summary>
    public const string MinimalApiPackageRequired = "ATC_API_DEP007";

    /// <summary>
    /// ATC_API_DEP008: InjectLogger enabled but Microsoft.Extensions.Logging.Abstractions not available.
    /// </summary>
    public const string LoggingRequiresPackage = "ATC_API_DEP008";

    /// <summary>
    /// ATC_API_DEP009: HealthChecks config conflicts with health paths in OpenAPI spec.
    /// </summary>
    public const string HealthCheckPathConflict = "ATC_API_DEP009";

    /// <summary>
    /// ATC_API_DEP010: HealthChecks enabled — Atc.Rest.HealthChecks package recommended for structured JSON responses.
    /// </summary>
    public const string HealthChecksPackageRecommended = "ATC_API_DEP010";

    /// <summary>
    /// ATC_API_DEP011: Client EndpointPerOperation mode (and the typed client's Result style) requires Atc.Rest.Client 2.6.0 or later.
    /// </summary>
    public const string AtcRestClientTooOld = "ATC_API_DEP011";

    /// <summary>
    /// ATC_API_DEP012: The specification declares SignalR hubs (x-signalr-hubs) and the client project does not
    /// reference Microsoft.AspNetCore.SignalR.Client, so the typed hub clients are not generated.
    /// </summary>
    public const string SignalRClientPackageRecommended = "ATC_API_DEP012";

    /// <summary>
    /// ATC_API_DEP013: The server builds on Atc.Rest.MinimalApi (its endpoint interface, ValidationFilter or error
    /// handler) and the project references a version older than 3.0.0.
    /// </summary>
    public const string MinimalApiTooOld = "ATC_API_DEP013";

    /// <summary>
    /// ATC_API_AOT001: The project turns the Request Delegate Generator on (EnableRequestDelegateGenerator, which
    /// PublishAot sets), but that generator does not see the endpoints this one emits: they are bound by reflection
    /// at run time, which trimming and Native AOT do not support - and the build's trim analysis does not say so.
    /// </summary>
    public const string RequestDelegateGeneratorDoesNotSeeTheEndpoints = "ATC_API_AOT001";

    /// <summary>
    /// ATC_API_AOT002: With the Request Delegate Generator on, the root namespace's first segment is a name found
    /// from inside Microsoft.AspNetCore.Http.Generated (a type of Microsoft.AspNetCore.Http, a namespace of
    /// Microsoft.AspNetCore or Microsoft). That generator writes an [AsParameters] type without 'global::', so its
    /// output does not compile under such a namespace (CS0426).
    /// </summary>
    public const string RootNamespaceBindsToAnAspNetCoreName = "ATC_API_AOT002";

    // ========== OpenAPI Validation Rules (VAL) ==========

    /// <summary>
    /// ATC_API_VAL001: OpenAPI core parsing error from Microsoft.OpenApi library.
    /// </summary>
    public const string OpenApiCoreError = "ATC_API_VAL001";

    /// <summary>
    /// ATC_API_VAL002: OpenAPI 2.0 not supported - must use OpenAPI 3.0.x.
    /// </summary>
    public const string OpenApi20NotSupported = "ATC_API_VAL002";

    /// <summary>
    /// ATC_API_VAL003: A warning from the Microsoft.OpenApi reader (except the kinds a rule of its own reports: a
    /// missing schema is SCH013, a reference cycle SCH023). Warning, Standard and Strict.
    /// </summary>
    public const string OpenApiCoreWarning = "ATC_API_VAL003";

    // ========== Naming Convention Rules (NAM) ==========

    /// <summary>
    /// ATC_API_NAM001: OperationId must start with lowercase letter (camelCase).
    /// </summary>
    public const string OperationIdMustBeCamelCase = "ATC_API_NAM001";

    /// <summary>
    /// ATC_API_NAM002: Model name must use PascalCase.
    /// </summary>
    public const string ModelNameMustBePascalCase = "ATC_API_NAM002";

    /// <summary>
    /// ATC_API_NAM003: Property name must use camelCase.
    /// </summary>
    public const string PropertyNameMustBeCamelCase = "ATC_API_NAM003";

    /// <summary>
    /// ATC_API_NAM004: Parameter name must use camelCase.
    /// </summary>
    public const string ParameterNameMustBeCamelCase = "ATC_API_NAM004";

    /// <summary>
    /// ATC_API_NAM005: Enum value must use PascalCase or UPPER_SNAKE_CASE.
    /// </summary>
    public const string EnumValueCasing = "ATC_API_NAM005";

    /// <summary>
    /// ATC_API_NAM006: Tag name must use kebab-case.
    /// </summary>
    public const string TagNameMustBeKebabCase = "ATC_API_NAM006";

    // ========== Security Rules (SEC) ==========

    /// <summary>
    /// ATC_API_SEC001: Path authorize role not defined in global section.
    /// </summary>
    public const string PathAuthorizeRoleNotDefined = "ATC_API_SEC001";

    /// <summary>
    /// ATC_API_SEC002: Path authentication scheme not defined in global section.
    /// </summary>
    public const string PathAuthenticationSchemeNotDefined = "ATC_API_SEC002";

    /// <summary>
    /// ATC_API_SEC003: Operation authorize role not defined in global section.
    /// </summary>
    public const string OperationAuthorizeRoleNotDefined = "ATC_API_SEC003";

    /// <summary>
    /// ATC_API_SEC004: Operation authentication scheme not defined in global section.
    /// </summary>
    public const string OperationAuthenticationSchemeNotDefined = "ATC_API_SEC004";

    /// <summary>
    /// ATC_API_SEC005: Operation has authenticationRequired=false but has roles/schemes.
    /// </summary>
    public const string OperationAuthenticationConflict = "ATC_API_SEC005";

    /// <summary>
    /// ATC_API_SEC006: Operation authorize role has incorrect casing vs global section.
    /// </summary>
    public const string OperationAuthorizeRoleCasing = "ATC_API_SEC006";

    /// <summary>
    /// ATC_API_SEC007: Operation authentication scheme has incorrect casing vs global.
    /// </summary>
    public const string OperationAuthenticationSchemeCasing = "ATC_API_SEC007";

    /// <summary>
    /// ATC_API_SEC008: Path authorize role has incorrect casing vs global section.
    /// </summary>
    public const string PathAuthorizeRoleCasing = "ATC_API_SEC008";

    /// <summary>
    /// ATC_API_SEC009: Path authentication scheme has incorrect casing vs global.
    /// </summary>
    public const string PathAuthenticationSchemeCasing = "ATC_API_SEC009";

    /// <summary>
    /// ATC_API_SEC010: Path has authenticationRequired=false but has roles/schemes.
    /// </summary>
    public const string PathAuthenticationConflict = "ATC_API_SEC010";

    /// <summary>
    /// ATC_API_SEC011: mutualTLS security scheme — no HTTP credential is injected by the generator.
    /// Certificate must be configured at the HttpClient transport level.
    /// </summary>
    public const string MutualTlsSchemeNoCertInjection = "ATC_API_SEC011";

    /// <summary>
    /// ATC_API_SEC012: A 'security' requirement names a scheme that 'components/securitySchemes' does not declare
    /// (a typo such as 'petsstore_auth'). The generated endpoint would require an authentication scheme no host
    /// registers, so every request on it fails at run time. Error, every strategy.
    /// </summary>
    public const string SecuritySchemeNotDeclared = "ATC_API_SEC012";

    // ========== Streaming Rules (STREAM) ==========

    /// <summary>
    /// ATC_API_STREAM001: Streaming media type has unsupported `prefixEncoding`.
    /// The generator does not yet emit per-prefix encoding headers; this field is ignored.
    /// </summary>
    public const string StreamingPrefixEncodingUnsupported = "ATC_API_STREAM001";

    // ========== Rate Limiting Rules (RL) ==========

    /// <summary>
    /// ATC_API_RL001: Unrecognized `x-ratelimit-partition` value.
    /// The value falls back to `global`, which means one shared bucket for all callers.
    /// </summary>
    public const string RateLimitPartitionValueUnrecognized = "ATC_API_RL001";

    /// <summary>
    /// ATC_API_RL002: `x-ratelimit-partition-claim` declared without an effective `x-ratelimit-partition: user`.
    /// The claim is only read when partitioning by user, so it is silently ignored.
    /// </summary>
    public const string RateLimitPartitionClaimWithoutUserPartition = "ATC_API_RL002";

    /// <summary>
    /// ATC_API_RL003: One rate limit policy name is declared with conflicting settings at several sites.
    /// A policy name maps to a single limiter registration, so the first declaration wins and the rest
    /// are silently discarded.
    /// </summary>
    public const string RateLimitPolicyConflictingSettings = "ATC_API_RL003";

    /// <summary>
    /// ATC_API_RL004: Two or more distinct rate limit policy names sanitize to the same C# identifier,
    /// which emits duplicate constants in the generated RateLimitPolicies class and breaks compilation.
    /// </summary>
    public const string RateLimitPolicyNameCollision = "ATC_API_RL004";

    /// <summary>
    /// ATC_API_RL005: A rate limit value is outside the range the limiter accepts. The limiter
    /// constructor throws <see cref="ArgumentException"/> during DI setup, so the application
    /// fails to start.
    /// </summary>
    public const string RateLimitValueOutOfRange = "ATC_API_RL005";

    /// <summary>
    /// ATC_API_RL006: `x-ratelimit-enabled` is only honoured at operation level; declaring it at
    /// document or path level is silently ignored and the endpoints stay rate limited.
    /// </summary>
    public const string RateLimitEnabledIgnoredOutsideOperation = "ATC_API_RL006";

    /// <summary>
    /// ATC_API_RL007: The policy's algorithm does not put a Retry-After value on its rejection lease,
    /// so no Retry-After header is emitted even though `x-ratelimit-emit-retry-after` is enabled.
    /// Applies to `sliding` and `concurrency`.
    /// </summary>
    public const string RateLimitRetryAfterUnsupportedByAlgorithm = "ATC_API_RL007";

    /// <summary>
    /// ATC_API_RL008: `x-ratelimit-window-seconds` is ignored by the concurrency limiter, which has
    /// no time component.
    /// </summary>
    public const string RateLimitWindowIgnoredForConcurrency = "ATC_API_RL008";

    // ========== Retry Rules (RTY) ==========

    /// <summary>
    /// ATC_API_RTY001: An x-retry-* setting no generated policy reads: a policy takes its values from where its name is
    /// first declared, inheriting operation → path → document, so a setting at a level no such declaration covers is
    /// dropped - with no x-retry-policy in the document, every one. (The timeout of an operation that is not retried is
    /// read.) Warning, Standard and Strict.
    /// </summary>
    public const string RetrySettingNotApplied = "ATC_API_RTY001";

    // ========== Caching Rules (CACHE) ==========

    /// <summary>
    /// ATC_API_CACHE001: Output caching is configured on an operation whose HTTP method the
    /// ASP.NET Core output-cache middleware never caches. The middleware's default policy stores
    /// only GET and HEAD responses, so the emitted <c>.CacheOutput(...)</c> call has no effect.
    /// </summary>
    public const string OutputCacheOnNonCacheableMethod = "ATC_API_CACHE001";

    // ========== Server Rules (SRV) ==========

    /// <summary>
    /// ATC_API_SRV001: Invalid server URL format.
    /// </summary>
    public const string InvalidServerUrl = "ATC_API_SRV001";

    /// <summary>
    /// ATC_API_SRV002: An operation declares its 400 as a string while the ValidationFilter is on: an invalid
    /// request is answered with a ValidationProblemDetails, whose field errors the string cannot carry.
    /// </summary>
    public const string ValidationBadRequestDeclaredAsString = "ATC_API_SRV002";

    // ========== Schema Rules (SCH) ==========

    /// <summary>
    /// ATC_API_SCH001: Missing title on array type.
    /// </summary>
    public const string ArrayTitleMissing = "ATC_API_SCH001";

    /// <summary>
    /// ATC_API_SCH002: Array type title not starting with uppercase.
    /// </summary>
    public const string ArrayTitleNotUppercase = "ATC_API_SCH002";

    /// <summary>
    /// ATC_API_SCH003: Missing title on object type.
    /// </summary>
    public const string ObjectTitleMissing = "ATC_API_SCH003";

    /// <summary>
    /// ATC_API_SCH004: Object type title not starting with uppercase.
    /// </summary>
    public const string ObjectTitleNotUppercase = "ATC_API_SCH004";

    /// <summary>
    /// ATC_API_SCH005: Implicit object definition in array property not supported.
    /// </summary>
    public const string ImplicitArrayObjectNotSupported = "ATC_API_SCH005";

    /// <summary>
    /// ATC_API_SCH006: Object name not using correct casing style.
    /// </summary>
    public const string ObjectNameCasing = "ATC_API_SCH006";

    /// <summary>
    /// ATC_API_SCH007: Object property name not using correct casing style.
    /// </summary>
    public const string PropertyNameCasing = "ATC_API_SCH007";

    /// <summary>
    /// ATC_API_SCH008: Enum name not using correct casing style.
    /// </summary>
    public const string EnumNameCasing = "ATC_API_SCH008";

    /// <summary>
    /// ATC_API_SCH009: Array property missing data type specification.
    /// </summary>
    public const string ArrayPropertyMissingType = "ATC_API_SCH009";

    /// <summary>
    /// ATC_API_SCH010: Implicit object definition on property not supported.
    /// </summary>
    public const string ImplicitObjectNotSupported = "ATC_API_SCH010";

    /// <summary>
    /// ATC_API_SCH011: Array property missing items specification.
    /// </summary>
    public const string ArrayPropertyMissingItems = "ATC_API_SCH011";

    /// <summary>
    /// ATC_API_SCH012: Missing key/name for object property.
    /// </summary>
    public const string PropertyKeyMissing = "ATC_API_SCH012";

    /// <summary>
    /// ATC_API_SCH013: Schema reference does not exist in components.schemas.
    /// </summary>
    public const string InvalidSchemaReference = "ATC_API_SCH013";

    /// <summary>
    /// ATC_API_SCH014: Multiple non-null types in type array (OpenAPI 3.1).
    /// Schema has multiple types like ["string", "integer"], using primary type.
    /// </summary>
    public const string MultipleNonNullTypes = "ATC_API_SCH014";

    /// <summary>
    /// ATC_API_SCH015: $ref with sibling properties detected (OpenAPI 3.1).
    /// Schema reference has sibling properties (description, deprecated, default) that override target.
    /// </summary>
    public const string RefWithSiblingProperties = "ATC_API_SCH015";

    /// <summary>
    /// ATC_API_SCH016: Schema uses const value (JSON Schema 2020-12).
    /// Const value will be used as the default and only valid value for this property.
    /// </summary>
    public const string SchemaUsesConstValue = "ATC_API_SCH016";

    /// <summary>
    /// ATC_API_SCH017: Schema uses unevaluatedProperties: false (JSON Schema 2020-12).
    /// This restricts properties in composition (allOf/oneOf/anyOf) but is not fully supported in code generation.
    /// </summary>
    public const string UnevaluatedPropertiesNotSupported = "ATC_API_SCH017";

    /// <summary>
    /// ATC_API_SCH018: Two or more distinct schema names sanitize to the same C# identifier,
    /// which would produce duplicate type definitions in the generated code.
    /// </summary>
    public const string SchemaNameCollision = "ATC_API_SCH018";

    /// <summary>
    /// ATC_API_SCH019: A reusable media type (in components.mediaTypes) wraps an anonymous
    /// inline schema with no $ref to components.schemas. Reference a named schema instead to
    /// ensure a stable, single type emission.
    /// </summary>
    public const string AnonymousInlineMediaTypeSchema = "ATC_API_SCH019";

    /// <summary>
    /// ATC_API_SCH020: A polymorphic schema has a discriminator block but 'propertyName' is absent
    /// and no common string property could be auto-detected across all variants.
    /// Add 'propertyName' to the discriminator block for reliable polymorphic code generation.
    /// </summary>
    public const string DiscriminatorMissingPropertyName = "ATC_API_SCH020";

    /// <summary>
    /// ATC_API_SCH021: The specification defines a schema named 'ProblemDetails' or
    /// 'ValidationProblemDetails' while generating in EndpointPerOperation mode, where the generator
    /// already emits its own built-in versions of those types. The spec-defined schema is ignored.
    /// </summary>
    public const string ProblemDetailsSchemaShadowedByBuiltIn = "ATC_API_SCH021";

    /// <summary>
    /// ATC_API_SCH022: An object schema has properties but no 'required' list, so every property is optional and
    /// generated nullable with a '= null' default. Usually a specification gap: list the properties the API always
    /// sends in 'required'. Info, Strict mode only.
    /// </summary>
    public const string SchemaWithoutRequiredProperties = "ATC_API_SCH022";

    /// <summary>
    /// ATC_API_SCH023: A component schema contains itself - through 'allOf' (it inherits itself), a bare '$ref'
    /// (it aliases itself) or the 'items' of an array alias (it lists itself). The generators inline those three,
    /// so the schema cannot be generated; a schema that refers to itself from a property is fine. Error, every
    /// strategy.
    /// </summary>
    public const string SchemaCycle = "ATC_API_SCH023";

    /// <summary>
    /// ATC_API_SCH024: A component schema's type is only 'null' ('type: [null]' in OpenAPI 3.1): it has no values
    /// and no C# type, so nothing is generated for it while every reference to it names a type that does not exist.
    /// Error, every strategy.
    /// </summary>
    public const string SchemaTypeOnlyNull = "ATC_API_SCH024";

    /// <summary>
    /// ATC_API_SCH025: A schema constraint the generated code cannot carry - bounds the wrong way round
    /// (minLength past maxLength, minimum past maximum, minItems past maxItems), a bound that is no finite number,
    /// a 'pattern' that is no regular expression, an integer enum with a member that is no integer, an enum with no
    /// member but null. The generated attribute would throw at run time, or a type be named that is never generated.
    /// Error, every strategy.
    /// </summary>
    public const string SchemaConstraintNotGeneratable = "ATC_API_SCH025";

    /// <summary>
    /// ATC_API_SCH026: An enum lists a value twice (JSON Schema: the values SHOULD be unique). The value is generated
    /// once - two members with one wire value could not both be read back. Warning, every strategy.
    /// </summary>
    public const string EnumValueListedTwice = "ATC_API_SCH026";

    /// <summary>
    /// ATC_API_SCH027: A reference in a form the generators cannot follow: to another file or a URL, a pointer into a
    /// component (a property of a schema), or an OpenAPI 3.1 '$dynamicRef'. Only '#/components/{section}/{name}'
    /// references are generated - bundle the document first. Error, every strategy.
    /// </summary>
    public const string UnsupportedReferenceForm = "ATC_API_SCH027";

    // ========== Path Rules (PTH) ==========

    /// <summary>
    /// ATC_API_PTH001: Path parameters not well-formatted (unbalanced braces).
    /// </summary>
    public const string PathParametersNotWellFormatted = "ATC_API_PTH001";

    /// <summary>
    /// ATC_API_PTH002: The specification has no operations (a models-only document, a multi-part base file whose
    /// parts are missing, an overlay that removed 'paths'): no endpoints, handlers or client methods are generated.
    /// Warning, every strategy.
    /// </summary>
    public const string NoOperations = "ATC_API_PTH002";

    // ========== Operation Rules (OPR) ==========

    /// <summary>
    /// ATC_API_OPR001: Missing operationId.
    /// </summary>
    public const string OperationIdMissing = "ATC_API_OPR001";

    /// <summary>
    /// ATC_API_OPR002: OperationId not using correct casing style.
    /// </summary>
    public const string OperationIdCasing = "ATC_API_OPR002";

    /// <summary>
    /// ATC_API_OPR003: GET operationId should start with 'Get' or 'List'.
    /// </summary>
    public const string GetOperationIdPrefix = "ATC_API_OPR003";

    /// <summary>
    /// ATC_API_OPR004: POST operationId should not start with 'Delete'.
    /// </summary>
    public const string PostOperationIdPrefix = "ATC_API_OPR004";

    /// <summary>
    /// ATC_API_OPR005: PUT operationId should start with 'Update'.
    /// </summary>
    public const string PutOperationIdPrefix = "ATC_API_OPR005";

    /// <summary>
    /// ATC_API_OPR006: PATCH operationId should start with 'Patch' or 'Update'.
    /// </summary>
    public const string PatchOperationIdPrefix = "ATC_API_OPR006";

    /// <summary>
    /// ATC_API_OPR007: DELETE operationId should start with 'Delete' or 'Remove'.
    /// </summary>
    public const string DeleteOperationIdPrefix = "ATC_API_OPR007";

    /// <summary>
    /// ATC_API_OPR008: Pluralized operationId but response is single item.
    /// </summary>
    public const string OperationIdPluralizationMismatch = "ATC_API_OPR008";

    /// <summary>
    /// ATC_API_OPR009: Singular operationId but response is array.
    /// </summary>
    public const string OperationIdSingularMismatch = "ATC_API_OPR009";

    /// <summary>
    /// ATC_API_OPR010: Has BadRequest response but no parameters.
    /// </summary>
    public const string BadRequestWithoutParameters = "ATC_API_OPR010";

    /// <summary>
    /// ATC_API_OPR011: Global path parameter not present in route.
    /// </summary>
    public const string GlobalPathParameterNotInRoute = "ATC_API_OPR011";

    /// <summary>
    /// ATC_API_OPR012: Operation missing path parameter defined in route.
    /// </summary>
    public const string OperationMissingPathParameter = "ATC_API_OPR012";

    /// <summary>
    /// ATC_API_OPR013: Operation path parameter not present in route.
    /// </summary>
    public const string OperationPathParameterNotInRoute = "ATC_API_OPR013";

    /// <summary>
    /// ATC_API_OPR014: GET with path parameter missing NotFound response.
    /// </summary>
    public const string GetMissingNotFoundResponse = "ATC_API_OPR014";

    /// <summary>
    /// ATC_API_OPR015: Path parameter missing required=true.
    /// </summary>
    public const string PathParameterNotRequired = "ATC_API_OPR015";

    /// <summary>
    /// ATC_API_OPR016: Path parameter must not be nullable.
    /// </summary>
    public const string PathParameterNullable = "ATC_API_OPR016";

    /// <summary>
    /// ATC_API_OPR017: RequestBody with inline model not supported.
    /// </summary>
    public const string RequestBodyInlineModel = "ATC_API_OPR017";

    /// <summary>
    /// ATC_API_OPR018: Multiple 2xx status codes not supported.
    /// </summary>
    public const string Multiple2xxStatusCodes = "ATC_API_OPR018";

    // ========== Response Code Consistency Rules (OPR021-OPR025) ==========

    /// <summary>
    /// ATC_API_OPR021: 401 Unauthorized response defined but no security requirements.
    /// </summary>
    public const string UnauthorizedWithoutSecurity = "ATC_API_OPR021";

    /// <summary>
    /// ATC_API_OPR022: 403 Forbidden response defined on an operation without any security requirement.
    /// </summary>
    public const string ForbiddenWithoutAuthorization = "ATC_API_OPR022";

    /// <summary>
    /// ATC_API_OPR023: 404 NotFound response on POST operation (unusual).
    /// </summary>
    public const string NotFoundOnPostOperation = "ATC_API_OPR023";

    /// <summary>
    /// ATC_API_OPR024: 409 Conflict response on read-only operation (GET).
    /// </summary>
    public const string ConflictOnNonMutatingOperation = "ATC_API_OPR024";

    /// <summary>
    /// ATC_API_OPR025: 429 TooManyRequests response defined but no rate limiting configured.
    /// </summary>
    public const string TooManyRequestsWithoutRateLimiting = "ATC_API_OPR025";

    // ========== Parameter Serialization Rules (OPR026) ==========

    /// <summary>
    /// ATC_API_OPR026: Parameter declares a style/explode serialization this generator does not
    /// yet support; default form serialization is emitted instead.
    /// </summary>
    public const string ParameterSerializationNotSupported = "ATC_API_OPR026";

    /// <summary>
    /// ATC_API_OPR027: A <c>GET</c> declares an array query parameter with no <c>maxItems</c>, or a
    /// <c>maxItems</c> large enough that the serialized URL can exceed a typical 8 KB request-line
    /// limit. Such a request fails at runtime with <c>414 URI Too Long</c> before it reaches a
    /// handler. An OpenAPI 3.2 <c>query:</c> operation carries the criteria in a request body
    /// instead.
    /// </summary>
    public const string LargeArrayQueryParameterShouldUseQueryMethod = "ATC_API_OPR027";

    /// <summary>
    /// ATC_API_OPR028: A <c>QUERY</c> operation declares no <c>requestBody</c>. The entire reason to
    /// choose <c>QUERY</c> over <c>GET</c> is that the criteria travel in a body, so such an
    /// operation is a <c>GET</c> written with a verb that fewer intermediaries understand.
    /// </summary>
    public const string QueryOperationWithoutRequestBody = "ATC_API_OPR028";

    /// <summary>
    /// ATC_API_OPR029: A <c>QUERY</c> operation declares a response that implies it changes state
    /// (<c>201 Created</c> or <c>409 Conflict</c>). <c>QUERY</c> is safe and idempotent, so a caller
    /// or cache is entitled to assume it mutates nothing.
    /// </summary>
    public const string QueryOperationWithMutationResponse = "ATC_API_OPR029";

    /// <summary>
    /// ATC_API_OPR030: An operation declares an informational (<c>1xx</c>) response. A 1xx is never a final
    /// response (RFC 9110 §15.2), so no factory, endpoint metadata or client accessor is generated for it.
    /// </summary>
    public const string InformationalResponseStatusCode = "ATC_API_OPR030";

    /// <summary>
    /// ATC_API_OPR031: A request or response body is not generated as the specification declares it: an XML body
    /// whose schema has no XML mapping (inline or polymorphic), the XML variant of a JSON body that is not the same
    /// model, or a response that declares a model and a file for a status other than 200 (only its model is
    /// generated). The body is otherwise dropped without notice.
    /// </summary>
    public const string BodyNotGeneratedAsDeclared = "ATC_API_OPR031";

    /// <summary>
    /// ATC_API_OPR032: Two operations have operationIds that become one C# name ('getUser' and 'get_user', or
    /// 'GetUSER'): one handler interface, one result class and one client method for both. Error, every strategy.
    /// </summary>
    public const string OperationIdCollision = "ATC_API_OPR032";

    /// <summary>
    /// ATC_API_OPR033: A parameter has neither a 'schema' nor a 'content' map: it has no type, and a client would
    /// pass an 'object' where a string belongs. Error, every strategy.
    /// </summary>
    public const string ParameterWithoutSchema = "ATC_API_OPR033";

    /// <summary>
    /// ATC_API_OPR034: An operation declares a response for a status range ('2XX', '4XX', '5XX'), which names no
    /// status a server could answer or a client could read with. The range is generated as its first status (200)
    /// unless the operation declares that status itself, in which case the range is not generated.
    /// </summary>
    public const string ResponseStatusRange = "ATC_API_OPR034";

    /// <summary>
    /// ATC_API_OPR035: A response declares 'links' (OpenAPI runtime expressions naming the operations a client may
    /// call next). Nothing is generated from them; a client follows them by hand. Information, every strategy.
    /// </summary>
    public const string ResponseLinksDropped = "ATC_API_OPR035";

    /// <summary>
    /// ATC_API_OPR036: An OAS 3.2 'in: querystring' parameter appears more than once, or next to an 'in: query'
    /// parameter of the operation or its path item - the specification forbids both: the querystring parameter is the
    /// whole query string. Error, every strategy.
    /// </summary>
    public const string QueryStringParameterNotAlone = "ATC_API_OPR036";

    /// <summary>
    /// ATC_API_OPR037: A parameter's value is one the generated code cannot bind or send - an object other than a
    /// flat model in the query (as deepObject or form with explode), or a 'oneOf'/'anyOf'/'allOf' without a type -
    /// or a form field that is an object, a map or a composition and has no form encoding: in a URL-encoded form, or in a
    /// multipart form whose encoding names another media type than JSON (else it is an application/json part). A
    /// warning (a client leaves the parameter or the field out; a server generator raises it to an error), an error in
    /// the path.
    /// </summary>
    public const string ParameterValueNotBindable = "ATC_API_OPR037";

    /// <summary>
    /// ATC_API_OPR038: An object in the query sent as 'form' with 'explode' (each property by its own name) has a
    /// property named like another query parameter of the operation - both would be one key. Error, every strategy.
    /// </summary>
    public const string QueryObjectKeyCollision = "ATC_API_OPR038";

    /// <summary>
    /// ATC_API_OPR039: A HEAD operation declares content for a response. A HEAD answer has no body, so the content
    /// is not generated: no client returns a value for it and the server's result takes none. Warning, every strategy.
    /// </summary>
    public const string HeadResponseContentIgnored = "ATC_API_OPR039";

    /// <summary>
    /// ATC_API_OPR040: A request body or a response declares two media types that are one for the generators -
    /// 'application/json' and 'application/json; charset=utf-8', or a model under '*/*' next to 'application/json' -
    /// with different schemas. The first is generated, the second is not. Warning, every strategy.
    /// </summary>
    public const string MediaTypeDeclaredTwice = "ATC_API_OPR040";

    // ========== Webhook Rules (WBH) ==========

    /// <summary>
    /// ATC_API_WBH001: Webhook operation missing operationId.
    /// </summary>
    public const string WebhookMissingOperationId = "ATC_API_WBH001";

    /// <summary>
    /// ATC_API_WBH002: Webhook operation missing request body.
    /// </summary>
    public const string WebhookMissingRequestBody = "ATC_API_WBH002";

    /// <summary>
    /// ATC_API_WBH003: Webhooks detected in specification (OpenAPI 3.1 info).
    /// </summary>
    public const string WebhooksDetected = "ATC_API_WBH003";

    // ========== Multi-Part Rules (MPT) ==========

    /// <summary>
    /// ATC_API_MPT001: Duplicate path found in part file.
    /// </summary>
    public const string DuplicatePathInPart = "ATC_API_MPT001";

    /// <summary>
    /// ATC_API_MPT002: Duplicate schema found in part file.
    /// </summary>
    public const string DuplicateSchemaInPart = "ATC_API_MPT002";

    /// <summary>
    /// ATC_API_MPT003: Part file contains prohibited section (info, openapi, servers, securitySchemes).
    /// </summary>
    public const string PartFileContainsProhibitedSection = "ATC_API_MPT003";

    /// <summary>
    /// ATC_API_MPT004: Multi-part merge successful (info).
    /// </summary>
    public const string MultiPartMergeSuccessful = "ATC_API_MPT004";

    /// <summary>
    /// ATC_API_MPT006: Unresolved reference after merge.
    /// </summary>
    public const string UnresolvedReferenceAfterMerge = "ATC_API_MPT006";

    /// <summary>
    /// ATC_API_MPT007: Part file has an info section - it is ignored, the merge takes info from the base file.
    /// </summary>
    public const string PartFileInfoIgnored = "ATC_API_MPT007";

    /// <summary>
    /// ATC_API_MPT008: Base file not found for multi-part specification.
    /// </summary>
    public const string BaseFileNotFound = "ATC_API_MPT008";

    /// <summary>
    /// ATC_API_MPT009: Duplicate parameter found in part file.
    /// </summary>
    public const string DuplicateParameterInPart = "ATC_API_MPT009";

    /// <summary>
    /// ATC_API_MPT010: Non-identical duplicate found during MergeIfIdentical.
    /// </summary>
    public const string NonIdenticalMergeConflict = "ATC_API_MPT010";

    /// <summary>
    /// ATC_API_MPT011: A path's operations belong to more than one split group; the path is kept whole in one part.
    /// </summary>
    public const string SplitPathSpansGroups = "ATC_API_MPT011";

    /// <summary>
    /// ATC_API_MPT012: A part file declares another OpenAPI version than the base file; the part is read by its own
    /// version (a 3.0 'nullable' in a 3.1 part is an unknown keyword, say).
    /// </summary>
    public const string PartFileOpenApiVersionDiffers = "ATC_API_MPT012";

    /// <summary>
    /// ATC_API_MPT013: A part file contributes nothing to the merge - no paths, no components (a 'paths' that is no
    /// map, an empty file).
    /// </summary>
    public const string PartFileContributesNothing = "ATC_API_MPT013";

    // ========== Client Rules (CLT) ==========

    /// <summary>
    /// ATC_API_CLT001: Generated type name collision in single-client mode.
    /// </summary>
    public const string SingleClientTypeNameCollision = "ATC_API_CLT001";

    /// <summary>
    /// ATC_API_CLT002: 'clientName' is ignored when clientGranularity is PerArea.
    /// </summary>
    public const string ClientNameIgnoredForPerArea = "ATC_API_CLT002";

    // ========== Spec Version Migration Rules (VER) ==========

    /// <summary>
    /// ATC_API_VER001: 'nullable' is declared on a Schema Object that has no 'type' of its own,
    /// so per the specification it has no effect.
    /// </summary>
    public const string NullableWithoutSiblingType = "ATC_API_VER001";

    /// <summary>
    /// ATC_API_VER002: 'nullable' is used in an OpenAPI 3.1 or later document, where the keyword
    /// no longer exists.
    /// </summary>
    public const string NullableKeywordRemovedInSpecVersion = "ATC_API_VER002";

    /// <summary>
    /// ATC_API_VER003: 'exclusiveMinimum' or 'exclusiveMaximum' is declared in the boolean form,
    /// which OpenAPI 3.1 replaced with a numeric value.
    /// </summary>
    public const string ExclusiveBoundBooleanForm = "ATC_API_VER003";

    /// <summary>
    /// ATC_API_VER004: A type array ('type: [string, "null"]') is used in an OpenAPI 3.0 document,
    /// where 'type' must be a single string.
    /// </summary>
    public const string TypeArrayNotSupportedInSpecVersion = "ATC_API_VER004";
}