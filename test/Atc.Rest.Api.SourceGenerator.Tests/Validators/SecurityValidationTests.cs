namespace Atc.Rest.Api.SourceGenerator.Tests.Validators;

/// <summary>
/// Tests for Security validation rules (SEC001-SEC010).
/// </summary>
/// <remarks>
/// Security validation uses custom OpenAPI extensions:
/// - x-authentication-required: bool - whether authentication is required
/// - x-authorize-roles: string[] - required roles at document/path/operation level
/// - x-authentication-schemes: string[] - authentication schemes at document/path/operation level
/// </remarks>
[SuppressMessage("", "SA1512:Single-line comments should not be followed by blank line", Justification = "OK")]
[SuppressMessage("", "SA1515:Single-line comment should be preceded by blank line", Justification = "OK")]
public class SecurityValidationTests
{
    private const string TestFilePath = "test.yaml";

    // ========== SEC001: Path authorize role not defined in global section ==========

    [Fact]
    public void Validate_PathRoleNotDefinedInGlobal_ReportsSEC001()
    {
        // Arrange
        var yaml = CreateYamlWithGlobalAndPathRoles(
            globalRoles: ["admin", "user"],
            pathRoles: ["admin", "supervisor"]); // supervisor not in global
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sec001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathAuthorizeRoleNotDefined);
        Assert.NotNull(sec001);
        Assert.Contains("supervisor", sec001.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PathRoleDefinedInGlobal_NoSEC001()
    {
        // Arrange
        var yaml = CreateYamlWithGlobalAndPathRoles(
            globalRoles: ["admin", "user"],
            pathRoles: ["admin"]); // admin is in global
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sec001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathAuthorizeRoleNotDefined);
        Assert.Null(sec001);
    }

    // ========== SEC002: Path authentication scheme not defined in global section ==========

    [Fact]
    public void Validate_PathSchemeNotDefinedInGlobal_ReportsSEC002()
    {
        // Arrange
        var yaml = CreateYamlWithGlobalAndPathSchemes(
            globalSchemes: ["Bearer", "ApiKey"],
            pathSchemes: ["Bearer", "OAuth2"]); // OAuth2 not in global
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sec002 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathAuthenticationSchemeNotDefined);
        Assert.NotNull(sec002);
        Assert.Contains("OAuth2", sec002.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PathSchemeDefinedInGlobal_NoSEC002()
    {
        // Arrange
        var yaml = CreateYamlWithGlobalAndPathSchemes(
            globalSchemes: ["Bearer", "ApiKey"],
            pathSchemes: ["Bearer"]); // Bearer is in global
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sec002 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathAuthenticationSchemeNotDefined);
        Assert.Null(sec002);
    }

    // ========== SEC003: Operation authorize role not defined in global section ==========

    [Fact]
    public void Validate_OperationRoleNotDefinedInGlobal_ReportsSEC003()
    {
        // Arrange
        var yaml = CreateYamlWithGlobalAndOperationRoles(
            globalRoles: ["admin", "user"],
            operationRoles: ["admin", "manager"]); // manager not in global
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sec003 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationAuthorizeRoleNotDefined);
        Assert.NotNull(sec003);
        Assert.Contains("manager", sec003.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_OperationRoleDefinedInGlobal_NoSEC003()
    {
        // Arrange
        var yaml = CreateYamlWithGlobalAndOperationRoles(
            globalRoles: ["admin", "user"],
            operationRoles: ["admin"]); // admin is in global
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sec003 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationAuthorizeRoleNotDefined);
        Assert.Null(sec003);
    }

    // ========== SEC004: Operation authentication scheme not defined in global section ==========

    [Fact]
    public void Validate_OperationSchemeNotDefinedInGlobal_ReportsSEC004()
    {
        // Arrange
        var yaml = CreateYamlWithGlobalAndOperationSchemes(
            globalSchemes: ["Bearer", "ApiKey"],
            operationSchemes: ["Bearer", "Basic"]); // Basic not in global
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sec004 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationAuthenticationSchemeNotDefined);
        Assert.NotNull(sec004);
        Assert.Contains("Basic", sec004.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_OperationSchemeDefinedInGlobal_NoSEC004()
    {
        // Arrange
        var yaml = CreateYamlWithGlobalAndOperationSchemes(
            globalSchemes: ["Bearer", "ApiKey"],
            operationSchemes: ["Bearer"]); // Bearer is in global
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sec004 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationAuthenticationSchemeNotDefined);
        Assert.Null(sec004);
    }

    // ========== SEC005: Operation has authenticationRequired=false but has roles/schemes ==========

    [Fact]
    public void Validate_OperationAuthNotRequiredButHasRoles_ReportsSEC005()
    {
        // Arrange
        var yaml = CreateYamlWithOperationAuthConflict(
            authenticationRequired: false,
            operationRoles: ["admin"]);
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sec005 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationAuthenticationConflict);
        Assert.NotNull(sec005);
        Assert.Contains("x-authentication-required", sec005.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_OperationAuthRequiredWithRoles_NoSEC005()
    {
        // Arrange
        var yaml = CreateYamlWithOperationAuthConflict(
            authenticationRequired: true,
            operationRoles: ["admin"]);
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sec005 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationAuthenticationConflict);
        Assert.Null(sec005);
    }

    // ========== SEC006: Operation authorize role has incorrect casing vs global section ==========

    [Fact]
    public void Validate_OperationRoleIncorrectCasing_ReportsSEC006()
    {
        // Arrange
        var yaml = CreateYamlWithGlobalAndOperationRoles(
            globalRoles: ["Admin", "User"],
            operationRoles: ["admin"]); // admin vs Admin - different casing
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sec006 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationAuthorizeRoleCasing);
        Assert.NotNull(sec006);
        Assert.Contains("admin", sec006.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_OperationRoleCorrectCasing_NoSEC006()
    {
        // Arrange
        var yaml = CreateYamlWithGlobalAndOperationRoles(
            globalRoles: ["Admin", "User"],
            operationRoles: ["Admin"]); // Same casing
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sec006 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationAuthorizeRoleCasing);
        Assert.Null(sec006);
    }

    // ========== SEC007: Operation authentication scheme has incorrect casing vs global section ==========

    [Fact]
    public void Validate_OperationSchemeIncorrectCasing_ReportsSEC007()
    {
        // Arrange
        var yaml = CreateYamlWithGlobalAndOperationSchemes(
            globalSchemes: ["Bearer", "ApiKey"],
            operationSchemes: ["bearer"]); // bearer vs Bearer - different casing
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sec007 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationAuthenticationSchemeCasing);
        Assert.NotNull(sec007);
        Assert.Contains("bearer", sec007.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_OperationSchemeCorrectCasing_NoSEC007()
    {
        // Arrange
        var yaml = CreateYamlWithGlobalAndOperationSchemes(
            globalSchemes: ["Bearer", "ApiKey"],
            operationSchemes: ["Bearer"]); // Same casing
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sec007 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationAuthenticationSchemeCasing);
        Assert.Null(sec007);
    }

    // ========== SEC008: Path authorize role has incorrect casing vs global section ==========

    [Fact]
    public void Validate_PathRoleIncorrectCasing_ReportsSEC008()
    {
        // Arrange
        var yaml = CreateYamlWithGlobalAndPathRoles(
            globalRoles: ["Admin", "User"],
            pathRoles: ["admin"]); // admin vs Admin - different casing
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sec008 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathAuthorizeRoleCasing);
        Assert.NotNull(sec008);
        Assert.Contains("admin", sec008.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PathRoleCorrectCasing_NoSEC008()
    {
        // Arrange
        var yaml = CreateYamlWithGlobalAndPathRoles(
            globalRoles: ["Admin", "User"],
            pathRoles: ["Admin"]); // Same casing
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sec008 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathAuthorizeRoleCasing);
        Assert.Null(sec008);
    }

    // ========== SEC009: Path authentication scheme has incorrect casing vs global section ==========

    [Fact]
    public void Validate_PathSchemeIncorrectCasing_ReportsSEC009()
    {
        // Arrange
        var yaml = CreateYamlWithGlobalAndPathSchemes(
            globalSchemes: ["Bearer", "ApiKey"],
            pathSchemes: ["bearer"]); // bearer vs Bearer - different casing
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sec009 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathAuthenticationSchemeCasing);
        Assert.NotNull(sec009);
        Assert.Contains("bearer", sec009.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PathSchemeCorrectCasing_NoSEC009()
    {
        // Arrange
        var yaml = CreateYamlWithGlobalAndPathSchemes(
            globalSchemes: ["Bearer", "ApiKey"],
            pathSchemes: ["Bearer"]); // Same casing
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sec009 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathAuthenticationSchemeCasing);
        Assert.Null(sec009);
    }

    // ========== SEC010: Path has authenticationRequired=false but has roles/schemes ==========

    [Fact]
    public void Validate_PathAuthNotRequiredButHasRoles_ReportsSEC010()
    {
        // Arrange
        var yaml = CreateYamlWithPathAuthConflict(
            authenticationRequired: false,
            pathRoles: ["admin"]);
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sec010 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathAuthenticationConflict);
        Assert.NotNull(sec010);
        Assert.Contains("x-authentication-required", sec010.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PathAuthRequiredWithRoles_NoSEC010()
    {
        // Arrange
        var yaml = CreateYamlWithPathAuthConflict(
            authenticationRequired: true,
            pathRoles: ["admin"]);
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var sec010 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathAuthenticationConflict);
        Assert.Null(sec010);
    }

    // ========== Helper Methods ==========

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, TestFilePath, out var document)
            ? document
            : null;

    private static string FormatArrayExtension(string[] values)
    {
        if (values.Length == 0)
        {
            return "[]";
        }

        return $"[{string.Join(", ", values.Select(v => $"\"{v}\""))}]";
    }

    private static string CreateYamlWithGlobalAndPathRoles(
        string[] globalRoles,
        string[] pathRoles)
        => $"""
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            x-authorize-roles: {FormatArrayExtension(globalRoles)}
            paths:
              /pets:
                x-authorize-roles: {FormatArrayExtension(pathRoles)}
                get:
                  operationId: listPets
                  responses:
                    '200':
                      description: Success
            """;

    private static string CreateYamlWithGlobalAndPathSchemes(
        string[] globalSchemes,
        string[] pathSchemes)
        => $"""
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            x-authentication-schemes: {FormatArrayExtension(globalSchemes)}
            paths:
              /pets:
                x-authentication-schemes: {FormatArrayExtension(pathSchemes)}
                get:
                  operationId: listPets
                  responses:
                    '200':
                      description: Success
            """;

    private static string CreateYamlWithGlobalAndOperationRoles(
        string[] globalRoles,
        string[] operationRoles)
        => $"""
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            x-authorize-roles: {FormatArrayExtension(globalRoles)}
            paths:
              /pets:
                get:
                  operationId: listPets
                  x-authorize-roles: {FormatArrayExtension(operationRoles)}
                  responses:
                    '200':
                      description: Success
            """;

    private static string CreateYamlWithGlobalAndOperationSchemes(
        string[] globalSchemes,
        string[] operationSchemes)
        => $"""
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            x-authentication-schemes: {FormatArrayExtension(globalSchemes)}
            paths:
              /pets:
                get:
                  operationId: listPets
                  x-authentication-schemes: {FormatArrayExtension(operationSchemes)}
                  responses:
                    '200':
                      description: Success
            """;

    private static string CreateYamlWithOperationAuthConflict(
        bool authenticationRequired,
        string[] operationRoles)
        => $"""
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            x-authorize-roles: {FormatArrayExtension(operationRoles)}
            paths:
              /pets:
                get:
                  operationId: listPets
                  x-authentication-required: {authenticationRequired.ToString().ToLowerInvariant()}
                  x-authorize-roles: {FormatArrayExtension(operationRoles)}
                  responses:
                    '200':
                      description: Success
            """;

    private static string CreateYamlWithPathAuthConflict(
        bool authenticationRequired,
        string[] pathRoles)
        => $"""
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            x-authorize-roles: {FormatArrayExtension(pathRoles)}
            paths:
              /pets:
                x-authentication-required: {authenticationRequired.ToString().ToLowerInvariant()}
                x-authorize-roles: {FormatArrayExtension(pathRoles)}
                get:
                  operationId: listPets
                  responses:
                    '200':
                      description: Success
            """;
}