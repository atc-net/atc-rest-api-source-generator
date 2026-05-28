namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptZodExtractorTests
{
    // ============ TypeScriptZodEnumExtractor ============
    [Fact]
    public void ZodEnum_NullDocument_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TypeScriptZodEnumExtractor.Extract(openApiDoc: null!, new TypeScriptClientConfig()));
    }

    [Fact]
    public void ZodEnum_NullConfig_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TypeScriptZodEnumExtractor.Extract(new OpenApiDocument(), config: null!));
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

        var result = TypeScriptZodEnumExtractor.Extract(doc!, new TypeScriptClientConfig());
        var (name, content) = Assert.Single(result);

        Assert.Equal("Status", name);
        Assert.Contains("z.enum(", content, StringComparison.Ordinal);
        Assert.Contains("'active'", content, StringComparison.Ordinal);
        Assert.Contains("'inactive'", content, StringComparison.Ordinal);
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

        var result = TypeScriptZodEnumExtractor.Extract(doc!, new TypeScriptClientConfig());

        Assert.Empty(result);
    }

    // ============ TypeScriptZodModelExtractor ============
    [Fact]
    public void ZodModel_NullDocument_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TypeScriptZodModelExtractor.Extract(openApiDoc: null!, new TypeScriptClientConfig()));
    }

    [Fact]
    public void ZodModel_NullConfig_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TypeScriptZodModelExtractor.Extract(new OpenApiDocument(), config: null!));
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

        var result = TypeScriptZodModelExtractor.Extract(doc!, new TypeScriptClientConfig());
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

        var defaultResult = TypeScriptZodModelExtractor.Extract(doc!, new TypeScriptClientConfig { IncludeDeprecated = false });
        Assert.Empty(defaultResult);

        var includedResult = TypeScriptZodModelExtractor.Extract(doc!, new TypeScriptClientConfig { IncludeDeprecated = true });
        Assert.Single(includedResult);
    }

    [Fact]
    public void ZodModel_SelfReferencingSchema_WrapsRecursiveRefWithZLazy()
    {
        // Regression for issues/003 §2: a schema whose property references itself (here
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

        var (_, content) = Assert.Single(TypeScriptZodModelExtractor.Extract(doc!, new TypeScriptClientConfig()));

        // Explicit type annotation on the const — without this the recursive ref types
        // as `any` and zod's inference can't recover.
        Assert.Contains("export const NodeSchema: z.ZodType<Node>", content, StringComparison.Ordinal);

        // The model type import sits next to the zod import so the annotation resolves.
        Assert.Contains("import type { Node } from './Node';", content, StringComparison.Ordinal);

        // The recursive sub-expression itself is wrapped in z.lazy.
        Assert.Contains("z.lazy(() => z.array(NodeSchema)", content, StringComparison.Ordinal);
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

        var (_, content) = Assert.Single(TypeScriptZodModelExtractor.Extract(doc!, new TypeScriptClientConfig()));

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

        var (_, content) = Assert.Single(TypeScriptZodModelExtractor.Extract(doc!, new TypeScriptClientConfig()));

        Assert.DoesNotContain("z.lazy(", content, StringComparison.Ordinal);
        Assert.DoesNotContain("z.ZodType<", content, StringComparison.Ordinal);
        Assert.Contains("export const UserSchema = z.object({", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ZodModel_MutuallyRecursiveSchemas_BothGetLazyAndTypeAnnotation()
    {
        // §3.6: Tag and Category reference each other. At module load time one initializer
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

        var results = TypeScriptZodModelExtractor.Extract(doc!, new TypeScriptClientConfig());

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
        // Regression guard for §3.6: when A references B but B does not reference A,
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

        var results = TypeScriptZodModelExtractor.Extract(doc!, new TypeScriptClientConfig());

        var (_, orderContent) = Assert.Single(results, r => r.Name == "Order");
        Assert.DoesNotContain("z.lazy(", orderContent, StringComparison.Ordinal);
        Assert.DoesNotContain("z.ZodType<", orderContent, StringComparison.Ordinal);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}