// ReSharper disable CommentTypo
// ReSharper disable InconsistentNaming
namespace Atc.Rest.Api.Generator;

/// <summary>
/// Contains rule identifier constants for all diagnostic rules.
/// Rule ID format: ATC_API_[3-letter category][3-digit number]
/// Categories: GEN (generation), DEP (dependencies), VAL (validation),
///             NAM (naming), SEC (security), SRV (server), SCH (schema),
///             PTH (path), OPR (operation)
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
    /// ATC_API_DEP004: Rate limiting extensions require Microsoft.AspNetCore.RateLimiting reference.
    /// </summary>
    public const string RateLimitingRequiresPackage = "ATC_API_DEP004";

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

    // ========== OpenAPI Validation Rules (VAL) ==========

    /// <summary>
    /// ATC_API_VAL001: OpenAPI core parsing error from Microsoft.OpenApi library.
    /// </summary>
    public const string OpenApiCoreError = "ATC_API_VAL001";

    /// <summary>
    /// ATC_API_VAL002: OpenAPI 2.0 not supported - must use OpenAPI 3.0.x.
    /// </summary>
    public const string OpenApi20NotSupported = "ATC_API_VAL002";

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

    // ========== Server Rules (SRV) ==========

    /// <summary>
    /// ATC_API_SRV001: Invalid server URL format.
    /// </summary>
    public const string InvalidServerUrl = "ATC_API_SRV001";

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

    // ========== Path Rules (PTH) ==========

    /// <summary>
    /// ATC_API_PTH001: Path parameters not well-formatted (unbalanced braces).
    /// </summary>
    public const string PathParametersNotWellFormatted = "ATC_API_PTH001";

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

    // ========== Response Code Consistency Rules (OPR021-OPR026) ==========

    /// <summary>
    /// ATC_API_OPR021: 401 Unauthorized response defined but no security requirements.
    /// </summary>
    public const string UnauthorizedWithoutSecurity = "ATC_API_OPR021";

    /// <summary>
    /// ATC_API_OPR022: 403 Forbidden response defined but no authorization (roles/policies).
    /// </summary>
    public const string ForbiddenWithoutAuthorization = "ATC_API_OPR022";

    /// <summary>
    /// ATC_API_OPR023: 404 NotFound response on POST operation (unusual).
    /// </summary>
    public const string NotFoundOnPostOperation = "ATC_API_OPR023";

    /// <summary>
    /// ATC_API_OPR024: 409 Conflict response on non-mutating operation (GET/DELETE).
    /// </summary>
    public const string ConflictOnNonMutatingOperation = "ATC_API_OPR024";

    /// <summary>
    /// ATC_API_OPR025: 429 TooManyRequests response defined but no rate limiting configured.
    /// </summary>
    public const string TooManyRequestsWithoutRateLimiting = "ATC_API_OPR025";

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
    /// ATC_API_MPT005: Referenced part file not found.
    /// </summary>
    public const string PartFileNotFound = "ATC_API_MPT005";

    /// <summary>
    /// ATC_API_MPT006: Unresolved reference after merge.
    /// </summary>
    public const string UnresolvedReferenceAfterMerge = "ATC_API_MPT006";

    /// <summary>
    /// ATC_API_MPT007: Part file missing openapi version - will use base file version.
    /// </summary>
    public const string PartFileMissingOpenApiVersion = "ATC_API_MPT007";

    /// <summary>
    /// ATC_API_MPT008: Base file not found for multi-part specification.
    /// </summary>
    public const string BaseFileNotFound = "ATC_API_MPT008";
}