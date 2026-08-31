namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// Tests for <see cref="SingleClientCollisionValidator"/>, which enforces rule
/// <c>ATC_API_CLT001</c>.
/// </summary>
/// <remarks>
/// Under <see cref="ClientGranularityType.PerArea"/> the per-area namespaces mask collisions between
/// schema keys that normalise to the same C# identifier. Flattening models into
/// <c>{root}.Generated.Models</c> for <see cref="ClientGranularityType.Single"/> exposes them as an
/// opaque CS0101 inside generated code the user cannot edit, hence this rule.
/// <para>
/// The PerArea test cases are load-bearing for backward compatibility: no existing project may newly
/// fail validation.
/// </para>
/// </remarks>
public class SingleClientCollisionValidatorTests
{
    // ========== Collisions that must be detected ==========

    /// <summary>
    /// Distinct raw keys that differ only by separator normalise to one identifier.
    /// </summary>
    [Fact]
    public void Validate_SeparatorVariants_RaiseCollision()
    {
        // Arrange
        var document = CreateDocumentWithSchemas("pet-status", "pet_status");

        // Act
        var diagnostics = SingleClientCollisionValidator.Validate(
            document,
            ClientGranularityType.Single,
            filePath: "spec.yaml");

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(RuleIdentifiers.SingleClientTypeNameCollision, diagnostic.RuleId);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("PetStatus", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("pet-status", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("pet_status", diagnostic.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Case-only differences are real collisions because the comparison of generated names is
    /// ordinal and both keys pascal-case to the same identifier.
    /// </summary>
    [Fact]
    public void Validate_CaseOnlyVariants_RaiseCollision()
    {
        // Arrange
        var document = CreateDocumentWithSchemas("petStatus", "PetStatus");

        // Act
        var diagnostics = SingleClientCollisionValidator.Validate(
            document,
            ClientGranularityType.Single,
            filePath: "spec.yaml");

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(RuleIdentifiers.SingleClientTypeNameCollision, diagnostic.RuleId);
        Assert.Contains("PetStatus", diagnostic.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Three contributors to one name must still produce exactly one diagnostic, not three,
    /// to avoid diagnostic spam.
    /// </summary>
    [Fact]
    public void Validate_ThreeContributors_ReportOnceForTheName()
    {
        // Arrange
        var document = CreateDocumentWithSchemas("pet-status", "pet_status", "PetStatus");

        // Act
        var diagnostics = SingleClientCollisionValidator.Validate(
            document,
            ClientGranularityType.Single,
            filePath: "spec.yaml");

        // Assert
        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("PetStatus", diagnostic.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Two independent collisions produce two diagnostics.
    /// </summary>
    [Fact]
    public void Validate_TwoDistinctCollisions_ReportBoth()
    {
        // Arrange
        var document = CreateDocumentWithSchemas("pet-status", "pet_status", "order-item", "order_item");

        // Act
        var diagnostics = SingleClientCollisionValidator.Validate(
            document,
            ClientGranularityType.Single,
            filePath: "spec.yaml");

        // Assert
        Assert.Equal(2, diagnostics.Count);
        Assert.All(diagnostics, d => Assert.Equal(RuleIdentifiers.SingleClientTypeNameCollision, d.RuleId));
    }

    // ========== Cases that must NOT raise ==========
    [Fact]
    public void Validate_CleanSpec_RaisesNothing()
    {
        // Arrange
        var document = CreateDocumentWithSchemas("Pet", "Order", "Customer");

        // Act
        var diagnostics = SingleClientCollisionValidator.Validate(
            document,
            ClientGranularityType.Single,
            filePath: "spec.yaml");

        // Assert
        Assert.Empty(diagnostics);
    }

    /// <summary>
    /// BACKWARD COMPATIBILITY GUARD: the identical colliding spec must not raise under PerArea,
    /// so no existing project can newly fail.
    /// </summary>
    [Fact]
    public void Validate_CollidingSpecUnderPerArea_RaisesNothing()
    {
        // Arrange
        var document = CreateDocumentWithSchemas("pet-status", "pet_status");

        // Act
        var diagnostics = SingleClientCollisionValidator.Validate(
            document,
            ClientGranularityType.PerArea,
            filePath: "spec.yaml");

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Validate_NoSchemas_RaisesNothing()
    {
        // Arrange
        var document = new OpenApiDocument();

        // Act
        var diagnostics = SingleClientCollisionValidator.Validate(
            document,
            ClientGranularityType.Single,
            filePath: "spec.yaml");

        // Assert
        Assert.Empty(diagnostics);
    }

    // ========== Diagnostic quality ==========
    [Fact]
    public void Validate_Collision_IncludesActionableSuggestions()
    {
        // Arrange
        var document = CreateDocumentWithSchemas("pet-status", "pet_status");

        // Act
        var diagnostic = Assert.Single(SingleClientCollisionValidator.Validate(
            document,
            ClientGranularityType.Single,
            filePath: "spec.yaml"));

        // Assert
        Assert.NotNull(diagnostic.Suggestions);
        Assert.Contains(
            diagnostic.Suggestions!,
            s => s.Contains("PerArea", StringComparison.Ordinal));
        Assert.Equal("spec.yaml", diagnostic.FilePath);
    }

    private static OpenApiDocument CreateDocumentWithSchemas(
        params string[] schemaNames)
    {
        var schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);

        foreach (var schemaName in schemaNames)
        {
            schemas[schemaName] = new OpenApiSchema { Type = JsonSchemaType.String };
        }

        return new OpenApiDocument
        {
            Components = new OpenApiComponents
            {
                Schemas = schemas,
            },
        };
    }
}