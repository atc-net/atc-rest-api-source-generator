namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptZodExtractorTests
{
    // ============ TypeScriptZodEnumExtractor ============
    [Theory]
    [InlineData(@"^\d{2}/\d{2}$", @"^\d{2}\/\d{2}$")]
    [InlineData(@"^a\/b$", @"^a\/b$")]
    [InlineData(@"^[/]+$", @"^[\/]+$")]
    [InlineData("^\\\\/$", "^\\\\\\/$")]
    [InlineData(@"^[A-Z]{3}$", @"^[A-Z]{3}$")]
    public void ToRegexLiteralBody_EscapesWhatWouldEndTheLiteral(
        string pattern,
        string expected)
        => Assert.Equal(expected, TypeScriptZodModelExtractor.ToRegexLiteralBody(pattern));

    /// <summary>
    /// A pattern written for .NET: the inline options <c>(?i)</c>, <c>(?m)</c>, <c>(?s)</c> are the literal's
    /// flags (a JavaScript regex has no inline options - a syntax error), <c>\A</c> is <c>^</c> and <c>\Z</c> /
    /// <c>\z</c> are <c>$</c> (identity escapes that would match a letter).
    /// </summary>
    [Theory]
    [InlineData(@"(?i)^[a-z0-9-]+$", @"/^[a-z0-9-]+$/i")]
    [InlineData(@"(?is)^a.b$", @"/^a.b$/is")]
    [InlineData(@"\A[a-z]+\z", @"/^[a-z]+$/")]
    [InlineData(@"\A[a-z]+\Z", @"/^[a-z]+$/")]
    [InlineData(@"^\\A$", @"/^\\A$/")]
    [InlineData(@"^\d{2}/\d{2}$", @"/^\d{2}\/\d{2}$/")]
    [InlineData(@"^[A-Z]{3}$", @"/^[A-Z]{3}$/")]
    public void ToRegexLiteral_IsAJavaScriptRegex(
        string pattern,
        string expected)
        => Assert.Equal(expected, TypeScriptZodModelExtractor.ToRegexLiteral(pattern));

    [Fact]
    public void ZodEnum_NullDocument_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TypeScriptZodEnumExtractor.Extract(openApiDoc: null, new TypeScriptClientConfig()));
    }

    [Fact]
    public void ZodEnum_NullConfig_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TypeScriptZodEnumExtractor.Extract(new OpenApiDocument(), config: null));
    }

    [Fact]
    public void ZodEnum_EmptyDocument_ReturnsEmpty()
    {
        var result = TypeScriptZodEnumExtractor.Extract(new OpenApiDocument(), new TypeScriptClientConfig());

        Assert.Empty(result);
    }

    [Fact]
    public void ZodEnum_StringEnum_GeneratesZodEnumSchema()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Status:
                                  type: string
                                  enum: [active, inactive]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var result = TypeScriptZodEnumExtractor.Extract(doc, new TypeScriptClientConfig());
        var (name, content) = Assert.Single(result);

        Assert.Equal("Status", name);
        Assert.Contains("z.enum(", content, StringComparison.Ordinal);
        Assert.Contains("'active'", content, StringComparison.Ordinal);
        Assert.Contains("'inactive'", content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, "export const StatusSchema = z.union([z.enum(['active', 'inactive']), z.string()]);")]
    [InlineData(false, "export const StatusSchema = z.enum(['active', 'inactive']);")]
    public void ZodEnum_AcceptsAValueTheServerAddsLater_UnlessExtensibleEnumsIsOff(
        bool extensibleEnums,
        string expected)
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Status:
                                  type: string
                                  enum: [active, inactive]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptZodEnumExtractor.Extract(doc, new TypeScriptClientConfig { ExtensibleEnums = extensibleEnums }));

        Assert.Contains(expected, content, StringComparison.Ordinal);
    }

    [Fact]
    public void ZodEnum_NonStringEnum_Skipped()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Level:
                                  type: integer
                                  enum: [1, 2, 3]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var result = TypeScriptZodEnumExtractor.Extract(doc, new TypeScriptClientConfig());

        Assert.Empty(result);
    }

    // ============ TypeScriptZodModelExtractor ============

    /// <summary>
    /// A date-time on the wire carries an offset as often as a Z (a .NET server writes <c>+00:00</c> for a
    /// <c>DateTimeOffset</c>); Zod's <c>datetime()</c> accepts only Z unless told about offsets.
    /// </summary>
    [Fact]
    public void ZodModel_DateTime_AcceptsAnOffset()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Moment:
                                  type: object
                                  properties:
                                    day: { type: string, format: date }
                                    at: { type: string, format: date-time }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptZodModelExtractor.Extract(doc, new TypeScriptClientConfig()), r => r.Name == "Moment");

        Assert.Contains("day: z.string().date()", content, StringComparison.Ordinal);
        Assert.Contains("at: z.string().datetime({ offset: true, local: true })", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ZodModel_AllOfWithoutType_ExtendsEveryBaseAndKeepsTheInlineProperties()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Entity: { type: object, properties: { id: { type: string } } }
                                Audited: { type: object, properties: { createdBy: { type: string } } }
                                Person:
                                  allOf:
                                    - $ref: '#/components/schemas/Entity'
                                    - $ref: '#/components/schemas/Audited'
                                    - type: object
                                      required: [email]
                                      properties:
                                        email: { type: string }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptZodModelExtractor.Extract(doc, new TypeScriptClientConfig()), r => r.Name == "Person");

        Assert.Contains("export const PersonSchema = EntitySchema.extend(AuditedSchema.shape).extend({", content, StringComparison.Ordinal);
        Assert.Contains("  email: z.string(),", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ZodUnion_OneOfOfReferences_IsAZodUnionOfTheirSchemas()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Card: { type: object, properties: { number: { type: string } } }
                                Wallet: { type: object, properties: { address: { type: string } } }
                                Payment:
                                  oneOf:
                                    - $ref: '#/components/schemas/Card'
                                    - $ref: '#/components/schemas/Wallet'
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (name, content) = Assert.Single(TypeScriptZodModelExtractor.ExtractUnionTypeAliases(doc, new TypeScriptClientConfig()));

        Assert.Equal("Payment", name);
        Assert.Contains("import { CardSchema } from './Card.zod';", content, StringComparison.Ordinal);
        Assert.Contains("export const PaymentSchema = z.union([CardSchema.passthrough(), WalletSchema.passthrough()]);", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// The option schemas name the discriminator as the models do (the naming strategy), so the union dispatches on
    /// that name - <c>z.discriminatedUnion('payment_type', ...)</c> over options that declare <c>paymentType</c>
    /// did not type-check (TS2322) and could not parse anything.
    /// </summary>
    [Fact]
    public void ZodUnion_DiscriminatorNamedForTheWire_DispatchesOnTheModelsName()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Card:
                                  type: object
                                  required: [payment_type]
                                  properties: { payment_type: { type: string }, card_number: { type: string } }
                                Bank:
                                  type: object
                                  required: [payment_type]
                                  properties: { payment_type: { type: string }, iban_number: { type: string } }
                                Payment:
                                  oneOf:
                                    - $ref: '#/components/schemas/Card'
                                    - $ref: '#/components/schemas/Bank'
                                  discriminator:
                                    propertyName: payment_type
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);
        var config = new TypeScriptClientConfig();

        var (_, union) = Assert.Single(TypeScriptZodModelExtractor.ExtractUnionTypeAliases(doc, config));
        var models = TypeScriptZodModelExtractor.Extract(doc, config);

        Assert.Contains("export const PaymentSchema = z.discriminatedUnion('paymentType', [CardSchema, BankSchema]);", union, StringComparison.Ordinal);
        Assert.Contains("  paymentType: z.literal('Card'),", models.Single(m => m.Name == "Card").Content, StringComparison.Ordinal);
    }

    [Fact]
    public void ZodUnion_OneOfWithAnExplicitDiscriminator_IsADiscriminatedUnionOfLiterals()
    {
        // z.discriminatedUnion picks the variant by the literal instead of trying each one, and reports a
        // mismatch against the variant the value names rather than every variant's errors.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Card:
                                  type: object
                                  required: [type]
                                  properties: { type: { type: string }, number: { type: string } }
                                Wallet:
                                  type: object
                                  required: [type]
                                  properties: { type: { type: string }, address: { type: string } }
                                Payment:
                                  oneOf:
                                    - $ref: '#/components/schemas/Card'
                                    - $ref: '#/components/schemas/Wallet'
                                  discriminator:
                                    propertyName: type
                                    mapping:
                                      card: '#/components/schemas/Card'
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);
        var config = new TypeScriptClientConfig();

        var (_, union) = Assert.Single(TypeScriptZodModelExtractor.ExtractUnionTypeAliases(doc, config));
        var models = TypeScriptZodModelExtractor.Extract(doc, config);

        Assert.Contains("export const PaymentSchema = z.discriminatedUnion('type', [CardSchema, WalletSchema]);", union, StringComparison.Ordinal);
        Assert.Contains("  type: z.literal('card'),", models.Single(m => m.Name == "Card").Content, StringComparison.Ordinal);
        Assert.Contains("  type: z.literal('Wallet'),", models.Single(m => m.Name == "Wallet").Content, StringComparison.Ordinal);
    }

    [Fact]
    public void ZodModel_ComponentWithOnlyAdditionalProperties_IsARecord()
    {
        // `z.object({})` strips every key it does not know - a dictionary component validated at runtime came
        // back empty.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Labels:
                                  type: object
                                  additionalProperties:
                                    type: string
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);
        var config = new TypeScriptClientConfig();

        var (_, content) = Assert.Single(TypeScriptZodModelExtractor.ExtractArrayTypeAliases(doc, config));

        Assert.Contains("export const LabelsSchema = z.record(z.string(), z.string());", content, StringComparison.Ordinal);
        Assert.Empty(TypeScriptZodModelExtractor.Extract(doc, config));
    }

    [Fact]
    public void ZodModel_PropertiesAndAdditionalProperties_KeepsTheExtraKeys()
    {
        const string yaml = """
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
                                Translations:
                                  type: object
                                  additionalProperties:
                                    type: string
                                  properties:
                                    default: { type: string }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var models = TypeScriptZodModelExtractor.Extract(doc, new TypeScriptClientConfig());

        Assert.Contains("}).passthrough();", models.Single(m => m.Name == "Event").Content, StringComparison.Ordinal);
        Assert.Contains("}).catchall(z.string());", models.Single(m => m.Name == "Translations").Content, StringComparison.Ordinal);
    }

    [Fact]
    public void ZodModel_NullDocument_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TypeScriptZodModelExtractor.Extract(openApiDoc: null, new TypeScriptClientConfig()));
    }

    [Fact]
    public void ZodModel_NullConfig_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TypeScriptZodModelExtractor.Extract(new OpenApiDocument(), config: null));
    }

    [Fact]
    public void ZodModel_EmptyDocument_ReturnsEmpty()
    {
        var result = TypeScriptZodModelExtractor.Extract(new OpenApiDocument(), new TypeScriptClientConfig());

        Assert.Empty(result);
    }

    [Fact]
    public void ZodModel_ObjectSchema_GeneratesZodObjectSchema()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    id:
                                      type: string
                                    name:
                                      type: string
                                  required: [id]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var result = TypeScriptZodModelExtractor.Extract(doc, new TypeScriptClientConfig());
        var (name, content) = Assert.Single(result);

        Assert.Equal("Pet", name);
        Assert.Contains("z.object", content, StringComparison.Ordinal);
        Assert.Contains("id:", content, StringComparison.Ordinal);
        Assert.Contains("name:", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ZodModel_DeprecatedSchema_RespectsIncludeFlag()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                LegacyAccount:
                                  type: object
                                  deprecated: true
                                  properties:
                                    id: { type: string }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var defaultResult = TypeScriptZodModelExtractor.Extract(doc, new TypeScriptClientConfig { IncludeDeprecated = false });
        Assert.Empty(defaultResult);

        var includedResult = TypeScriptZodModelExtractor.Extract(doc, new TypeScriptClientConfig { IncludeDeprecated = true });
        Assert.Single(includedResult);
    }

    [Fact]
    public void ZodModel_SelfReferencingSchema_WrapsRecursiveRefWithZLazy()
    {
        // A schema whose property references itself (here
        // via array.items.$ref) must emit z.lazy(...) around the recursive sub-expression
        // and an explicit `: z.ZodType<Name>` annotation, or strict tsc trips TS 7022
        // (implicit any) and TS 2448 (used before declaration).
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Node:
                                  type: object
                                  properties:
                                    id:
                                      type: string
                                    children:
                                      type: array
                                      items:
                                        $ref: '#/components/schemas/Node'
                                  required: [id]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptZodModelExtractor.Extract(doc, new TypeScriptClientConfig()));

        // Explicit type annotation on the const — without this the recursive ref types
        // as `any` and zod's inference can't recover.
        Assert.Contains("export const NodeSchema: z.ZodType<Node>", content, StringComparison.Ordinal);

        // The model type import sits next to the zod import so the annotation resolves.
        Assert.Contains("import type { Node } from './Node';", content, StringComparison.Ordinal);

        // The recursive sub-expression itself is wrapped in z.lazy.
        Assert.Contains("z.lazy(() => z.array(NodeSchema)", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ZodModel_ACycleThroughAnArrayAlias_WrapsWithZLazy()
    {
        // Node refers to NodeList, an array alias whose items are Node: the cycle runs through the alias's own
        // items, which the dependency graph used to miss - the schema loaded with a ReferenceError.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Node:
                                  type: object
                                  required: [name]
                                  properties:
                                    name:
                                      type: string
                                    children:
                                      $ref: '#/components/schemas/NodeList'
                                NodeList:
                                  type: array
                                  items:
                                    $ref: '#/components/schemas/Node'
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptZodModelExtractor.Extract(doc, new TypeScriptClientConfig()), r => r.Name == "Node");

        Assert.Contains("export const NodeSchema: z.ZodType<Node>", content, StringComparison.Ordinal);
        Assert.Contains("z.lazy(", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// A union without a discriminator takes the first variant that parses, and a plain object schema strips the keys
    /// it does not know: <c>{x, y}</c> parsed against <c>[A{x}, B{x, y}]</c> came back as <c>{x}</c>. The members of
    /// such a union pass unknown keys through, so the data stays what the server sent; a discriminated union and a
    /// cyclic member (a <c>ZodType</c>, which has no <c>passthrough</c>) are unchanged.
    /// </summary>
    [Fact]
    public void ZodUnion_WithoutDiscriminator_KeepsTheKeysOfEveryVariant()
    {
        const string yaml = """
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
                                  required: [radius]
                                  properties:
                                    radius: { type: number }
                                Square:
                                  type: object
                                  required: [radius, side]
                                  properties:
                                    radius: { type: number }
                                    side: { type: number }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptZodModelExtractor.ExtractUnionTypeAliases(doc, new TypeScriptClientConfig()));

        Assert.Contains("z.union([CircleSchema.passthrough(), SquareSchema.passthrough()])", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// What .NET writes must validate: an optional property is written as <c>null</c> when it has no value, so it is
    /// <c>nullish</c> (null or absent), not only <c>optional</c>; a required nullable one is <c>nullable</c>; a
    /// <c>$ref</c> property follows the same rule, and a 3.1 <c>oneOf: [$ref, {type: 'null'}]</c> is the referenced
    /// schema, nullable - not a union with <c>z.unknown()</c> that validates nothing.
    /// </summary>
    [Fact]
    public void ZodModel_OptionalIsNullish_RequiredNullableIsNullable_ANullBranchIsNullable()
    {
        const string yaml = """
                            openapi: 3.1.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Other:
                                  type: object
                                  properties:
                                    id: { type: string }
                                Thing:
                                  type: object
                                  required: [name, tag, must, alt, at]
                                  properties:
                                    name: { type: string }
                                    note: { type: string }
                                    tag: { type: ['string', 'null'] }
                                    ref: { $ref: '#/components/schemas/Other' }
                                    must: { $ref: '#/components/schemas/Other' }
                                    alt:
                                      oneOf:
                                        - $ref: '#/components/schemas/Other'
                                        - type: 'null'
                                    at: { type: string, format: date-time }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptZodModelExtractor.Extract(doc, new TypeScriptClientConfig()), r => r.Name == "Thing");

        Assert.Contains("name: z.string(),", content, StringComparison.Ordinal);
        Assert.Contains("note: z.string().nullish(),", content, StringComparison.Ordinal);
        Assert.Contains("tag: z.string().nullable(),", content, StringComparison.Ordinal);
        Assert.Contains("ref: OtherSchema.nullish(),", content, StringComparison.Ordinal);
        Assert.Contains("must: OtherSchema,", content, StringComparison.Ordinal);
        Assert.Contains("alt: OtherSchema.nullable(),", content, StringComparison.Ordinal);
        Assert.Contains("at: z.string().datetime({ offset: true, local: true }),", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// An integer enum is open like a string enum when enums are extensible: a value the server adds later is a
    /// number the schema accepts.
    /// </summary>
    [Theory]
    [InlineData(true, "export const LevelSchema = z.union([z.literal(1), z.literal(2), z.number()]);")]
    [InlineData(false, "export const LevelSchema = z.union([z.literal(1), z.literal(2)]);")]
    public void ZodScalarAlias_AnIntegerEnum_IsOpen_UnlessExtensibleEnumsIsOff(
        bool extensibleEnums,
        string expected)
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Level:
                                  type: integer
                                  enum: [1, 2]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptZodModelExtractor.ExtractArrayTypeAliases(doc, new TypeScriptClientConfig { ExtensibleEnums = extensibleEnums }));

        Assert.Contains(expected, content, StringComparison.Ordinal);
    }

    [Fact]
    public void ZodModel_DirectSelfReferenceViaRef_WrapsWithZLazy()
    {
        // A direct $ref to self (without an array layer) also needs z.lazy.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                TreeNode:
                                  type: object
                                  properties:
                                    parent:
                                      $ref: '#/components/schemas/TreeNode'
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptZodModelExtractor.Extract(doc, new TypeScriptClientConfig()));

        Assert.Contains("z.lazy(() => TreeNodeSchema", content, StringComparison.Ordinal);
        Assert.Contains("export const TreeNodeSchema: z.ZodType<TreeNode>", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ZodModel_NonRecursiveSchema_DoesNotEmitZLazyOrTypeAnnotation()
    {
        // Regression guard: the lazy / type-annotation branch must only fire for
        // self-referencing schemas. A vanilla object should still emit the original
        // unannotated form so downstream consumers and existing snapshots stay stable.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                User:
                                  type: object
                                  properties:
                                    id: { type: string }
                                    name: { type: string }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptZodModelExtractor.Extract(doc, new TypeScriptClientConfig()));

        Assert.DoesNotContain("z.lazy(", content, StringComparison.Ordinal);
        Assert.DoesNotContain("z.ZodType<", content, StringComparison.Ordinal);
        Assert.Contains("export const UserSchema = z.object({", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ZodModel_MutuallyRecursiveSchemas_BothGetLazyAndTypeAnnotation()
    {
        // Tag and Category reference each other. At module load time one initializer
        // would see the other's binding as `undefined` and crash on `.optional()`. Both
        // sides need `: z.ZodType<Name>` plus z.lazy on the cross-cycle property.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Category:
                                  type: object
                                  properties:
                                    id: { type: string }
                                    tag: { $ref: '#/components/schemas/Tag' }
                                Tag:
                                  type: object
                                  properties:
                                    label: { type: string }
                                    category: { $ref: '#/components/schemas/Category' }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var results = TypeScriptZodModelExtractor.Extract(doc, new TypeScriptClientConfig());

        var (_, categoryContent) = Assert.Single(results, r => r.Name == "Category");
        Assert.Contains("export const CategorySchema: z.ZodType<Category>", categoryContent, StringComparison.Ordinal);
        Assert.Contains("import type { Category } from './Category';", categoryContent, StringComparison.Ordinal);
        Assert.Contains("z.lazy(() => TagSchema", categoryContent, StringComparison.Ordinal);

        var (_, tagContent) = Assert.Single(results, r => r.Name == "Tag");
        Assert.Contains("export const TagSchema: z.ZodType<Tag>", tagContent, StringComparison.Ordinal);
        Assert.Contains("import type { Tag } from './Tag';", tagContent, StringComparison.Ordinal);
        Assert.Contains("z.lazy(() => CategorySchema", tagContent, StringComparison.Ordinal);
    }

    [Fact]
    public void ZodModel_AcyclicCrossSchemaReference_DoesNotEmitLazyOrAnnotation()
    {
        // When A references B but B does not reference A,
        // the relationship is acyclic — neither side needs lazy/annotation.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Order:
                                  type: object
                                  properties:
                                    id: { type: string }
                                    customer: { $ref: '#/components/schemas/Customer' }
                                Customer:
                                  type: object
                                  properties:
                                    name: { type: string }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var results = TypeScriptZodModelExtractor.Extract(doc, new TypeScriptClientConfig());

        var (_, orderContent) = Assert.Single(results, r => r.Name == "Order");
        Assert.DoesNotContain("z.lazy(", orderContent, StringComparison.Ordinal);
        Assert.DoesNotContain("z.ZodType<", orderContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// OpenAPI 3.1: a default next to a <c>$ref</c> is the referencing property's own. The schema of the property
    /// has it - also where the component declares a default of its own - and a property that references the same
    /// component without one does not get the component's.
    /// </summary>
    [Fact]
    public void ZodModel_ADefaultNextToAReference_IsThePropertysDefault()
    {
        const string yaml = """
                            openapi: 3.1.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Order:
                                  type: object
                                  properties:
                                    priority:
                                      $ref: '#/components/schemas/Priority'
                                      default: high
                                    fallback:
                                      $ref: '#/components/schemas/Priority'
                                    count:
                                      $ref: '#/components/schemas/Count'
                                      default: 3
                                Priority:
                                  type: string
                                  enum: [low, high]
                                Count:
                                  type: integer
                                  default: 1
                            """;
        var doc = OpenApiDocumentHelper.TryParseYaml(yaml, $"zod-sibling-{Guid.NewGuid():N}.yaml", out var document) ? document : null;
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptZodModelExtractor.Extract(doc, new TypeScriptClientConfig()), r => r.Name == "Order");

        Assert.Contains("priority: PrioritySchema.nullish().default('high'),", content, StringComparison.Ordinal);
        Assert.Contains("fallback: PrioritySchema.nullish(),", content, StringComparison.Ordinal);
        Assert.Contains("count: CountSchema.nullish().default(3),", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// A default is written as the TypeScript literal of its value: a text with an apostrophe or a backslash is a
    /// string that still compiles, a number is the number however the reader holds it, a boolean a boolean.
    /// </summary>
    [Fact]
    public void ZodModel_Defaults_AreLiteralsThatCompile()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths: {}
                            components:
                              schemas:
                                Settings:
                                  type: object
                                  properties:
                                    greeting:
                                      type: string
                                      default: "it's a C:\\temp"
                                    retries:
                                      type: integer
                                      default: 3
                                    ratio:
                                      type: number
                                      default: 0.5
                                    enabled:
                                      type: boolean
                                      default: true
                            """;
        var doc = OpenApiDocumentHelper.TryParseYaml(yaml, $"zod-defaults-{Guid.NewGuid():N}.yaml", out var document) ? document : null;
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptZodModelExtractor.Extract(doc, new TypeScriptClientConfig()), r => r.Name == "Settings");

        Assert.Contains(@".default('it\'s a C:\\temp')", content, StringComparison.Ordinal);
        Assert.Contains("retries: z.number().int().nullish().default(3),", content, StringComparison.Ordinal);
        Assert.Contains(".default(0.5)", content, StringComparison.Ordinal);
        Assert.Contains(".default(true)", content, StringComparison.Ordinal);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}