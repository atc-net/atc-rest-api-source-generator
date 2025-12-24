namespace Atc.Rest.Api.SourceGenerator.Tests.Validators;

/// <summary>
/// Tests for Operation validation rules (OPR001-OPR018).
/// </summary>
[SuppressMessage("", "SA1512:Single-line comments should not be followed by blank line", Justification = "OK")]
[SuppressMessage("", "SA1515:Single-line comment should be preceded by blank line", Justification = "OK")]
public class OperationValidationTests
{
    private const string TestFilePath = "test.yaml";

    // ========== OPR001: Missing operationId ==========

    [Fact]
    public void Validate_MissingOperationId_ReportsOPR001()
    {
        // Arrange
        var document = ParseYaml(CreateOperationYaml(operationId: null));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationIdMissing);
        Assert.NotNull(opr001);
        Assert.Contains("Missing operationId", opr001.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_HasOperationId_NoOPR001()
    {
        // Arrange
        var document = ParseYaml(CreateOperationYaml(operationId: "listPets"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationIdMissing);
        Assert.Null(opr001);
    }

    // ========== OPR002: OperationId not using valid casing style ==========

    [Fact]
    public void Validate_OperationIdSnakeCase_ReportsOPR002()
    {
        // Arrange
        var document = ParseYaml(CreateOperationYaml(operationId: "list_pets"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr002 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationIdCasing);
        Assert.NotNull(opr002);
        Assert.Contains("list_pets", opr002.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_OperationIdCamelCase_NoOPR002()
    {
        // Arrange
        var document = ParseYaml(CreateOperationYaml(operationId: "listPets"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr002 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationIdCasing);
        Assert.Null(opr002);
    }

    [Fact]
    public void Validate_OperationIdKebabCase_NoOPR002()
    {
        // Arrange
        var document = ParseYaml(CreateOperationYaml(operationId: "list-pets"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr002 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationIdCasing);
        Assert.Null(opr002);
    }

    // ========== OPR003: GET operationId should start with 'Get' ==========

    [Fact]
    public void Validate_GetOperationIdWithoutGetPrefix_ReportsOPR003()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithMethodYaml("get", "findPets"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr003 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.GetOperationIdPrefix);
        Assert.NotNull(opr003);
        Assert.Contains("findPets", opr003.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_GetOperationIdWithGetPrefix_NoOPR003()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithMethodYaml("get", "getPets"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr003 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.GetOperationIdPrefix);
        Assert.Null(opr003);
    }

    [Fact]
    public void Validate_GetOperationIdWithListPrefix_NoOPR003()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithMethodYaml("get", "listPets"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr003 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.GetOperationIdPrefix);
        Assert.Null(opr003);
    }

    // ========== OPR004: POST operationId should NOT start with 'Delete' ==========

    [Fact]
    public void Validate_PostOperationIdStartsWithDelete_ReportsOPR004()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithMethodYaml("post", "deletePet"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr004 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PostOperationIdPrefix);
        Assert.NotNull(opr004);
        Assert.Contains("deletePet", opr004.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PostOperationIdWithCreatePrefix_NoOPR004()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithMethodYaml("post", "createPet"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr004 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PostOperationIdPrefix);
        Assert.Null(opr004);
    }

    [Fact]
    public void Validate_PostOperationIdWithAddPrefix_NoOPR004()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithMethodYaml("post", "addPet"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr004 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PostOperationIdPrefix);
        Assert.Null(opr004);
    }

    // ========== OPR005: PUT operationId should start with 'Update' ==========

    [Fact]
    public void Validate_PutOperationIdWithoutUpdatePrefix_ReportsOPR005()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithMethodAndPathYaml("put", "modifyPet", "/pets/{petId}"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr005 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PutOperationIdPrefix);
        Assert.NotNull(opr005);
        Assert.Contains("modifyPet", opr005.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PutOperationIdWithUpdatePrefix_NoOPR005()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithMethodAndPathYaml("put", "updatePet", "/pets/{petId}"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr005 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PutOperationIdPrefix);
        Assert.Null(opr005);
    }

    // ========== OPR006: PATCH operationId should start with 'Patch' or 'Update' ==========

    [Fact]
    public void Validate_PatchOperationIdWithoutPatchPrefix_ReportsOPR006()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithMethodAndPathYaml("patch", "modifyPet", "/pets/{petId}"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr006 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PatchOperationIdPrefix);
        Assert.NotNull(opr006);
        Assert.Contains("modifyPet", opr006.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PatchOperationIdWithPatchPrefix_NoOPR006()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithMethodAndPathYaml("patch", "patchPet", "/pets/{petId}"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr006 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PatchOperationIdPrefix);
        Assert.Null(opr006);
    }

    [Fact]
    public void Validate_PatchOperationIdWithUpdatePrefix_NoOPR006()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithMethodAndPathYaml("patch", "updatePet", "/pets/{petId}"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr006 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PatchOperationIdPrefix);
        Assert.Null(opr006);
    }

    // ========== OPR007: DELETE operationId should start with 'Delete' or 'Remove' ==========

    [Fact]
    public void Validate_DeleteOperationIdWithoutDeletePrefix_ReportsOPR007()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithMethodAndPathYaml("delete", "erasePet", "/pets/{petId}"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr007 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.DeleteOperationIdPrefix);
        Assert.NotNull(opr007);
        Assert.Contains("erasePet", opr007.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_DeleteOperationIdWithDeletePrefix_NoOPR007()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithMethodAndPathYaml("delete", "deletePet", "/pets/{petId}"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr007 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.DeleteOperationIdPrefix);
        Assert.Null(opr007);
    }

    [Fact]
    public void Validate_DeleteOperationIdWithRemovePrefix_NoOPR007()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithMethodAndPathYaml("delete", "removePet", "/pets/{petId}"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr007 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.DeleteOperationIdPrefix);
        Assert.Null(opr007);
    }

    // ========== OPR008: Pluralized operationId but response is single item ==========

    [Fact]
    public void Validate_PluralOperationIdReturnsSingleObject_ReportsOPR008()
    {
        // Arrange - plural name (getPets) but returns single object
        var document = ParseYaml(CreateOperationWithObjectResponseYaml("getPets"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr008 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationIdPluralizationMismatch);
        Assert.NotNull(opr008);
        Assert.Contains("getPets", opr008.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_SingularOperationIdReturnsSingleObject_NoOPR008()
    {
        // Arrange - singular name (getPet) returns single object - no mismatch
        var document = ParseYaml(CreateOperationWithObjectResponseYaml("getPet"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr008 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationIdPluralizationMismatch);
        Assert.Null(opr008);
    }

    // ========== OPR009: Singular operationId but response is array ==========

    [Fact]
    public void Validate_SingularOperationIdReturnsArray_ReportsOPR009()
    {
        // Arrange - singular name (getPet) but returns array
        var document = ParseYaml(CreateOperationWithArrayResponseYaml("getPet"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr009 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationIdSingularMismatch);
        Assert.NotNull(opr009);
        Assert.Contains("getPet", opr009.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PluralOperationIdReturnsArray_NoOPR009()
    {
        // Arrange - plural name (getPets) returns array - no mismatch
        var document = ParseYaml(CreateOperationWithArrayResponseYaml("getPets"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr009 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationIdSingularMismatch);
        Assert.Null(opr009);
    }

    // ========== OPR010: BadRequest without parameters ==========

    [Fact]
    public void Validate_BadRequestResponseWithoutParameters_ReportsOPR010()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithBadRequestYaml(hasParameters: false));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr010 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.BadRequestWithoutParameters);
        Assert.NotNull(opr010);
        Assert.Contains("getPets", opr010.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_BadRequestResponseWithParameters_NoOPR010()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithBadRequestYaml(hasParameters: true));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr010 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.BadRequestWithoutParameters);
        Assert.Null(opr010);
    }

    // ========== OPR011: Global path parameter not in route ==========

    [Fact]
    public void Validate_GlobalPathParameterNotInRoute_ReportsOPR011()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithGlobalPathParameterYaml(
            pathParamName: "userId",
            routePath: "/pets"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr011 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.GlobalPathParameterNotInRoute);
        Assert.NotNull(opr011);
        Assert.Contains("userId", opr011.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_GlobalPathParameterInRoute_NoOPR011()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithGlobalPathParameterYaml(
            pathParamName: "petId",
            routePath: "/pets/{petId}"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr011 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.GlobalPathParameterNotInRoute);
        Assert.Null(opr011);
    }

    // ========== OPR012: Operation missing path parameter defined in route ==========

    [Fact]
    public void Validate_OperationMissingPathParameter_ReportsOPR012()
    {
        // Arrange
        var document = ParseYaml(CreateOperationMissingPathParameterYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr012 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationMissingPathParameter);
        Assert.NotNull(opr012);
        Assert.Contains("petId", opr012.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_OperationHasAllPathParameters_NoOPR012()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithAllPathParametersYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr012 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationMissingPathParameter);
        Assert.Null(opr012);
    }

    // ========== OPR013: Operation path parameter not in route ==========

    [Fact]
    public void Validate_OperationPathParameterNotInRoute_ReportsOPR013()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithExtraPathParameterYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr013 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationPathParameterNotInRoute);
        Assert.NotNull(opr013);
        Assert.Contains("orderId", opr013.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_OperationPathParameterInRoute_NoOPR013()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithAllPathParametersYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr013 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.OperationPathParameterNotInRoute);
        Assert.Null(opr013);
    }

    // ========== OPR014: GET operation with path parameter missing 404 response ==========

    [Fact]
    public void Validate_GetWithPathParameterMissing404_ReportsOPR014()
    {
        // Arrange
        var document = ParseYaml(CreateGetOperationMissing404Yaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr014 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.GetMissingNotFoundResponse);
        Assert.NotNull(opr014);
        Assert.Contains("getPetById", opr014.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_GetWithPathParameterHas404_NoOPR014()
    {
        // Arrange
        var document = ParseYaml(CreateGetOperationWith404Yaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr014 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.GetMissingNotFoundResponse);
        Assert.Null(opr014);
    }

    // ========== OPR015: Path parameter not marked as required ==========

    [Fact]
    public void Validate_PathParameterNotRequired_ReportsOPR015()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithNonRequiredPathParameterYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr015 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathParameterNotRequired);
        Assert.NotNull(opr015);
        Assert.Contains("petId", opr015.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PathParameterMarkedRequired_NoOPR015()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithAllPathParametersYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr015 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathParameterNotRequired);
        Assert.Null(opr015);
    }

    // ========== OPR016: Path parameter marked as nullable ==========

    [Fact]
    public void Validate_PathParameterNullable_ReportsOPR016()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithNullablePathParameterYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr016 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathParameterNullable);
        Assert.NotNull(opr016);
        Assert.Contains("petId", opr016.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PathParameterNotNullable_NoOPR016()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithAllPathParametersYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr016 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathParameterNullable);
        Assert.Null(opr016);
    }

    // ========== OPR017: RequestBody with inline model ==========

    [Fact]
    public void Validate_RequestBodyWithInlineModel_ReportsOPR017()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithInlineRequestBodyYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr017 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RequestBodyInlineModel);
        Assert.NotNull(opr017);
        Assert.Contains("createPet", opr017.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RequestBodyWithSchemaReference_NoOPR017()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithReferencedRequestBodyYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr017 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RequestBodyInlineModel);
        Assert.Null(opr017);
    }

    // ========== OPR018: Multiple 2xx status codes ==========

    [Fact]
    public void Validate_Multiple2xxStatusCodes_ReportsOPR018()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithMultiple2xxResponsesYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr018 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.Multiple2xxStatusCodes);
        Assert.NotNull(opr018);
        Assert.Contains("createPet", opr018.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Single2xxStatusCode_NoOPR018()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWithMethodYaml("post", "createPet"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr018 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.Multiple2xxStatusCodes);
        Assert.Null(opr018);
    }

    // ========== OPR021: 401 Unauthorized without security requirements ==========

    [Fact]
    public void Validate_401WithoutSecurity_ReportsOPR021()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWith401WithoutSecurityYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr021 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.UnauthorizedWithoutSecurity);
        Assert.NotNull(opr021);
        Assert.Contains("getPets", opr021.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_401WithSecurity_NoOPR021()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWith401WithSecurityYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr021 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.UnauthorizedWithoutSecurity);
        Assert.Null(opr021);
    }

    // ========== OPR022: 403 Forbidden without authorization ==========

    [Fact]
    public void Validate_403WithoutAuthorization_ReportsOPR022()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWith403WithoutAuthorizationYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr022 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ForbiddenWithoutAuthorization);
        Assert.NotNull(opr022);
        Assert.Contains("getPets", opr022.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_403WithAuthorization_NoOPR022()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWith403WithAuthorizationYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr022 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ForbiddenWithoutAuthorization);
        Assert.Null(opr022);
    }

    // ========== OPR023: 404 NotFound on POST operation ==========

    [Fact]
    public void Validate_404OnPostOperation_ReportsOPR023()
    {
        // Arrange
        var document = ParseYaml(CreatePostOperationWith404Yaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr023 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.NotFoundOnPostOperation);
        Assert.NotNull(opr023);
        Assert.Contains("createPet", opr023.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_404OnGetOperation_NoOPR023()
    {
        // Arrange
        var document = ParseYaml(CreateGetOperationWith404Yaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr023 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.NotFoundOnPostOperation);
        Assert.Null(opr023);
    }

    // ========== OPR024: 409 Conflict on non-mutating operation ==========

    [Fact]
    public void Validate_409OnGetOperation_ReportsOPR024()
    {
        // Arrange
        var document = ParseYaml(CreateGetOperationWith409Yaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr024 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ConflictOnNonMutatingOperation);
        Assert.NotNull(opr024);
        Assert.Contains("getPets", opr024.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_409OnPostOperation_NoOPR024()
    {
        // Arrange
        var document = ParseYaml(CreatePostOperationWith409Yaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr024 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.ConflictOnNonMutatingOperation);
        Assert.Null(opr024);
    }

    // ========== OPR025: 429 TooManyRequests without rate limiting ==========

    [Fact]
    public void Validate_429WithoutRateLimiting_ReportsOPR025()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWith429WithoutRateLimitingYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr025 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.TooManyRequestsWithoutRateLimiting);
        Assert.NotNull(opr025);
        Assert.Contains("getPets", opr025.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_429WithRateLimiting_NoOPR025()
    {
        // Arrange
        var document = ParseYaml(CreateOperationWith429WithRateLimitingYaml());
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var opr025 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.TooManyRequestsWithoutRateLimiting);
        Assert.Null(opr025);
    }

    // ========== Helper Methods ==========

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, TestFilePath, out var document)
            ? document
            : null;

    private static string CreateOperationYaml(string? operationId)
    {
        var operationIdLine = operationId != null ? $"operationId: {operationId}" : string.Empty;
        return $$"""

                 openapi: 3.0.0
                 info:
                   title: Test API
                   version: 1.0.0
                 paths:
                   /pets:
                     get:
                       {{operationIdLine}}
                       responses:
                         '200':
                           description: Success

                 """;
    }

    private static string CreateOperationWithMethodYaml(
        string httpMethod,
        string operationId)
        => $$"""

             openapi: 3.0.0
             info:
               title: Test API
               version: 1.0.0
             paths:
               /pets:
                 {{httpMethod}}:
                   operationId: {{operationId}}
                   responses:
                     '200':
                       description: Success

             """;

    private static string CreateOperationWithMethodAndPathYaml(
        string httpMethod,
        string operationId,
        string path)
        => $$"""

             openapi: 3.0.0
             info:
               title: Test API
               version: 1.0.0
             paths:
               {{path}}:
                 {{httpMethod}}:
                   operationId: {{operationId}}
                   parameters:
                     - name: petId
                       in: path
                       required: true
                       schema:
                         type: string
                   responses:
                     '200':
                       description: Success

             """;

    private static string CreateOperationWithArrayResponseYaml(
        string operationId)
        => $$"""

             openapi: 3.0.0
             info:
               title: Test API
               version: 1.0.0
             paths:
               /pets:
                 get:
                   operationId: {{operationId}}
                   responses:
                     '200':
                       description: Success
                       content:
                         application/json:
                           schema:
                             type: array
                             items:
                               type: string

             """;

    private static string CreateOperationWithObjectResponseYaml(
        string operationId)
        => $$"""

             openapi: 3.0.0
             info:
               title: Test API
               version: 1.0.0
             paths:
               /pets:
                 get:
                   operationId: {{operationId}}
                   responses:
                     '200':
                       description: Success
                       content:
                         application/json:
                           schema:
                             type: object
                             properties:
                               id:
                                 type: string

             """;

    private static string CreateOperationWithBadRequestYaml(bool hasParameters)
    {
        var parametersSection = hasParameters
            ? """
              parameters:
                      - name: limit
                        in: query
                        schema:
                          type: integer
              """
            : string.Empty;

        return $$"""

                 openapi: 3.0.0
                 info:
                   title: Test API
                   version: 1.0.0
                 paths:
                   /pets:
                     get:
                       operationId: getPets
                       {{parametersSection}}
                       responses:
                         '200':
                           description: Success
                         '400':
                           description: Bad request

                 """;
    }

    private static string CreateOperationWithGlobalPathParameterYaml(
        string pathParamName,
        string routePath)
        => $$"""

             openapi: 3.0.0
             info:
               title: Test API
               version: 1.0.0
             paths:
               {{routePath}}:
                 parameters:
                   - name: {{pathParamName}}
                     in: path
                     required: true
                     schema:
                       type: string
                 get:
                   operationId: getPets
                   responses:
                     '200':
                       description: Success

             """;

    private static string CreateOperationMissingPathParameterYaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths:
             /pets/{petId}:
               get:
                 operationId: getPetById
                 responses:
                   '200':
                     description: Success

           """;

    private static string CreateOperationWithAllPathParametersYaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths:
             /pets/{petId}:
               get:
                 operationId: getPetById
                 parameters:
                   - name: petId
                     in: path
                     required: true
                     schema:
                       type: string
                 responses:
                   '200':
                     description: Success

           """;

    private static string CreateOperationWithExtraPathParameterYaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths:
             /pets/{petId}:
               get:
                 operationId: getPetById
                 parameters:
                   - name: petId
                     in: path
                     required: true
                     schema:
                       type: string
                   - name: orderId
                     in: path
                     required: true
                     schema:
                       type: string
                 responses:
                   '200':
                     description: Success

           """;

    private static string CreateGetOperationMissing404Yaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths:
             /pets/{petId}:
               get:
                 operationId: getPetById
                 parameters:
                   - name: petId
                     in: path
                     required: true
                     schema:
                       type: string
                 responses:
                   '200':
                     description: Success

           """;

    private static string CreateGetOperationWith404Yaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths:
             /pets/{petId}:
               get:
                 operationId: getPetById
                 parameters:
                   - name: petId
                     in: path
                     required: true
                     schema:
                       type: string
                 responses:
                   '200':
                     description: Success
                   '404':
                     description: Not found

           """;

    private static string CreateOperationWithNonRequiredPathParameterYaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths:
             /pets/{petId}:
               get:
                 operationId: getPetById
                 parameters:
                   - name: petId
                     in: path
                     required: false
                     schema:
                       type: string
                 responses:
                   '200':
                     description: Success

           """;

    private static string CreateOperationWithNullablePathParameterYaml()
        => """

           openapi: 3.1.0
           info:
             title: Test API
             version: 1.0.0
           paths:
             /pets/{petId}:
               get:
                 operationId: getPetById
                 parameters:
                   - name: petId
                     in: path
                     required: true
                     schema:
                       type:
                         - string
                         - 'null'
                 responses:
                   '200':
                     description: Success

           """;

    private static string CreateOperationWithInlineRequestBodyYaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths:
             /pets:
               post:
                 operationId: createPet
                 requestBody:
                   content:
                     application/json:
                       schema:
                         type: object
                         properties:
                           name:
                             type: string
                           age:
                             type: integer
                 responses:
                   '201':
                     description: Created

           """;

    private static string CreateOperationWithReferencedRequestBodyYaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths:
             /pets:
               post:
                 operationId: createPet
                 requestBody:
                   content:
                     application/json:
                       schema:
                         $ref: '#/components/schemas/Pet'
                 responses:
                   '201':
                     description: Created
           components:
             schemas:
               Pet:
                 type: object
                 title: Pet
                 properties:
                   name:
                     type: string

           """;

    private static string CreateOperationWithMultiple2xxResponsesYaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths:
             /pets:
               post:
                 operationId: createPet
                 requestBody:
                   content:
                     application/json:
                       schema:
                         $ref: '#/components/schemas/Pet'
                 responses:
                   '200':
                     description: Success
                   '201':
                     description: Created
           components:
             schemas:
               Pet:
                 type: object
                 title: Pet
                 properties:
                   name:
                     type: string

           """;

    private static string CreateOperationWith401WithoutSecurityYaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths:
             /pets:
               get:
                 operationId: getPets
                 responses:
                   '200':
                     description: Success
                   '401':
                     description: Unauthorized

           """;

    private static string CreateOperationWith401WithSecurityYaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths:
             /pets:
               get:
                 operationId: getPets
                 security:
                   - bearerAuth: []
                 responses:
                   '200':
                     description: Success
                   '401':
                     description: Unauthorized
           components:
             securitySchemes:
               bearerAuth:
                 type: http
                 scheme: bearer

           """;

    private static string CreateOperationWith403WithoutAuthorizationYaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths:
             /pets:
               get:
                 operationId: getPets
                 responses:
                   '200':
                     description: Success
                   '403':
                     description: Forbidden

           """;

    private static string CreateOperationWith403WithAuthorizationYaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           x-authorize-roles: ["admin"]
           paths:
             /pets:
               get:
                 operationId: getPets
                 x-authorize-roles: ["admin"]
                 responses:
                   '200':
                     description: Success
                   '403':
                     description: Forbidden

           """;

    private static string CreatePostOperationWith404Yaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths:
             /pets:
               post:
                 operationId: createPet
                 requestBody:
                   content:
                     application/json:
                       schema:
                         $ref: '#/components/schemas/Pet'
                 responses:
                   '201':
                     description: Created
                   '404':
                     description: Not found
           components:
             schemas:
               Pet:
                 type: object
                 title: Pet
                 properties:
                   name:
                     type: string

           """;

    private static string CreateGetOperationWith409Yaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths:
             /pets:
               get:
                 operationId: getPets
                 responses:
                   '200':
                     description: Success
                   '409':
                     description: Conflict

           """;

    private static string CreatePostOperationWith409Yaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths:
             /pets:
               post:
                 operationId: createPet
                 requestBody:
                   content:
                     application/json:
                       schema:
                         $ref: '#/components/schemas/Pet'
                 responses:
                   '201':
                     description: Created
                   '409':
                     description: Conflict
           components:
             schemas:
               Pet:
                 type: object
                 title: Pet
                 properties:
                   name:
                     type: string

           """;

    private static string CreateOperationWith429WithoutRateLimitingYaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths:
             /pets:
               get:
                 operationId: getPets
                 responses:
                   '200':
                     description: Success
                   '429':
                     description: Too many requests

           """;

    private static string CreateOperationWith429WithRateLimitingYaml()
        => """

           openapi: 3.0.0
           info:
             title: Test API
             version: 1.0.0
           paths:
             /pets:
               get:
                 operationId: getPets
                 x-ratelimit-policy: "fixed"
                 responses:
                   '200':
                     description: Success
                   '429':
                     description: Too many requests

           """;
}