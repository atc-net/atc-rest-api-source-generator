namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The domain generator rewrites the <c>ExecuteAsync</c> signature of an implemented handler when the operation
/// changed. The rewrite takes the whole parameter list - a default value or an attribute with a <c>)</c> in it
/// does not cut it short - rewrites that one method only, and writes the file back as it was read: without a BOM.
/// </summary>
public sealed class HandlerSignatureRewriteTests : IDisposable
{
    private const string ExpectedSignature =
        "public Task<ListPetsResult> ExecuteAsync(\n" +
        "        ListPetsParameters parameters,\n" +
        "        CancellationToken cancellationToken = default)";

    private readonly string directory = Path.Combine(Path.GetTempPath(), "atc-handler-rewrite-" + Guid.NewGuid().ToString("N"));

    public HandlerSignatureRewriteTests()
        => Directory.CreateDirectory(directory);

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AParameterListWithAParenthesisInside_IsRewrittenWhole_WithoutABom()
    {
        var path = Path.Combine(directory, "ListPetsHandler.cs");
        var original =
            "namespace Contoso.Handlers;\n\n" +
            "public sealed class ListPetsHandler : IListPetsHandler\n{\n" +
            "    public async Task<ListPetsResult> ExecuteAsync(\n" +
            "        [FromServices(Key = \"(primary)\")] IRepository repository,\n" +
            "        CancellationToken cancellationToken = default(CancellationToken))\n" +
            "    {\n" +
            "        var pets = await repository.ListAsync(cancellationToken);\n" +
            "        return ListPetsResult.Ok(pets);\n" +
            "    }\n}\n";
        File.WriteAllText(path, original, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        ApiServerDomainGenerator.UpdateHandlerSignatureIfNeeded(path, ExpectedSignature);

        var bytes = File.ReadAllBytes(path);
        Assert.False(bytes.Length > 2 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "the file got a BOM");
        var rewritten = Encoding.UTF8.GetString(bytes);
        Assert.Contains("public async Task<ListPetsResult> ExecuteAsync(\n        ListPetsParameters parameters,\n        CancellationToken cancellationToken = default)\n    {\n        var pets = await repository.ListAsync(cancellationToken);", rewritten, StringComparison.Ordinal);
        Assert.DoesNotContain("default(CancellationToken)", rewritten, StringComparison.Ordinal);
        Assert.Equal(rewritten.Count(c => c == '('), rewritten.Count(c => c == ')'));
    }

    [Fact]
    public void OnlyThePublicExecuteAsync_IsRewritten()
    {
        var path = Path.Combine(directory, "ListPetsHandler.cs");
        var original =
            "public sealed class ListPetsHandler : IListPetsHandler\n{\n" +
            "    public Task<ListPetsResult> ExecuteAsync(\n" +
            "        CancellationToken cancellationToken = default)\n" +
            "        => Inner.ExecuteAsync(cancellationToken);\n\n" +
            "    private sealed class Inner\n    {\n" +
            "        public static Task<ListPetsResult> ExecuteAsync(CancellationToken cancellationToken) => throw new NotImplementedException();\n" +
            "    }\n}\n";
        File.WriteAllText(path, original, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        ApiServerDomainGenerator.UpdateHandlerSignatureIfNeeded(path, ExpectedSignature);

        var rewritten = File.ReadAllText(path);
        Assert.Contains("public static Task<ListPetsResult> ExecuteAsync(CancellationToken cancellationToken) => throw", rewritten, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Matches(rewritten, "ListPetsParameters parameters").Count);
    }
}