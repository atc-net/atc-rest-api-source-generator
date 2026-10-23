namespace Atc.Rest.Api.Generator.Tests.Extractors;

/// <summary>
/// Keywords a specification may legally carry that the generator used to drop or mistype: exclusive bounds (the 3.0
/// boolean and the 3.1 number) become <c>[Range]</c> with the exclusive flags; an enum default is the member, not a
/// dropped default that made the property required; a default no C# constant can hold (a date, an array) leaves the
/// property optional and nullable instead of required.
/// </summary>
public sealed class HostileSchemaKeywordsTests
{
    [Theory]
    [InlineData("3.0.3", "type: integer\n      minimum: 1\n      exclusiveMinimum: true", "Range(1, long.MaxValue, MinimumIsExclusive = true, ErrorMessage = \"The field {0} must be greater than 1.\")")]
    [InlineData("3.0.3", "type: number\n      maximum: 10\n      exclusiveMaximum: true", "Range(double.MinValue, 10, MaximumIsExclusive = true, ErrorMessage = \"The field {0} must be less than 10.\")")]
    [InlineData("3.0.3", "type: integer\n      minimum: 0\n      exclusiveMinimum: true\n      maximum: 100\n      exclusiveMaximum: true", "Range(0, 100, MinimumIsExclusive = true, MaximumIsExclusive = true)")]
    [InlineData("3.1.0", "type: integer\n      exclusiveMinimum: 0\n      exclusiveMaximum: 100", "Range(0, 100, MinimumIsExclusive = true, MaximumIsExclusive = true)")]
    [InlineData("3.1.0", "type: number\n      exclusiveMinimum: 0.5", "Range(0.5, double.MaxValue, MinimumIsExclusive = true, ErrorMessage = \"The field {0} must be greater than 0.5.\")")]
    [InlineData("3.1.0", "type: integer\n      minimum: 1\n      maximum: 9", "Range(1, 9)")]

    // One bound: the message names it, not the type's far end ("between -9.22E+18 and 100")
    [InlineData("3.0.3", "type: integer\n      maximum: 100", "Range(long.MinValue, 100, ErrorMessage = \"The field {0} must be at most 100.\")")]
    [InlineData("3.0.3", "type: integer\n      minimum: 1", "Range(1, long.MaxValue, ErrorMessage = \"The field {0} must be at least 1.\")")]
    [InlineData("3.0.3", "type: number\n      minimum: 0.01", "Range(0.01, double.MaxValue, ErrorMessage = \"The field {0} must be at least 0.01.\")")]
    public void ExclusiveBounds_BecomeARangeWithTheExclusiveFlags(
        string version,
        string schema,
        string expected)
    {
        var document = Parse(version, $$"""
            Thing:
              type: object
              properties:
                value:
                  {{schema}}
            """);
        var value = ((OpenApiSchema)document.Components!.Schemas!["Thing"]).Properties!["value"];

        var attributes = value.GetValidationAttributes(isRequired: false);

        Assert.Equal([expected], attributes);
    }

    [Theory]
    [InlineData("color", "Color", "Color.Red")]
    [InlineData("priority", "ThingPriority", "ThingPriority.Value2")]
    [InlineData("kind", "ThingKind", "ThingKind.Bug")]
    [InlineData("count", "int", "5")]
    public void AnEnumOrConstantDefault_IsTheDefaultOfANonNullableProperty(
        string property,
        string typeName,
        string expected)
    {
        var record = ExtractThing();

        var parameter = Assert.Single(record.Parameters!, p => string.Equals(p.Name, Pascal(property), StringComparison.Ordinal));
        Assert.Equal(typeName, parameter.TypeName);
        Assert.False(parameter.IsNullableType);
        Assert.Equal(expected, parameter.DefaultValue);
    }

    [Theory]
    [InlineData("due", "DateOnly")]
    [InlineData("tags", "List<string>")]
    public void ADefaultNoConstantHolds_LeavesThePropertyOptionalAndNullable(
        string property,
        string typeName)
    {
        var record = ExtractThing();

        var parameter = Assert.Single(record.Parameters!, p => string.Equals(p.Name, Pascal(property), StringComparison.Ordinal));
        Assert.Equal(typeName, parameter.TypeName);
        Assert.True(parameter.IsNullableType);
        Assert.Equal("null", parameter.DefaultValue);
    }

    [Theory]
    [InlineData("color", "Color")]
    [InlineData("kind", "ThingKind")]
    public void AStringEnumDefault_LeavesAnExtensibleEnumClientsPropertyOptionalAndNullable(
        string property,
        string typeName)
    {
        // A generated client's string enum is a struct: its member is no compile-time constant that a parameter
        // default or a [DefaultValue] could carry, so the property is left out and null, as without a default
        var record = ExtractThing(ExtensibleEnumsRegistry());

        var parameter = Assert.Single(record.Parameters!, p => string.Equals(p.Name, Pascal(property), StringComparison.Ordinal));
        Assert.Equal(typeName, parameter.TypeName);
        Assert.True(parameter.IsNullableType);
        Assert.Equal("null", parameter.DefaultValue);
    }

    [Theory]
    [InlineData("priority", "ThingPriority", "ThingPriority.Value2")]
    [InlineData("count", "int", "5")]
    public void AnIntegerEnumOrConstantDefault_IsTheDefaultOnAnExtensibleEnumClientToo(
        string property,
        string typeName,
        string expected)
    {
        var record = ExtractThing(ExtensibleEnumsRegistry());

        var parameter = Assert.Single(record.Parameters!, p => string.Equals(p.Name, Pascal(property), StringComparison.Ordinal));
        Assert.Equal(typeName, parameter.TypeName);
        Assert.False(parameter.IsNullableType);
        Assert.Equal(expected, parameter.DefaultValue);
    }

    [Fact]
    public void AStringEnumParameterDefault_IsNoDefaultValueOnAnExtensibleEnumClient()
    {
        var document = Parse(
            "3.0.3",
            """
            Thing:
              type: object
            """,
            parameters: """
            parameters:
              - name: sortBy
                in: query
                schema:
                  type: string
                  enum: [name, price]
                  default: name
            """);

        var records = OperationParameterExtractor.ExtractIndividual(
            document,
            "Contoso",
            pathSegment: null,
            ExtensibleEnumsRegistry(),
            includeBindingAttributes: false);

        // An optional parameter without a default: nullable, null unless the caller names it
        var parameter = Assert.Single(Assert.Single(records!).Parameters!, p => string.Equals(p.Name, "SortBy", StringComparison.Ordinal));
        Assert.True(parameter.IsNullableType);
        Assert.Equal("null", parameter.DefaultValue);
        Assert.DoesNotContain(parameter.Attributes ?? [], a => string.Equals(a.Name, "DefaultValue", StringComparison.Ordinal));
    }

    private static TypeConflictRegistry ExtensibleEnumsRegistry()
        => TypeConflictRegistry.ForSegment(new HashSet<string>(StringComparer.Ordinal), "Contoso", extensibleEnums: true);

    private static RecordParameters ExtractThing(
        TypeConflictRegistry? registry = null)
    {
        var document = Parse("3.0.3", """
            Color:
              type: string
              enum: [red, dark-blue]
            Thing:
              type: object
              properties:
                color:
                  allOf:
                    - $ref: '#/components/schemas/Color'
                  default: red
                priority:
                  type: integer
                  enum: [1, 2, 3]
                  default: 2
                kind:
                  type: string
                  enum: [bug, feature]
                  default: bug
                count:
                  type: integer
                  default: 5
                due:
                  type: string
                  format: date
                  default: '2026-01-31'
                tags:
                  type: array
                  items: { type: string }
                  default: [a, b]
            """);

        var (records, _) = SchemaExtractor.ExtractForSchemasWithInlineEnums(document, "Contoso", ["Color", "Thing"], pathSegment: null, registry);
        return Assert.Single(records!.Parameters, r => string.Equals(r.Name, "Thing", StringComparison.Ordinal));
    }

    private static string Pascal(string name)
        => char.ToUpperInvariant(name[0]) + name.Substring(1);

    private static OpenApiDocument Parse(
        string version,
        string schemas,
        string parameters = "")
    {
        var yaml = $$"""
            openapi: {{version}}
            info: { title: Contoso, version: 1.0.0 }
            paths:
              /things:
                get:
                  operationId: listThings
            {{Indent(parameters, 6)}}
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema: { $ref: '#/components/schemas/Thing' }
            components:
              schemas:
            {{Indent(schemas, 4)}}
            """;
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yaml, "hostile.yaml");
        Assert.NotNull(document);
        return document;
    }

    private static string Indent(
        string text,
        int spaces)
        => string.Join("\n", text.Split('\n').Select(l => new string(' ', spaces) + l));
}