namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// Two operations whose operationIds become one C# name (<c>getUser</c> and <c>get_user</c>, <c>GetUSER</c>) would
/// be one handler interface, one result class and one client method: an error (<c>ATC_API_OPR032</c>) under every
/// strategy, naming both operations. Distinct names, and the same name on different paths that stay distinct, pass.
/// </summary>
public class OperationIdCollisionValidatorTests
{
    private const string TestFilePath = "collisions.yaml";

    private const string Head = """
        openapi: 3.0.3
        info: { title: Contoso, version: 1.0.0 }
        paths:

        """;

    public static TheoryData<string, string> Collisions
        => new()
        {
            { "getUser", "get_user" },
            { "getUser", "GetUSER" },
            { "getUser", "get-user" },
        };

    [Theory]
    [MemberData(nameof(Collisions))]
    public void Validate_TwoOperationIdsThatBecomeOneName_IsOneErrorNamingBoth(
        string first,
        string second)
    {
        var document = Parse(
            $$"""
              /users:
                get:
                  operationId: {{first}}
                  responses:
                    '200': { description: OK }
              /users/{id}:
                get:
                  operationId: {{second}}
                  responses:
                    '200': { description: OK }
            """);

        foreach (var strategy in (ValidateSpecificationStrategy[])[ValidateSpecificationStrategy.None, ValidateSpecificationStrategy.Standard, ValidateSpecificationStrategy.Strict])
        {
            var diagnostics = OpenApiDocumentValidator.Validate(strategy, document, [], TestFilePath);

            var error = Assert.Single(diagnostics, d => d.RuleId == RuleIdentifiers.OperationIdCollision);
            Assert.Equal(DiagnosticSeverity.Error, error.Severity);
            Assert.Contains(first, error.Message, StringComparison.Ordinal);
            Assert.Contains(second, error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Validate_DistinctOperationIds_IsNoCollision()
    {
        var document = Parse(
            """
              /users:
                get:
                  operationId: listUsers
                  responses:
                    '200': { description: OK }
                post:
                  operationId: createUser
                  responses:
                    '201': { description: Created }
            """);

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Strict, document, [], TestFilePath);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.OperationIdCollision);
    }

    private static OpenApiDocument Parse(string paths)
    {
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(Head + paths, TestFilePath + paths.Length);
        Assert.NotNull(document);
        return document;
    }
}