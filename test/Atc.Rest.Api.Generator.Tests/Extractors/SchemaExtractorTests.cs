namespace Atc.Rest.Api.Generator.Tests.Extractors;

/// <summary>
/// Tests for SchemaExtractor, particularly header content generation with usings.
/// </summary>
public class SchemaExtractorTests
{
    [Fact]
    public void ExtractForSchemas_WithListProperty_IncludesCollectionsUsing()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                TestResponse:
                                  type: object
                                  properties:
                                    items:
                                      type: array
                                      items:
                                        type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "TestResponse" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("using System.Collections.Generic;", result.HeaderContent, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractForSchemas_WithGuidProperty_IncludesSystemUsing()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                TestResponse:
                                  type: object
                                  properties:
                                    id:
                                      type: string
                                      format: uuid
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "TestResponse" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("using System;", result.HeaderContent, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractForSchemas_WithDateTimeOffsetProperty_IncludesSystemUsing()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                TestResponse:
                                  type: object
                                  properties:
                                    createdAt:
                                      type: string
                                      format: date-time
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "TestResponse" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("using System;", result.HeaderContent, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractForSchemas_WithListOfGuids_IncludesBothUsings()
    {
        // Arrange - This is the D365TestClient case: List<Guid> Ids
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                ResendEventsResponse:
                                  type: object
                                  properties:
                                    resourceType:
                                      type: string
                                    processedCount:
                                      type: integer
                                    message:
                                      type: string
                                    ids:
                                      type: array
                                      items:
                                        type: string
                                        format: uuid
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "ResendEventsResponse" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("using System;", result.HeaderContent, StringComparison.Ordinal);
        Assert.Contains("using System.Collections.Generic;", result.HeaderContent, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractForSchemas_WithUriProperty_IncludesSystemUsing()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                TestResponse:
                                  type: object
                                  properties:
                                    callbackUrl:
                                      type: string
                                      format: uri
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "TestResponse" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("using System;", result.HeaderContent, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractForSchemas_WithDictionaryProperty_IncludesCollectionsUsing()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                TestResponse:
                                  type: object
                                  properties:
                                    metadata:
                                      type: object
                                      additionalProperties:
                                        type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "TestResponse" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("using System.Collections.Generic;", result.HeaderContent, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractForSchemas_WithSimpleStringProperty_DoesNotIncludeSystemUsing()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                TestResponse:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                                    count:
                                      type: integer
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "TestResponse" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain("using System;", result.HeaderContent, StringComparison.Ordinal);
        Assert.DoesNotContain("using System.Collections.Generic;", result.HeaderContent, StringComparison.Ordinal);
    }

    // ========== x-implements Extension Tests ==========
    [Fact]
    public void ExtractForSchemas_WithXImplements_IncludesInterfaceInRecordName()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  x-implements:
                                    - IAnimal
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Pet" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Parameters);
        Assert.Equal("Pet", result.Parameters[0].Name);
        Assert.Equal("IAnimal", result.Parameters[0].BaseTypeName);
    }

    [Fact]
    public void ExtractForSchemas_WithMultipleXImplements_IncludesAllInterfaces()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  x-implements:
                                    - IAnimal
                                    - ISerializable
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Pet" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Parameters);
        Assert.Equal("Pet", result.Parameters[0].Name);
        Assert.Equal("IAnimal, ISerializable", result.Parameters[0].BaseTypeName);
    }

    [Fact]
    public void ExtractForSchemas_WithoutXImplements_DoesNotIncludeInterface()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Pet" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Parameters);
        Assert.Equal("Pet", result.Parameters[0].Name);
    }

    // ========== readOnly / writeOnly Tests ==========
    [Fact]
    public void OpenApiParser_ReadOnlyProperty_IsParsed()
    {
        // Verify that Microsoft.OpenApi parses readOnly correctly
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    id:
                                      type: integer
                                      readOnly: true
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var pet = document.Components.Schemas["Pet"];
        var idProp = pet.Properties["id"];

        // Check if ReadOnly is accessible
        if (idProp is OpenApiSchema directSchema)
        {
            Assert.True(directSchema.ReadOnly, $"Direct OpenApiSchema.ReadOnly should be true (type: {idProp.GetType().Name})");
        }
        else if (idProp is OpenApiSchemaReference schemaRef && schemaRef.Target is OpenApiSchema targetSchema)
        {
            Assert.True(targetSchema.ReadOnly, $"Target OpenApiSchema.ReadOnly should be true");
        }
        else
        {
            Assert.Fail($"Property type is {idProp.GetType().Name}, cannot check ReadOnly");
        }
    }

    // required + nullable: the property must be in the JSON, and null is a value. [Required] would answer an explicit
    // null with a 400, so the property gets [JsonRequired] (presence) instead
    [Theory]
    [InlineData("3.0.3", "{ type: string, nullable: true }")]
    [InlineData("3.1.0", "{ type: [string, 'null'] }")]
    public void ExtractForSchemas_RequiredNullableProperty_IsJsonRequiredNotRequired(
        string openApiVersion,
        string nullableSchema)
    {
        var yaml = $$"""
                    openapi: {{openApiVersion}}
                    info:
                      title: Test
                      version: 1.0.0
                    paths: {}
                    components:
                      schemas:
                        Profile:
                          type: object
                          required:
                            - name
                            - bio
                          properties:
                            name:
                              type: string
                            bio: {{nullableSchema}}
                    """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            new HashSet<string>(StringComparer.Ordinal) { "Profile" },
            pathSegment: null);

        Assert.NotNull(result);
        var parameters = result.Parameters[0].Parameters!;

        var bio = parameters.First(p => p.Name == "Bio");
        Assert.True(bio.IsNullableType);
        Assert.Null(bio.DefaultValue);
        Assert.DoesNotContain(bio.Attributes!, a => a.Name == "Required");
        Assert.Contains(bio.Attributes!, a => a.Name == "global::System.Text.Json.Serialization.JsonRequired");

        var name = parameters.First(p => p.Name == "Name");
        Assert.Contains(name.Attributes!, a => a.Name == "Required");
    }

    [Fact]
    public void ExtractForSchemas_ReadOnlyProperty_IsOptionalEvenIfRequired()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  required:
                                    - id
                                    - name
                                  properties:
                                    id:
                                      type: integer
                                      format: int64
                                      readOnly: true
                                    name:
                                      type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Pet" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        var record = result.Parameters[0];
        Assert.NotNull(record.Parameters);

        // 'id' is readOnly + required -> should be treated as optional (nullable)
        var idParam = record.Parameters.First(p => p.Name == "Id");
        Assert.True(idParam.IsNullableType, "readOnly property should be nullable (optional) even when required");

        // 'name' is required and NOT readOnly -> should stay required (non-nullable)
        var nameParam = record.Parameters.First(p => p.Name == "Name");
        Assert.False(nameParam.IsNullableType, "Non-readOnly required property should remain required");
    }

    [Fact]
    public void ExtractForSchemas_WriteOnlyProperty_IsOptionalEvenIfRequired()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                User:
                                  type: object
                                  required:
                                    - email
                                    - password
                                  properties:
                                    email:
                                      type: string
                                    password:
                                      type: string
                                      writeOnly: true
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "User" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        var record = result.Parameters[0];
        Assert.NotNull(record.Parameters);

        // 'password' is writeOnly + required -> should be treated as optional (nullable)
        var passwordParam = record.Parameters.First(p => p.Name == "Password");
        Assert.True(passwordParam.IsNullableType, "writeOnly property should be nullable (optional) even when required");

        // 'email' is required and NOT writeOnly -> should stay required
        var emailParam = record.Parameters.First(p => p.Name == "Email");
        Assert.False(emailParam.IsNullableType, "Non-writeOnly required property should remain required");
    }

    // ========readOnly/writeOnly by the direction a schema is used in ==========

    /// <summary>
    /// A readOnly property listed in 'required' of a schema only responses carry is always there: it stays required
    /// (non-nullable, no '= null', [Required] on a reference type).
    /// </summary>
    [Fact]
    public void ExtractForSchemas_ReadOnlyRequiredProperty_OfAResponseOnlySchema_StaysRequired()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: Test, version: 1.0.0 }
                            paths:
                              /pets:
                                get:
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            $ref: '#/components/schemas/Pet'
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  required: [id, code, name]
                                  properties:
                                    id: { type: integer, format: int64, readOnly: true }
                                    code: { type: string, readOnly: true }
                                    name: { type: string }
                            """;

        var parameters = ExtractRecord(yaml, "Pet").Parameters!;

        var id = parameters.First(p => p.Name == "Id");
        Assert.False(id.IsNullableType);
        Assert.Null(id.DefaultValue);

        var code = parameters.First(p => p.Name == "Code");
        Assert.False(code.IsNullableType);
        Assert.Null(code.DefaultValue);
        Assert.Contains(code.Attributes!, a => a.Name == "Required");
    }

    /// <summary>
    /// A writeOnly property listed in 'required' of a schema only requests carry is always sent: it stays required.
    /// </summary>
    [Fact]
    public void ExtractForSchemas_WriteOnlyRequiredProperty_OfARequestOnlySchema_StaysRequired()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: Test, version: 1.0.0 }
                            paths:
                              /users:
                                post:
                                  requestBody:
                                    content:
                                      application/json:
                                        schema:
                                          $ref: '#/components/schemas/User'
                                  responses:
                                    '204':
                                      description: Created
                            components:
                              schemas:
                                User:
                                  type: object
                                  required: [email, password]
                                  properties:
                                    email: { type: string }
                                    password: { type: string, writeOnly: true }
                            """;

        var password = ExtractRecord(yaml, "User").Parameters!.First(p => p.Name == "Password");

        Assert.False(password.IsNullableType);
        Assert.Null(password.DefaultValue);
        Assert.Contains(password.Attributes!, a => a.Name == "Required");
    }

    /// <summary>
    /// A schema used both in requests and responses is one record for both: a required readOnly property must be
    /// omittable in the request, so it is generated optional (nullable, '= null').
    /// </summary>
    [Fact]
    public void ExtractForSchemas_ReadOnlyRequiredProperty_OfASchemaUsedBothWays_IsOptional()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: Test, version: 1.0.0 }
                            paths:
                              /pets:
                                post:
                                  requestBody:
                                    content:
                                      application/json:
                                        schema:
                                          $ref: '#/components/schemas/Pet'
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            $ref: '#/components/schemas/Pet'
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  required: [id, name]
                                  properties:
                                    id: { type: string, readOnly: true }
                                    name: { type: string }
                            """;

        var id = ExtractRecord(yaml, "Pet").Parameters!.First(p => p.Name == "Id");

        Assert.True(id.IsNullableType);
        Assert.Equal("null", id.DefaultValue);
        Assert.DoesNotContain(id.Attributes ?? [], a => a.Name == "Required");
    }

    /// <summary>
    /// A required property that is a $ref to a readOnly schema is decided like an inline readOnly one: optional,
    /// without [Required] - not [Required] on a nullable parameter with '= null'.
    /// </summary>
    [Fact]
    public void ExtractForSchemas_RequiredReferenceToAReadOnlySchema_IsOptionalWithoutRequiredAttribute()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: Test, version: 1.0.0 }
                            paths:
                              /things:
                                post:
                                  requestBody:
                                    content:
                                      application/json:
                                        schema:
                                          $ref: '#/components/schemas/Thing'
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            $ref: '#/components/schemas/Thing'
                            components:
                              schemas:
                                Audit:
                                  type: object
                                  readOnly: true
                                  properties:
                                    stamp: { type: string }
                                Thing:
                                  type: object
                                  required: [audit, name]
                                  properties:
                                    audit:
                                      $ref: '#/components/schemas/Audit'
                                    name: { type: string }
                            """;

        var audit = ExtractRecord(yaml, "Thing").Parameters!.First(p => p.Name == "Audit");

        Assert.True(audit.IsNullableType);
        Assert.Equal("null", audit.DefaultValue);
        Assert.DoesNotContain(audit.Attributes ?? [], a => a.Name == "Required");
    }

    /// <summary>
    /// A base reached only through a response-only schema is response-only: its required readOnly property stays
    /// required in the base and in the derived record, and the derived record forwards the base's parameters in the
    /// base's order.
    /// </summary>
    [Fact]
    public void ExtractForSchemas_ReadOnlyRequiredProperty_OfAnAllOfBaseOnlyResponsesReach_StaysRequiredInBoth()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: Test, version: 1.0.0 }
                            paths:
                              /dogs:
                                get:
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            $ref: '#/components/schemas/Dog'
                            components:
                              schemas:
                                Animal:
                                  type: object
                                  required: [id, name]
                                  properties:
                                    nickname: { type: string }
                                    id: { type: integer, format: int64, readOnly: true }
                                    name: { type: string }
                                Dog:
                                  allOf:
                                    - $ref: '#/components/schemas/Animal'
                                    - type: object
                                      required: [breed]
                                      properties:
                                        breed: { type: string }
                            """;

        var records = ExtractRecords(yaml, "Animal", "Dog");
        var animal = records.First(r => r.Name == "Animal");
        var dog = records.First(r => r.Name == "Dog");

        Assert.False(animal.Parameters!.First(p => p.Name == "Id").IsNullableType);
        Assert.False(dog.Parameters!.First(p => p.Name == "Id").IsNullableType);
        Assert.Null(dog.Parameters!.First(p => p.Name == "Id").DefaultValue);
        Assert.Equal(animal.Parameters!.Select(p => p.Name), dog.BaseConstructorArguments!, StringComparer.Ordinal);
    }

    /// <summary>
    /// A base used both ways keeps its readOnly property optional, so a derived record only responses carry has it
    /// optional too - the derived record forwards it to the base's constructor, and the two must agree.
    /// </summary>
    [Fact]
    public void ExtractForSchemas_ReadOnlyRequiredProperty_OfABaseUsedBothWays_IsOptionalInAResponseOnlyDerivedRecord()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: Test, version: 1.0.0 }
                            paths:
                              /animals:
                                post:
                                  requestBody:
                                    content:
                                      application/json:
                                        schema:
                                          $ref: '#/components/schemas/Animal'
                                  responses:
                                    '204':
                                      description: Created
                              /dogs:
                                get:
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            $ref: '#/components/schemas/Dog'
                            components:
                              schemas:
                                Animal:
                                  type: object
                                  required: [id, name]
                                  properties:
                                    id: { type: integer, format: int64, readOnly: true }
                                    name: { type: string }
                                Dog:
                                  allOf:
                                    - $ref: '#/components/schemas/Animal'
                                    - type: object
                                      required: [breed, tagged]
                                      properties:
                                        breed: { type: string }
                                        tagged: { type: boolean, readOnly: true }
                            """;

        var records = ExtractRecords(yaml, "Animal", "Dog");
        var animal = records.First(r => r.Name == "Animal");
        var dog = records.First(r => r.Name == "Dog");

        Assert.True(animal.Parameters!.First(p => p.Name == "Id").IsNullableType);
        Assert.True(dog.Parameters!.First(p => p.Name == "Id").IsNullableType);
        Assert.Equal("null", dog.Parameters!.First(p => p.Name == "Id").DefaultValue);
        Assert.True(dog.Parameters!.First(p => p.Name == "Tagged").IsNullableType);
        Assert.Equal(animal.Parameters!.Select(p => p.Name), dog.BaseConstructorArguments!, StringComparer.Ordinal);
    }

    // ========is nullable Property Tests ==========
    [Fact]
    public void ExtractForSchemas_WithNullableProperty_SetsNullableFlag()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                                    tag:
                                      type: string
                                      nullable: true
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Pet" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        var record = result.Parameters[0];
        Assert.NotNull(record.Parameters);
        var tagParam = record.Parameters.First(p => p.Name == "Tag");
        Assert.True(tagParam.IsNullableType);
    }

    // ========== Format Mapping Tests ==========
    [Theory]
    [InlineData("integer", "int32", "int")]
    [InlineData("integer", "int64", "long")]
    [InlineData("number", "float", "float")]
    [InlineData("number", "double", "double")]
    [InlineData("string", "uuid", "Guid")]
    [InlineData("string", "date-time", "DateTimeOffset")]
    [InlineData("string", "date", "DateOnly")]
    [InlineData("string", "uri", "Uri")]
    [InlineData("boolean", null, "bool")]
    public void ExtractForSchemas_FormatMapping_ProducesCorrectCSharpType(
        string openApiType,
        string? format,
        string expectedCSharpType)
    {
        var formatLine = format is not null ? $"\n          format: {format}" : string.Empty;
        var yaml = "openapi: 3.0.0\n" +
                   "info:\n  title: Test\n  version: 1.0.0\n" +
                   "paths: {}\n" +
                   "components:\n  schemas:\n    TestModel:\n      type: object\n      required:\n        - value\n" +
                   "      properties:\n        value:\n" +
                   $"          type: {openApiType}{formatLine}\n";

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "TestModel" };

        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        Assert.NotNull(result);
        var valueParam = result.Parameters[0].Parameters.First(p => p.Name == "Value");
        Assert.Equal(expectedCSharpType, valueParam.TypeName);
    }

    // ========== allOf Composition Tests ==========
    [Fact]
    public void ExtractForSchemas_AllOfComposition_MergesProperties()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Base:
                                  type: object
                                  properties:
                                    id:
                                      type: integer
                                      format: int64
                                Extended:
                                  allOf:
                                    - $ref: '#/components/schemas/Base'
                                    - type: object
                                      properties:
                                        name:
                                          type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Extended" };

        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        Assert.NotNull(result);
        var record = result.Parameters[0];
        Assert.NotNull(record.Parameters);

        // Should have both id from Base and name from inline
        Assert.Contains(record.Parameters, p => p.Name == "Id");
        Assert.Contains(record.Parameters, p => p.Name == "Name");
    }

    // ========== Required/Optional Ordering Tests ==========
    [Fact]
    public void ExtractForSchemas_RequiredBeforeOptional_CorrectParameterOrder()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  required:
                                    - name
                                  properties:
                                    tag:
                                      type: string
                                    name:
                                      type: string
                                    age:
                                      type: integer
                                      default: 0
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Pet" };

        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        Assert.NotNull(result);
        var parameters = result.Parameters[0].Parameters;

        // Required without defaults first, then optional/defaults last
        // "name" is required (no default) -> first
        // "tag" is optional (no default) -> middle
        // "age" has default -> last
        var nameIndex = parameters.ToList().FindIndex(p => p.Name == "Name");
        var ageIndex = parameters.ToList().FindIndex(p => p.Name == "Age");
        Assert.True(nameIndex < ageIndex, "Required parameter 'Name' should come before defaulted 'Age'");
    }

    // ========== Default Value Tests ==========
    [Fact]
    public void ExtractForSchemas_WithDefaultValue_SetsDefaultOnParameter()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Settings:
                                  type: object
                                  properties:
                                    isActive:
                                      type: boolean
                                      default: true
                                    maxRetries:
                                      type: integer
                                      default: 3
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Settings" };

        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        Assert.NotNull(result);
        var parameters = result.Parameters[0].Parameters;

        var isActiveParam = parameters.First(p => p.Name == "IsActive");
        Assert.Equal("true", isActiveParam.DefaultValue);

        var maxRetriesParam = parameters.First(p => p.Name == "MaxRetries");
        Assert.Equal("3", maxRetriesParam.DefaultValue);
    }

    // ========== Enum Schema Skipping ==========
    [Fact]
    public void ExtractForSchemas_EnumSchema_IsSkipped()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Color:
                                  type: string
                                  enum:
                                    - Red
                                    - Green
                                    - Blue
                                Pet:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Color", "Pet" };

        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        Assert.NotNull(result);
        Assert.Single(result.Parameters);
        Assert.Equal("Pet", result.Parameters[0].Name);
    }

    // ========== Array Property Tests ==========
    [Fact]
    public void ExtractForSchemas_ArrayProperty_ProducesListType()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Order:
                                  type: object
                                  required:
                                    - items
                                  properties:
                                    items:
                                      type: array
                                      items:
                                        type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Order" };

        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        Assert.NotNull(result);
        var itemsParam = result.Parameters[0].Parameters.First(p => p.Name == "Items");
        Assert.Equal("List<string>", itemsParam.TypeName);
    }

    // ========== Schema Description & Example Tests ==========
    [Fact]
    public void ExtractForSchemas_WithSchemaDescription_GeneratesDocumentationTags()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  description: A pet in the store.
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Pet" };

        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        Assert.NotNull(result);
        var record = result.Parameters[0];
        Assert.NotNull(record.DocumentationTags);
        Assert.Equal("A pet in the store.", record.DocumentationTags.Summary);
    }

    [Fact]
    public void ExtractForSchemas_PropertyDescriptions_BecomeParameterDocs()
    {
        // IntelliSense shows a record property's description through the record's <param> doc
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                                      description: The pet's name.
                                    tag:
                                      type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Pet" };

        var result = SchemaExtractor.ExtractForSchemas(document, "TestProject", schemaNames, pathSegment: null);

        Assert.NotNull(result);
        var docs = result.Parameters[0].DocumentationTags;
        Assert.NotNull(docs);
        Assert.NotNull(docs.Parameters);
        Assert.Equal("The pet's name.", docs.Parameters["Name"]);

        // Once one property is documented every one must be (CS1573), so an undocumented one gets a plain line
        Assert.Equal("The tag property.", docs.Parameters["Tag"]);
    }

    [Theory]
    [InlineData("Line one<br>Line two", "Line one<br/>Line two")]
    [InlineData("Line one<BR >Line two", "Line one<br/>Line two")]
    [InlineData("Line one<br/>Line two", "Line one<br/>Line two")]
    [InlineData("Line one</br>Line two", "Line one<br/>Line two")]
    public void ExtractForSchemas_HtmlLineBreakInDescription_IsWellFormedXml(
        string description,
        string expected)
    {
        // Real specifications write HTML line breaks; an unclosed <br> would be malformed XML in the doc comment (CS1570)
        var yaml = $$"""
                     openapi: 3.0.0
                     info:
                       title: Test API
                       version: 1.0.0
                     paths: {}
                     components:
                       schemas:
                         Pet:
                           type: object
                           description: "{{description}}"
                           properties:
                             name:
                               type: string
                               description: "{{description}}"
                     """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Pet" };

        var result = SchemaExtractor.ExtractForSchemas(document, "TestProject", schemaNames, pathSegment: null);

        Assert.NotNull(result);
        var docs = result.Parameters[0].DocumentationTags!;
        Assert.Equal(expected, docs.Summary);
        Assert.Equal(expected, docs.Parameters!["Name"]);
    }

    [Fact]
    public void ExtractForSchemas_DeprecatedProperty_IsObsolete()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                                    nickname:
                                      type: string
                                      deprecated: true
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Pet" };

        var result = SchemaExtractor.ExtractForSchemas(document, "TestProject", schemaNames, pathSegment: null);

        Assert.NotNull(result);
        var parameters = result.Parameters[0].Parameters!;
        Assert.Contains(parameters.Single(p => p.Name == "Nickname").Attributes!, a => a.Name == "Obsolete");
        Assert.DoesNotContain(parameters.Single(p => p.Name == "Name").Attributes ?? [], a => a.Name == "Obsolete");
    }

    [Fact]
    public void ExtractIndividual_DeprecatedSchemaIncluded_IsObsolete()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                LegacyPet:
                                  type: object
                                  deprecated: true
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = SchemaExtractor.ExtractIndividual(document, includeDeprecated: true);

        Assert.NotNull(result);
        Assert.Contains(Assert.Single(result).Attributes!, a => a.Name == "Obsolete");
    }

    [Fact]
    public void ExtractForSchemas_WithSchemaExample_GeneratesExampleTag()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  description: A pet in the store.
                                  example:
                                    name: Buddy
                                    tag: Dog
                                  properties:
                                    name:
                                      type: string
                                    tag:
                                      type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Pet" };

        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        Assert.NotNull(result);
        var record = result.Parameters[0];
        Assert.NotNull(record.DocumentationTags);
        Assert.NotNull(record.DocumentationTags.Example);
        Assert.Contains("Buddy", record.DocumentationTags.Example, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractForSchemas_ComponentExtendingGenericPage_DerivesFromClosedPage()
    {
        const string yaml = """
                            openapi: 3.1.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Account:
                                  type: object
                                  properties:
                                    id:
                                      type: string
                                PaginatedResult:
                                  type: object
                                  properties:
                                    continuation:
                                      type: [string, 'null']
                                    results:
                                      type: array
                                      items: {}
                                AccountPage:
                                  allOf:
                                    - $ref: '#/components/schemas/PaginatedResult'
                                    - type: object
                                      properties:
                                        results:
                                          type: array
                                          items:
                                            $ref: '#/components/schemas/Account'
                                        total:
                                          type: integer
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "PaginatedResult", "AccountPage" };

        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        Assert.NotNull(result);

        // The page a component extends cannot be sealed
        var page = result.Parameters.Single(r => r.Name == "PaginatedResult<T>");
        Assert.Equal(DeclarationModifiers.PublicRecord, page.DeclarationModifier);

        var accountPage = result.Parameters.Single(r => r.Name == "AccountPage");
        Assert.Equal("PaginatedResult<Account>", accountPage.BaseTypeName);
        Assert.Equal(["Continuation", "Results"], accountPage.BaseConstructorArguments);

        // The forwarded parameters are the page's own with T filled in, followed by the component's own
        Assert.NotNull(accountPage.Parameters);
        Assert.Equal(["Continuation", "Results", "Total"], accountPage.Parameters.Select(p => p.Name), StringComparer.Ordinal);
        Assert.Equal("List<Account>", accountPage.Parameters[1].TypeName);

        // results is not in the page's required list: optional, so nullable and null by default, as every optional property
        Assert.True(accountPage.Parameters[1].IsNullableType);
        Assert.Equal("null", accountPage.Parameters[1].DefaultValue);
    }

    /// <summary>
    /// A chain of allOf: the deepest component derives from the one above it and forwards every inherited
    /// parameter to that base, so the record carries every property of the chain.
    /// </summary>
    [Fact]
    public void ExtractForSchemas_ThreeLevelAllOf_EachLevelDerivesFromTheOneAbove()
    {
        const string yaml = """
                            openapi: 3.0.3
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Base:
                                  type: object
                                  required: [id]
                                  properties:
                                    id: { type: string }
                                Mid:
                                  allOf:
                                    - $ref: '#/components/schemas/Base'
                                    - type: object
                                      required: [name]
                                      properties:
                                        name: { type: string }
                                Leaf:
                                  allOf:
                                    - $ref: '#/components/schemas/Mid'
                                    - type: object
                                      properties:
                                        extra: { type: string }
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Base", "Mid", "Leaf" };

        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        Assert.NotNull(result);
        var mid = result.Parameters.Single(r => r.Name == "Mid");
        Assert.Equal("Base", mid.BaseTypeName);
        Assert.Equal(["Id"], mid.BaseConstructorArguments);
        Assert.Equal(DeclarationModifiers.PublicRecord, mid.DeclarationModifier);

        var leaf = result.Parameters.Single(r => r.Name == "Leaf");
        Assert.Equal("Mid", leaf.BaseTypeName);
        Assert.Equal(["Id", "Name"], leaf.BaseConstructorArguments);
        Assert.NotNull(leaf.Parameters);
        Assert.Equal(["Id", "Name", "Extra"], leaf.Parameters.Select(p => p.Name), StringComparer.Ordinal);
    }

    [Fact]
    public void ExtractForSchemas_WithoutDescriptionOrExample_NullDocumentationTags()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Simple:
                                  type: object
                                  properties:
                                    id:
                                      type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Simple" };

        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        Assert.NotNull(result);
        var record = result.Parameters[0];
        Assert.Null(record.DocumentationTags);
    }

    [Fact]
    public void ExtractIndividual_WithoutFilter_ReturnsObjectRecordsAndSkipsEnumsArraysAndDeprecated()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  required: [name]
                                  properties:
                                    name:
                                      type: string
                                Pets:
                                  type: array
                                  items:
                                    $ref: '#/components/schemas/Pet'
                                Status:
                                  type: string
                                  enum: [available, sold]
                                LegacyPet:
                                  type: object
                                  deprecated: true
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = SchemaExtractor.ExtractIndividual(document);
        var withDeprecated = SchemaExtractor.ExtractIndividual(document, includeDeprecated: true);

        Assert.NotNull(result);
        var pet = Assert.Single(result);
        Assert.Equal("Pet", pet.Name);
        Assert.Equal(DeclarationModifiers.PublicSealedRecord, pet.DeclarationModifier);
        var name = Assert.Single(pet.Parameters!);
        Assert.Equal("Name", name.Name);
        Assert.Equal("string", name.TypeName);
        Assert.False(name.IsNullableType);

        Assert.Equal(["Pet", "LegacyPet"], withDeprecated!.Select(r => r.Name), StringComparer.Ordinal);
    }

    [Fact]
    public void ExtractIndividual_GeneratePartialModels_EmitsPartialRecords()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = SchemaExtractor.ExtractIndividual(document, generatePartialModels: true);

        Assert.Equal(DeclarationModifiers.PublicPartialRecord, Assert.Single(result!).DeclarationModifier);
    }

    [Fact]
    public void ExtractIndividual_NoSchemas_ReturnsNull()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        Assert.Null(SchemaExtractor.ExtractIndividual(document));
    }

    [Fact]
    public void ExtractIndividual_VariantInheritsBaseAndBaseIsSkipped()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Shape:
                                  oneOf:
                                    - $ref: '#/components/schemas/Circle'
                                    - $ref: '#/components/schemas/Square'
                                  discriminator:
                                    propertyName: shapeType
                                    mapping:
                                      circle: '#/components/schemas/Circle'
                                      square: '#/components/schemas/Square'
                                Circle:
                                  type: object
                                  properties:
                                    shapeType:
                                      type: string
                                    radius:
                                      type: number
                                Square:
                                  type: object
                                  properties:
                                    shapeType:
                                      type: string
                                    sideLength:
                                      type: number
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var records = SchemaExtractor.ExtractIndividual(document);

        // The oneOf base is handled by PolymorphicTypeExtractor; the variants derive from it
        Assert.Equal(["Circle", "Square"], records!.Select(r => r.Name), StringComparer.Ordinal);
        Assert.All(records!, r => Assert.Equal("Shape", r.BaseTypeName));
    }

    [Fact]
    public void SchemaExtractor_WithOneOfSingleReference_GeneratesCorrectRecordProperty()
    {
        // Arrange - full integration test using SchemaExtractor
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /devices:
                                get:
                                  operationId: getDevices
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              schemas:
                                InsightDevice:
                                  type: object
                                  properties:
                                    id:
                                      type: string
                                      format: uuid
                                    name:
                                      type: string
                                    customer:
                                      nullable: true
                                      oneOf:
                                        - $ref: '#/components/schemas/IdValue'
                                    site:
                                      nullable: true
                                      oneOf:
                                        - $ref: '#/components/schemas/IdValue'
                                    suggestedCustomer:
                                      nullable: true
                                      oneOf:
                                        - $ref: '#/components/schemas/KeyValue'
                                IdValue:
                                  type: object
                                  properties:
                                    id:
                                      type: string
                                      format: uuid
                                    name:
                                      type: string
                                KeyValue:
                                  type: object
                                  properties:
                                    key:
                                      type: string
                                    value:
                                      type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        // Act - use overload without pathSegment to extract all component schemas
        var recordsParams = SchemaExtractor.Extract(
            document,
            "TestApi",
            includeDeprecated: false);

        // Assert
        Assert.NotNull(recordsParams);

        var insightDeviceRecord = recordsParams.Parameters
            .FirstOrDefault(r => r.Name == "InsightDevice");
        Assert.NotNull(insightDeviceRecord);
        Assert.NotNull(insightDeviceRecord.Parameters);

        // Every property is optional (no required list), so each one is nullable; the type is the oneOf reference.
        // Check the customer property type
        var customerParam = insightDeviceRecord.Parameters
            .FirstOrDefault(p => p.Name == "Customer");
        Assert.NotNull(customerParam);
        Assert.Equal("IdValue", customerParam.TypeName);
        Assert.True(customerParam.IsNullableType);

        // Check the site property type
        var siteParam = insightDeviceRecord.Parameters
            .FirstOrDefault(p => p.Name == "Site");
        Assert.NotNull(siteParam);
        Assert.Equal("IdValue", siteParam.TypeName);
        Assert.True(siteParam.IsNullableType);

        // Check the suggestedCustomer property type
        var suggestedCustomerParam = insightDeviceRecord.Parameters
            .FirstOrDefault(p => p.Name == "SuggestedCustomer");
        Assert.NotNull(suggestedCustomerParam);
        Assert.Equal("KeyValue", suggestedCustomerParam.TypeName);
        Assert.True(suggestedCustomerParam.IsNullableType);
    }

    private static RecordParameters ExtractRecord(
        string yaml,
        string schemaName)
        => ExtractRecords(yaml, schemaName).Single(r => r.Name == schemaName);

    private static List<RecordParameters> ExtractRecords(
        string yaml,
        params string[] schemaNames)
    {
        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            new HashSet<string>(schemaNames, StringComparer.Ordinal),
            pathSegment: null);

        Assert.NotNull(result);
        return result.Parameters.ToList();
    }
}