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
        Assert.Equal("T[]", resultsProp.TypeAnnotation);
    }

    [Fact]
    public void Extract_AnUntypedArrayInAPlainObject_IsNoGenericPaginationBase()
    {
        // Only a pagination base is generic (Name<T>): a plain object with an untyped array - no page
        // properties, no schema extending it - is a model whose array is unknown[], or every reference to
        // it would need a type argument (TS2314).
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info:
              title: Test
              version: 1.0.0
            paths: {}
            components:
              schemas:
                Catalog:
                  type: object
                  properties:
                    name:
                      type: string
                    entries:
                      type: array
                      items: {}
            """);

        var results = TypeScriptModelExtractor.Extract(document, new TypeScriptClientConfig());

        var (name, parameters) = Assert.Single(results);
        Assert.Equal("Catalog", name);
        Assert.Equal("Catalog", parameters.TypeName);
        var entries = Assert.Single(parameters.Properties!, p => p.Name == "entries");
        Assert.Equal("unknown[]", entries.TypeAnnotation);
    }

    [Fact]
    public void Extract_AllOfWithoutType_ExtendsTheReferencesAndKeepsTheInlineProperties()
    {
        // Composition usually leaves `type` out; such a schema is still a model, the $ref members are
        // extended and the inline members' properties (and required names) belong to it.
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info:
              title: Test
              version: 1.0.0
            paths: {}
            components:
              schemas:
                Entity:
                  type: object
                  properties:
                    id:
                      type: string
                Audited:
                  type: object
                  properties:
                    createdBy:
                      type: string
                Person:
                  allOf:
                    - $ref: '#/components/schemas/Entity'
                    - $ref: '#/components/schemas/Audited'
                    - type: object
                      required:
                        - email
                      properties:
                        email:
                          type: string
                        age:
                          type: integer
            """);

        var results = TypeScriptModelExtractor.Extract(document, new TypeScriptClientConfig());

        var (_, person) = Assert.Single(results, r => r.Name == "Person");
        Assert.Equal("Entity, Audited", person.ExtendsTypeName);
        Assert.False(person.Properties!.Single(p => p.Name == "email").IsOptional);
        Assert.True(person.Properties!.Single(p => p.Name == "age").IsOptional);
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
        Assert.Equal("string[]", usersProp.TypeAnnotation);
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
        var responseProps = responseParams.Properties.Select(p => p.Name).ToList();
        Assert.Contains("id", responseProps, StringComparer.Ordinal);
        Assert.Contains("createdAt", responseProps, StringComparer.Ordinal);
        Assert.Contains("email", responseProps, StringComparer.Ordinal);
        Assert.Contains("displayName", responseProps, StringComparer.Ordinal);
        Assert.DoesNotContain("password", responseProps, StringComparer.Ordinal);

        // Writable variant: writeOnly + neutral properties, no readOnly.
        var (writableName, writableParams) = Assert.Single(models, m => m.Name == "UserWritable");
        Assert.Equal("UserWritable", writableName);
        var writableProps = writableParams.Properties.Select(p => p.Name).ToList();
        Assert.Contains("password", writableProps, StringComparer.Ordinal);
        Assert.Contains("email", writableProps, StringComparer.Ordinal);
        Assert.Contains("displayName", writableProps, StringComparer.Ordinal);
        Assert.DoesNotContain("id", writableProps, StringComparer.Ordinal);
        Assert.DoesNotContain("createdAt", writableProps, StringComparer.Ordinal);
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
    public void Extract_BrandedIdsOnAWritableVariant_BrandAfterTheEntity()
    {
        // The Writable request variant (readOnly/writeOnly split) must use the entity's brand
        // (UserId) - the BrandedIds file never declares UserWritableId.
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                User:
                  type: object
                  properties:
                    id: { type: string, format: uuid }
                    managerId: { type: string, format: uuid }
                    password: { type: string, writeOnly: true }
            """);

        var results = TypeScriptModelExtractor.Extract(document, new TypeScriptClientConfig { BrandedIds = true });

        var (_, writable) = Assert.Single(results, r => r.Name == "UserWritable");
        Assert.Equal("UserId", writable.Properties!.Single(p => p.Name == "id").TypeAnnotation);
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

        var idProp = parameters.Properties.Single(p => p.Name == "id");
        var ownerIdProp = parameters.Properties.Single(p => p.Name == "ownerId");
        var nameProp = parameters.Properties.Single(p => p.Name == "name");

        Assert.Equal("PetId", idProp.TypeAnnotation);
        Assert.Equal("OwnerId", ownerIdProp.TypeAnnotation);
        Assert.Equal("string", nameProp.TypeAnnotation);

        var brandImport = Assert.Single(parameters.ImportStatements, s => s.Contains("BrandedIds", StringComparison.Ordinal));
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

        var idProp = parameters.Properties.Single(p => p.Name == "id");
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
        Assert.NotNull(nameProp.DocumentationTags);
        Assert.NotNull(nameProp.DocumentationTags.Example);
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
        Assert.NotNull(valueProp.DocumentationTags);
        Assert.NotNull(valueProp.DocumentationTags.Example);
        Assert.Contains("hello", valueProp.DocumentationTags.Example, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_InlineOneOfWithMultipleRefs_EmitsAUnionType()
    {
        // The mapper already handled oneOf with a single $ref by returning that name; the
        // multi-ref case fell through to `unknown`. Meanwhile the Zod emitter produced a proper
        // z.union([...]) for the same schema, so the compile-time type and the runtime validator
        // disagreed about the same property.
        var document = OpenApiDocumentHelper.ParseYaml(OneOfPropertyYaml);
        var config = new TypeScriptClientConfig();

        var results = TypeScriptModelExtractor.Extract(document, config);

        var (_, parameters) = results.Single(r => r.Name == "NotificationEvent");
        var payload = parameters.Properties?.FirstOrDefault(p => p.Name == "payload");

        Assert.NotNull(payload);
        Assert.Contains("SystemNotification", payload.TypeAnnotation, StringComparison.Ordinal);
        Assert.Contains("UserActivityEvent", payload.TypeAnnotation, StringComparison.Ordinal);
        Assert.Contains("|", payload.TypeAnnotation, StringComparison.Ordinal);
        Assert.DoesNotContain("unknown", payload.TypeAnnotation, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_InlineAnyOfWithMultipleRefs_EmitsAUnionType()
    {
        var document = OpenApiDocumentHelper.ParseYaml(
            OneOfPropertyYaml.Replace("oneOf:", "anyOf:", StringComparison.Ordinal));
        var config = new TypeScriptClientConfig();

        var results = TypeScriptModelExtractor.Extract(document, config);

        var (_, parameters) = results.Single(r => r.Name == "NotificationEvent");
        var payload = parameters.Properties?.FirstOrDefault(p => p.Name == "payload");

        Assert.NotNull(payload);
        Assert.Contains("SystemNotification", payload.TypeAnnotation, StringComparison.Ordinal);
        Assert.Contains("UserActivityEvent", payload.TypeAnnotation, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_InlineOneOfWithANonRefBranch_StaysUnknown()
    {
        // TypeScript collapses `A | unknown` to `unknown`, so a partial union buys nothing and
        // would only mislead. Zod can emit a per-branch z.unknown() because its unions do not
        // collapse; this is the one place the two emitters legitimately differ.
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info:
              title: T
              version: 1.0.0
            paths: {}
            components:
              schemas:
                NotificationEvent:
                  type: object
                  title: NotificationEvent
                  properties:
                    payload:
                      oneOf:
                        - $ref: '#/components/schemas/SystemNotification'
                        - type: string
                SystemNotification:
                  type: object
                  title: SystemNotification
                  properties:
                    message:
                      type: string
            """);

        var config = new TypeScriptClientConfig();

        var results = TypeScriptModelExtractor.Extract(document, config);

        var (_, parameters) = results.Single(r => r.Name == "NotificationEvent");
        var payload = parameters.Properties?.FirstOrDefault(p => p.Name == "payload");

        Assert.NotNull(payload);
        Assert.Contains("unknown", payload.TypeAnnotation, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_InlineOneOfProperty_DoesNotImportTypesTheInterfaceNeverNames()
    {
        // A property whose schema is an inline oneOf is emitted as `unknown`, but the referenced
        // schemas were still collected as imports. The result is an interface importing three
        // types it never names — which fails to compile for any consumer using noUnusedLocals, or
        // the equivalent ESLint rule.
        var document = OpenApiDocumentHelper.ParseYaml(OneOfPropertyYaml);

        var config = new TypeScriptClientConfig();

        var results = TypeScriptModelExtractor.Extract(document, config);

        var (_, parameters) = results.Single(r => r.Name == "NotificationEvent");
        var imports = parameters.ImportStatements ?? [];
        var emittedTypes = string.Join(
            " ",
            (parameters.Properties ?? []).Select(p => p.TypeAnnotation));

        foreach (var import in imports)
        {
            var name = import.Split('{')[1].Split('}')[0].Trim();
            Assert.True(
                emittedTypes.Contains(name, StringComparison.Ordinal) ||
                string.Equals(parameters.ExtendsTypeName, name, StringComparison.Ordinal),
                $"'{name}' is imported but no property or extends clause names it. Imports:\n" +
                string.Join("\n", imports));
        }
    }

    [Fact]
    public void Extract_VariantOfAnExplicitDiscriminator_NarrowsThePropertyToItsLiteral()
    {
        // A discriminated union is only a discriminated union in TypeScript when each variant's
        // discriminator is a literal: `payment.type === 'credit_card'` narrows to CreditCard. The
        // value is the mapping key; a variant without a mapping entry takes its schema name (OpenAPI's
        // implicit mapping) - the same values the C# converter dispatches on.
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
                    mapping:
                      credit_card: '#/components/schemas/CreditCard'
                CreditCard:
                  type: object
                  required: [type]
                  properties:
                    type: { type: string }
                    last4: { type: string }
                BankTransfer:
                  type: object
                  properties:
                    type: { type: string }
            """);

        var results = TypeScriptModelExtractor.Extract(document, new TypeScriptClientConfig());

        var (_, creditCard) = Assert.Single(results, r => r.Name == "CreditCard");
        var (_, bankTransfer) = Assert.Single(results, r => r.Name == "BankTransfer");
        Assert.Equal("'credit_card'", creditCard.Properties!.Single(p => p.Name == "type").TypeAnnotation);
        Assert.Equal("string", creditCard.Properties!.Single(p => p.Name == "last4").TypeAnnotation);
        Assert.Equal("'BankTransfer'", bankTransfer.Properties!.Single(p => p.Name == "type").TypeAnnotation);
    }

    [Fact]
    public void Extract_VariantOfAUnionWithoutADiscriminator_KeepsThePropertyAString()
    {
        // No discriminator block: nothing in the specification fixes the value, so a literal would be
        // a guess the server need not honour. The union stays a plain `A | B`.
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

        var results = TypeScriptModelExtractor.Extract(document, new TypeScriptClientConfig());

        var (_, email) = Assert.Single(results, r => r.Name == "EmailNotification");
        Assert.Equal("string", email.Properties!.Single(p => p.Name == "kind").TypeAnnotation);
    }

    [Fact]
    public void Extract_VariantInTwoUnionsWithDifferentValues_TakesTheUnionOfTheLiterals()
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
                  discriminator:
                    propertyName: kind
                    mapping:
                      circle: '#/components/schemas/Circle'
                      square: '#/components/schemas/Square'
                Round:
                  oneOf:
                    - $ref: '#/components/schemas/Circle'
                    - $ref: '#/components/schemas/Ellipse'
                  discriminator:
                    propertyName: kind
                    mapping:
                      round: '#/components/schemas/Circle'
                      ellipse: '#/components/schemas/Ellipse'
                Circle:
                  type: object
                  properties:
                    kind: { type: string }
                Square:
                  type: object
                  properties:
                    kind: { type: string }
                Ellipse:
                  type: object
                  properties:
                    kind: { type: string }
            """);

        var results = TypeScriptModelExtractor.Extract(document, new TypeScriptClientConfig());

        var (_, circle) = Assert.Single(results, r => r.Name == "Circle");
        Assert.Equal("'circle' | 'round'", circle.Properties!.Single(p => p.Name == "kind").TypeAnnotation);
    }

    [Fact]
    public void Extract_DiscriminatorPropertyThatIsAnEnum_IsLeftToTheEnum()
    {
        // `kind: $ref ShapeKind` is already typed; replacing an enum reference with a string literal
        // would not be assignable under --enum-style Enum.
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                ShapeKind:
                  type: string
                  enum: [circle, square]
                Shape:
                  oneOf:
                    - $ref: '#/components/schemas/Circle'
                    - $ref: '#/components/schemas/Square'
                  discriminator:
                    propertyName: kind
                Circle:
                  type: object
                  properties:
                    kind: { $ref: '#/components/schemas/ShapeKind' }
                Square:
                  type: object
                  properties:
                    kind: { $ref: '#/components/schemas/ShapeKind' }
            """);

        var results = TypeScriptModelExtractor.Extract(document, new TypeScriptClientConfig(), ["ShapeKind"]);

        var (_, circle) = Assert.Single(results, r => r.Name == "Circle");
        Assert.Equal("ShapeKind", circle.Properties!.Single(p => p.Name == "kind").TypeAnnotation);
    }

    [Fact]
    public void ExtractArrayTypeAliases_ComponentWithOnlyAdditionalProperties_IsARecordAlias()
    {
        // `Labels: { type: object, additionalProperties: { type: string } }` has no properties of its
        // own: as an interface it was `export interface Labels {}` - every key lost.
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Labels:
                  type: object
                  additionalProperties:
                    type: string
                Attachments:
                  type: object
                  additionalProperties:
                    $ref: '#/components/schemas/Attachment'
                Attachment:
                  type: object
                  properties:
                    name: { type: string }
            """);
        var config = new TypeScriptClientConfig();

        var aliases = TypeScriptModelExtractor.ExtractArrayTypeAliases(document, config);
        var models = TypeScriptModelExtractor.Extract(document, config);

        var labels = Assert.Single(aliases, a => a.Name == "Labels");
        Assert.Contains("export type Labels = Record<string, string>;", labels.Content, StringComparison.Ordinal);
        var attachments = Assert.Single(aliases, a => a.Name == "Attachments");
        Assert.Contains("import type { Attachment } from './Attachment';", attachments.Content, StringComparison.Ordinal);
        Assert.Contains("export type Attachments = Record<string, Attachment>;", attachments.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(models, m => m.Name is "Labels" or "Attachments");
    }

    [Fact]
    public void Extract_PropertiesAndUntypedAdditionalProperties_AddsAnOpenIndexSignature()
    {
        // `additionalProperties: {}` beside declared properties: the extra keys are allowed and untyped. (A bare
        // `additionalProperties: true` is what the parser also reports for an absent keyword - JSON Schema's
        // default - so only the schema form is a declaration the generator can see.)
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Event:
                  type: object
                  additionalProperties: {}
                  properties:
                    id: { type: string }
                    count: { type: integer }
            """);

        var (_, model) = Assert.Single(TypeScriptModelExtractor.Extract(document, new TypeScriptClientConfig()));

        var index = model.Properties!.Single(p => p.Name == "[key: string]");
        Assert.Equal("unknown", index.TypeAnnotation);
        Assert.True(index.IsReadonly);
        Assert.False(index.IsOptional);
        Assert.Equal("[key: string]", model.Properties![^1].Name);
    }

    [Fact]
    public void Extract_PropertiesAndTypedAdditionalProperties_TypesTheIndexSignatureWhenEveryPropertyFits()
    {
        // An index signature's type must admit every declared property. `{ default: string; [key: string]: string }`
        // (localized strings) can be typed; a `string` dictionary with an `integer` property cannot, and is `unknown`.
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Translations:
                  type: object
                  required: [default]
                  additionalProperties:
                    type: string
                  properties:
                    default: { type: string }
                Counters:
                  type: object
                  additionalProperties:
                    type: string
                  properties:
                    total: { type: integer }
            """);

        var results = TypeScriptModelExtractor.Extract(document, new TypeScriptClientConfig());

        var (_, translations) = Assert.Single(results, r => r.Name == "Translations");
        Assert.Equal("string", translations.Properties!.Single(p => p.Name == "[key: string]").TypeAnnotation);
        var (_, counters) = Assert.Single(results, r => r.Name == "Counters");
        Assert.Equal("unknown", counters.Properties!.Single(p => p.Name == "[key: string]").TypeAnnotation);
    }

    [Fact]
    public void Extract_Int64_IsANumber()
    {
        // Decided: int64 maps to `number`. JSON carries it as a number and JSON.parse yields a number, so
        // `bigint` or `string` would describe a value the client never receives; a value above 2^53 loses
        // precision in every JavaScript client, which the TypeScript page documents.
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths: {}
            components:
              schemas:
                Counter:
                  type: object
                  properties:
                    value: { type: integer, format: int64 }
            """);

        var (_, model) = Assert.Single(TypeScriptModelExtractor.Extract(document, new TypeScriptClientConfig()));

        Assert.Equal("number", model.Properties!.Single(p => p.Name == "value").TypeAnnotation);
    }

    /// <summary>
    /// The shape behind Showcase's <c>NotificationEvent.payload</c>: a property whose schema is an
    /// inline <c>oneOf</c> of two named component schemas.
    /// </summary>
    private const string OneOfPropertyYaml = """
        openapi: 3.0.0
        info:
          title: T
          version: 1.0.0
        paths: {}
        components:
          schemas:
            NotificationEvent:
              type: object
              title: NotificationEvent
              properties:
                id:
                  type: string
                payload:
                  oneOf:
                    - $ref: '#/components/schemas/SystemNotification'
                    - $ref: '#/components/schemas/UserActivityEvent'
            SystemNotification:
              type: object
              title: SystemNotification
              properties:
                message:
                  type: string
            UserActivityEvent:
              type: object
              title: UserActivityEvent
              properties:
                action:
                  type: string
        """;
}