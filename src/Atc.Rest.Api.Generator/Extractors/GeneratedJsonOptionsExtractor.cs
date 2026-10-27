namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// The <c>GeneratedJsonOptions</c> of a typed client project: the generated code reads and writes every type through
/// the <c>JsonTypeInfo</c> its serializer options resolve, never through the reflection-based serializer overloads, so
/// it is trim- and Native AOT-safe. In an AOT app the metadata comes from the app's <c>JsonSerializerContext</c>, added
/// to the options (<c>TypeInfoResolverChain.Insert(0, ...)</c>); elsewhere options without a resolver get the reflection
/// one, as the serializer itself gives them on first use.
/// </summary>
public static class GeneratedJsonOptionsExtractor
{
    /// <summary>
    /// The class name of the generated helper.
    /// </summary>
    public const string ClassName = "GeneratedJsonOptions";

    /// <summary>
    /// The source of the helper, in the given namespace.
    /// </summary>
    /// <param name="namespaceName">The namespace of the generated file.</param>
    /// <returns>The file content.</returns>
    public static string Generate(string namespaceName)
        => $$"""
             {{HeaderBuilder.WithUsings(
                 NamespaceConstants.SystemCodeDomCompiler,
                 NamespaceConstants.SystemDiagnosticsCodeAnalysis,
                 NamespaceConstants.SystemNetHttpHeaders,
                 NamespaceConstants.SystemNetHttpJson,
                 NamespaceConstants.SystemTextJson).TrimEnd()}}

             namespace {{namespaceName}};

             /// <summary>
             /// Reads and writes through the <see cref="System.Text.Json.Serialization.Metadata.JsonTypeInfo"/> the
             /// serializer options resolve: from a <c>JsonSerializerContext</c> added to them, else - where
             /// reflection-based serialization is enabled - by reflection.
             /// </summary>
             [GeneratedCode("{{GeneratorInfo.Name}}", "{{GeneratorInfo.Version}}")]
             internal static class {{ClassName}}
             {
                 private static readonly object Gate = new();

                 /// <summary>
                 /// The metadata of <typeparamref name="T"/> the options resolve. Options in use for the first time get
                 /// the reflection resolver when they have none (only where reflection-based serialization is enabled)
                 /// and become read-only, as the serializer does with them on first use - so the metadata is cached.
                 /// </summary>
                 public static System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> TypeInfo<T>(this JsonSerializerOptions options)
                 {
                     if (!options.IsReadOnly)
                     {
                         lock (Gate)
                         {
                             if (!options.IsReadOnly)
                             {
                                 options.TypeInfoResolver ??= DefaultResolver();
                                 options.MakeReadOnly();
                             }
                         }
                     }

                     return (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
                 }

                 /// <summary>
                 /// The value as JSON content, with the media type when it is another JSON media type than
                 /// <c>application/json</c>.
                 /// </summary>
                 public static JsonContent ToJsonContent<T>(this JsonSerializerOptions options, T value, MediaTypeHeaderValue? mediaType = null)
                     => JsonContent.Create(value, options.TypeInfo<T>(), mediaType);

                 // Reflection only where reflection-based serialization is enabled: a trimmed or Native AOT app has it off
                 // (JsonSerializerIsReflectionEnabledByDefault), and the trimmer removes this branch by that feature switch
                 [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Only where reflection-based serialization is enabled; trimmed away by its feature switch.")]
                 [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Only where reflection-based serialization is enabled; trimmed away by its feature switch.")]
                 private static System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver DefaultResolver()
                     => JsonSerializer.IsReflectionEnabledByDefault
                         ? new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
                         : System.Text.Json.Serialization.Metadata.JsonTypeInfoResolver.Combine();
             }
             """;
}