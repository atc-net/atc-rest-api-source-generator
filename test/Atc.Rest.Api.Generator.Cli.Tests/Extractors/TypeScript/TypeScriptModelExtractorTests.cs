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
        // PaymentMethod from Polymorphism.yaml — oneOf with explicit discriminator + mapping.
        // Today the generator emits each leaf as its own interface but no parent union, so
        // consumers can't `if (payment.type === 'credit_card')` with TS narrowing.
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
        // Notification from Polymorphism.yaml — anyOf with no discriminator. Still a union;
        // narrowing relies on consumers checking shared properties at runtime.
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
}