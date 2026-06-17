namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptModelExtractorTests
{
    [Fact]
    public void Extract_PaginatedResultWithEmptyItems_GeneratesGenericInterface()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info:
              title: Test
              version: 1.0.0
            paths: {}
            components:
              schemas:
                PaginatedResult:
                  type: object
                  properties:
                    pageSize:
                      type: integer
                    results:
                      type: array
                      items: {}
            """);

        var config = new TypeScriptClientConfig();

        // Act
        var results = TypeScriptModelExtractor.Extract(document, config);

        // Assert
        Assert.Single(results);
        var (name, parameters) = results[0];
        Assert.Equal("PaginatedResult", name);
        Assert.Equal("PaginatedResult<T>", parameters.TypeName);

        // The results property should be T[]
        var resultsProp = parameters.Properties?.FirstOrDefault(p => p.Name == "results");
        Assert.NotNull(resultsProp);
        Assert.Equal("T[]", resultsProp!.TypeAnnotation);
    }

    [Fact]
    public void Extract_RegularArrayProperty_DoesNotGenerateGeneric()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info:
              title: Test
              version: 1.0.0
            paths: {}
            components:
              schemas:
                UserList:
                  type: object
                  properties:
                    users:
                      type: array
                      items:
                        type: string
            """);

        var config = new TypeScriptClientConfig();

        // Act
        var results = TypeScriptModelExtractor.Extract(document, config);

        // Assert
        Assert.Single(results);
        var (_, parameters) = results[0];
        Assert.Equal("UserList", parameters.TypeName); // No <T>

        var usersProp = parameters.Properties?.FirstOrDefault(p => p.Name == "users");
        Assert.NotNull(usersProp);
        Assert.Equal("string[]", usersProp!.TypeAnnotation);
    }

    [Fact]
    public void Extract_PagedResultWithNullItems_GeneratesGenericInterface()
    {
        // Arrange — different pagination type name
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info:
              title: Test
              version: 1.0.0
            paths: {}
            components:
              schemas:
                PagedResult:
                  type: object
                  properties:
                    totalCount:
                      type: integer
                    items:
                      type: array
                      items: {}
            """);

        var config = new TypeScriptClientConfig();

        // Act
        var results = TypeScriptModelExtractor.Extract(document, config);

        // Assert
        Assert.Single(results);
        Assert.Equal("PagedResult<T>", results[0].Parameters.TypeName);
    }

    [Fact]
    public void ExtractUnionTypeAliases_OneOfWithDiscriminator_EmitsUnionTypeAlias()
    {
        //// PaymentMethod from Polymorphism.yaml — oneOf with explicit discriminator + mapping.
        //// Today the generator emits each leaf as its own interface but no parent union, so
        //// consumers can't `if (payment.type === 'credit_card')` with TS narrowing.
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                PaymentMethod:
                  oneOf:
                    - $ref: '#/components/schemas/CreditCard'
                    - $ref: '#/components/schemas/BankTransfer'
                  discriminator:
                    propertyName: type
                CreditCard:
                  type: object
                  required: [type]
                  properties:
                    type: { type: string }
                BankTransfer:
                  type: object
                  required: [type]
                  properties:
                    type: { type: string }
            """);
        var config = new TypeScriptClientConfig();

        var unions = TypeScriptModelExtractor.ExtractUnionTypeAliases(document, config);

        var paymentMethod = Assert.Single(unions);
        Assert.Equal("PaymentMethod", paymentMethod.Name);
        Assert.Contains("export type PaymentMethod = CreditCard | BankTransfer;", paymentMethod.Content, StringComparison.Ordinal);
        Assert.Contains("import type { CreditCard } from './CreditCard';", paymentMethod.Content, StringComparison.Ordinal);
        Assert.Contains("import type { BankTransfer } from './BankTransfer';", paymentMethod.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractUnionTypeAliases_AnyOfWithoutDiscriminator_EmitsUnionTypeAlias()
    {
        //// Notification from Polymorphism.yaml — anyOf with no discriminator. Still a union;
        //// narrowing relies on consumers checking shared properties at runtime.
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Notification:
                  anyOf:
                    - $ref: '#/components/schemas/EmailNotification'
                    - $ref: '#/components/schemas/SmsNotification'
                EmailNotification:
                  type: object
                  properties:
                    kind: { type: string }
                SmsNotification:
                  type: object
                  properties:
                    kind: { type: string }
            """);
        var config = new TypeScriptClientConfig();

        var unions = TypeScriptModelExtractor.ExtractUnionTypeAliases(document, config);

        var notification = Assert.Single(unions);
        Assert.Equal("Notification", notification.Name);
        Assert.Contains("export type Notification = EmailNotification | SmsNotification;", notification.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractUnionTypeAliases_OneOfWithoutDiscriminator_EmitsUnionTypeAlias()
    {
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Shape:
                  oneOf:
                    - $ref: '#/components/schemas/Circle'
                    - $ref: '#/components/schemas/Square'
                Circle:
                  type: object
                  properties:
                    radius: { type: number }
                Square:
                  type: object
                  properties:
                    side: { type: number }
            """);
        var config = new TypeScriptClientConfig();

        var unions = TypeScriptModelExtractor.ExtractUnionTypeAliases(document, config);

        var shape = Assert.Single(unions);
        Assert.Equal("Shape", shape.Name);
        Assert.Contains("export type Shape = Circle | Square;", shape.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractUnionTypeAliases_ObjectSchema_NotIncluded()
    {
        // Regression-guard: plain object schemas (no oneOf/anyOf at the top level) must
        // NOT show up in the union list — that's TypeScriptModelExtractor.Extract's job.
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Pet:
                  type: object
                  properties:
                    name: { type: string }
            """);
        var config = new TypeScriptClientConfig();

        var unions = TypeScriptModelExtractor.ExtractUnionTypeAliases(document, config);

        Assert.Empty(unions);
    }

    [Fact]
    public void Extract_SchemaWithReadOnlyAndWriteOnly_EmitsBothVariants()
    {
        // Properties marked readOnly belong to responses; writeOnly belongs to requests.
        // When either marker is present the extractor emits two interfaces — `<Name>` for
        // the response shape and `<Name>Writable` for the request shape — so client method
        // signatures can pick the right variant per position.
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                User:
                  type: object
                  required: [email, password]
                  properties:
                    id: { type: string, readOnly: true }
                    email: { type: string }
                    password: { type: string, writeOnly: true }
                    createdAt: { type: string, readOnly: true }
                    displayName: { type: string }
            """);
        var config = new TypeScriptClientConfig();

        var models = TypeScriptModelExtractor.Extract(document, config);

        // Response variant: readOnly + neutral properties, no writeOnly.
        var (responseName, responseParams) = Assert.Single(models, m => m.Name == "User");
        Assert.Equal("User", responseName);
        var responseProps = responseParams.Properties!.Select(p => p.Name).ToList();
        Assert.Contains("id", responseProps);
        Assert.Contains("createdAt", responseProps);
        Assert.Contains("email", responseProps);
        Assert.Contains("displayName", responseProps);
        Assert.DoesNotContain("password", responseProps);

        // Writable variant: writeOnly + neutral properties, no readOnly.
        var (writableName, writableParams) = Assert.Single(models, m => m.Name == "UserWritable");
        Assert.Equal("UserWritable", writableName);
        var writableProps = writableParams.Properties!.Select(p => p.Name).ToList();
        Assert.Contains("password", writableProps);
        Assert.Contains("email", writableProps);
        Assert.Contains("displayName", writableProps);
        Assert.DoesNotContain("id", writableProps);
        Assert.DoesNotContain("createdAt", writableProps);
    }

    [Fact]
    public void Extract_SchemaWithoutReadOnlyOrWriteOnly_EmitsSingleVariant()
    {
        // Regression guard: schemas with no markers must still emit a single interface
        // under the canonical name — no spurious <Name>Writable sibling.
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Address:
                  type: object
                  properties:
                    street: { type: string }
                    city: { type: string }
            """);
        var config = new TypeScriptClientConfig();

        var models = TypeScriptModelExtractor.Extract(document, config);

        var (name, _) = Assert.Single(models);
        Assert.Equal("Address", name);
    }

    [Fact]
    public void CollectSchemasWithWritableVariant_ReturnsOnlyMarkedSchemas()
    {
        // Client/hook extractors consult this set to decide whether a body parameter
        // should use `<Name>Writable` instead of `<Name>`. Unmarked schemas must not
        // appear in the set — otherwise unrelated body params would get a non-existent
        // type name.
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                User:
                  type: object
                  properties:
                    id: { type: string, readOnly: true }
                    email: { type: string }
                Address:
                  type: object
                  properties:
                    street: { type: string }
            """);

        var writable = TypeScriptModelExtractor.CollectSchemasWithWritableVariant(document);

        Assert.Contains("User", writable);
        Assert.DoesNotContain("Address", writable);
    }

    [Fact]
    public void Extract_BrandedIdsEnabled_SwapsStringForBrandAndImports()
    {
        // When --branded-ids is on, qualifying properties get the brand type instead
        // of `string` and the interface picks up a single combined import from
        // ../types/BrandedIds. Other properties stay `string` so the import line
        // doesn't list anything that isn't used in the interface body.
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Pet:
                  type: object
                  properties:
                    id: { type: string, format: uuid }
                    ownerId: { type: string, format: uuid }
                    name: { type: string }
            """);

        var config = new TypeScriptClientConfig { BrandedIds = true };
        var (_, parameters) = Assert.Single(TypeScriptModelExtractor.Extract(document, config));

        var idProp = parameters.Properties!.Single(p => p.Name == "id");
        var ownerIdProp = parameters.Properties!.Single(p => p.Name == "ownerId");
        var nameProp = parameters.Properties!.Single(p => p.Name == "name");

        Assert.Equal("PetId", idProp.TypeAnnotation);
        Assert.Equal("OwnerId", ownerIdProp.TypeAnnotation);
        Assert.Equal("string", nameProp.TypeAnnotation);

        var brandImport = Assert.Single(parameters.ImportStatements!, s => s.Contains("BrandedIds", StringComparison.Ordinal));
        Assert.Equal("import type { OwnerId, PetId } from '../types/BrandedIds';", brandImport);
    }

    [Fact]
    public void Extract_BrandedIdsDisabled_LeavesPropertiesAsString()
    {
        // Regression guard: with the flag off, the model output must be byte-identical
        // to today's behavior — that's what keeps the existing 150+ snapshots stable.
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Pet:
                  type: object
                  properties:
                    id: { type: string, format: uuid }
                    ownerId: { type: string, format: uuid }
            """);

        var config = new TypeScriptClientConfig { BrandedIds = false };
        var (_, parameters) = Assert.Single(TypeScriptModelExtractor.Extract(document, config));

        var idProp = parameters.Properties!.Single(p => p.Name == "id");
        Assert.Equal("string", idProp.TypeAnnotation);
        Assert.DoesNotContain(parameters.ImportStatements ?? new List<string>(), s => s.Contains("BrandedIds", StringComparison.Ordinal));
    }

    [Fact]
    public void Extract_SchemaPropertyWithExample_PopulatesJsDocExample()
    {
        // Arrange - A5: schema-level 'example:' on a property → @example in JSDoc
        var document = OpenApiDocumentHelper.ParseYaml("""
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
                      description: The pet name
                      example: Fido
            """);

        var config = new TypeScriptClientConfig();

        // Act
        var results = TypeScriptModelExtractor.Extract(document, config);

        // Assert
        Assert.Single(results);
        var (_, parameters) = results[0];
        var nameProp = parameters.Properties?.FirstOrDefault(p => p.Name == "name");
        Assert.NotNull(nameProp);
        Assert.NotNull(nameProp!.DocumentationTags);
        Assert.NotNull(nameProp.DocumentationTags!.Example);
        Assert.Contains("Fido", nameProp.DocumentationTags.Example, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_SchemaPropertyWithExamplesArray_PopulatesJsDocExampleFromFirst()
    {
        // Arrange - A5: OAS 3.1/3.2 schema 'examples:' array → first item in @example JSDoc
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.1.0
            info:
              title: Test
              version: 1.0.0
            paths: {}
            components:
              schemas:
                Tag:
                  type: object
                  properties:
                    value:
                      type: string
                      description: The tag value
                      examples:
                        - hello
                        - world
            """);

        var config = new TypeScriptClientConfig();

        // Act
        var results = TypeScriptModelExtractor.Extract(document, config);

        // Assert
        Assert.Single(results);
        var (_, parameters) = results[0];
        var valueProp = parameters.Properties?.FirstOrDefault(p => p.Name == "value");
        Assert.NotNull(valueProp);
        Assert.NotNull(valueProp!.DocumentationTags);
        Assert.NotNull(valueProp.DocumentationTags!.Example);
        Assert.Contains("hello", valueProp.DocumentationTags.Example, StringComparison.Ordinal);
    }
}