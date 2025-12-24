namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class UsingStatementHelperTests
{
    // ========== ContentContains Tests ==========
    [Theory]
    [InlineData("public Dictionary<string, int> Items { get; set; }", "Dictionary<", true)]
    [InlineData("public List<string> Names { get; set; }", "Dictionary<", false)]
    [InlineData("public Task<Pet> GetPetAsync()", "Task<", true)]
    [InlineData("public void DoSomething()", "Task<", false)]
    [InlineData("[Required] public string Name { get; set; }", "[Required]", true)]
    [InlineData("public string Name { get; set; }", "[Required]", false)]
    public void ContentContains_ReturnsExpectedResult(
        string content,
        string pattern,
        bool expected)
    {
        var result = UsingStatementHelper.ContentContains(content, pattern);
        Assert.Equal(expected, result);
    }

    // ========== GetRequiredUsings Tests ==========
    [Fact]
    public void GetRequiredUsings_WithDictionary_ReturnsCollectionsGeneric()
    {
        var content = "public Dictionary<string, int> Items { get; set; }";
        var result = UsingStatementHelper.GetRequiredUsings(content);
        Assert.Contains("System.Collections.Generic", result);
    }

    [Fact]
    public void GetRequiredUsings_WithList_ReturnsCollectionsGeneric()
    {
        var content = "public List<string> Names { get; set; }";
        var result = UsingStatementHelper.GetRequiredUsings(content);
        Assert.Contains("System.Collections.Generic", result);
    }

    [Fact]
    public void GetRequiredUsings_WithTask_ReturnsThreadingTasks()
    {
        var content = "public Task<Pet> GetPetAsync(CancellationToken ct)";
        var result = UsingStatementHelper.GetRequiredUsings(content);
        Assert.Contains("System.Threading.Tasks", result);
        Assert.Contains("System.Threading", result);
    }

    [Fact]
    public void GetRequiredUsings_WithIFormFile_ReturnsAspNetCoreHttp()
    {
        var content = "public IFormFile File { get; set; }";
        var result = UsingStatementHelper.GetRequiredUsings(content);
        Assert.Contains("Microsoft.AspNetCore.Http", result);
    }

    [Fact]
    public void GetRequiredUsings_WithHttpClient_ReturnsNetHttp()
    {
        var content = "private readonly HttpClient _client;";
        var result = UsingStatementHelper.GetRequiredUsings(content);
        Assert.Contains("System.Net.Http", result);
    }

    [Fact]
    public void GetRequiredUsings_WithRequiredAttribute_ReturnsDataAnnotations()
    {
        var content = "[Required] public string Name { get; set; }";
        var result = UsingStatementHelper.GetRequiredUsings(content);
        Assert.Contains("System.ComponentModel.DataAnnotations", result);
    }

    [Fact]
    public void GetRequiredUsings_WithMultipleTypes_ReturnsAllNamespaces()
    {
        var content = @"
            public Dictionary<string, int> Items { get; set; }
            public Task<List<Pet>> GetPetsAsync(CancellationToken ct)
            [Required] public string Name { get; set; }
        ";

        var result = UsingStatementHelper.GetRequiredUsings(content);

        Assert.Contains("System.Collections.Generic", result);
        Assert.Contains("System.Threading.Tasks", result);
        Assert.Contains("System.Threading", result);
        Assert.Contains("System.ComponentModel.DataAnnotations", result);
    }

    [Fact]
    public void GetRequiredUsings_WithAlwaysInclude_IncludesSpecifiedNamespaces()
    {
        var content = "public string Name { get; set; }";
        var result = UsingStatementHelper.GetRequiredUsings(content, "System.CodeDom.Compiler");

        Assert.Contains("System.CodeDom.Compiler", result);
        Assert.Single(result);
    }

    [Fact]
    public void GetRequiredUsings_DeduplicatesNamespaces()
    {
        var content = @"
            public Dictionary<string, int> Dict1 { get; set; }
            public Dictionary<string, string> Dict2 { get; set; }
            public List<int> List1 { get; set; }
            public List<string> List2 { get; set; }
        ";

        var result = UsingStatementHelper.GetRequiredUsings(content);

        // Should only have one entry for System.Collections.Generic
        Assert.Single(result, u => u == "System.Collections.Generic");
    }

    [Fact]
    public void GetRequiredUsings_EmptyContent_ReturnsOnlyAlwaysInclude()
    {
        var result = UsingStatementHelper.GetRequiredUsings(string.Empty, "System.CodeDom.Compiler");

        Assert.Single(result);
        Assert.Contains("System.CodeDom.Compiler", result);
    }

    // ========== AppendUsings Tests ==========
    [Fact]
    public void AppendUsings_SortsAlphabeticallyWithinGroups()
    {
        var usings = new[]
        {
            "System.Threading.Tasks",
            "System.Collections.Generic",
            "System.Threading",
        };

        var sb = new StringBuilder();
        UsingStatementHelper.AppendUsings(sb, usings);

        var result = sb.ToString();
        var lines = result.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("using System.Collections.Generic;", lines[0]);
        Assert.Equal("using System.Threading;", lines[1]);
        Assert.Equal("using System.Threading.Tasks;", lines[2]);
    }

    [Fact]
    public void AppendUsings_SystemNamespacesBeforeMicrosoft()
    {
        var usings = new[]
        {
            "Microsoft.AspNetCore.Http",
            "System.Collections.Generic",
        };

        var sb = new StringBuilder();
        UsingStatementHelper.AppendUsings(sb, usings);

        var result = sb.ToString();
        var systemIndex = result.IndexOf("System.", StringComparison.Ordinal);
        var microsoftIndex = result.IndexOf("Microsoft.", StringComparison.Ordinal);

        Assert.True(systemIndex < microsoftIndex);
    }

    [Fact]
    public void AppendUsings_MicrosoftBeforeAsp()
    {
        var usings = new[]
        {
            "Asp.Versioning",
            "Microsoft.AspNetCore.Http",
        };

        var sb = new StringBuilder();
        UsingStatementHelper.AppendUsings(sb, usings);

        var result = sb.ToString();
        var microsoftIndex = result.IndexOf("Microsoft.", StringComparison.Ordinal);
        var aspIndex = result.IndexOf("Asp.", StringComparison.Ordinal);

        Assert.True(microsoftIndex < aspIndex);
    }

    [Fact]
    public void AppendUsings_AtcAfterAsp()
    {
        var usings = new[]
        {
            "Atc.Rest.Client",
            "Asp.Versioning",
        };

        var sb = new StringBuilder();
        UsingStatementHelper.AppendUsings(sb, usings);

        var result = sb.ToString();
        var aspIndex = result.IndexOf("Asp.", StringComparison.Ordinal);
        var atcIndex = result.IndexOf("Atc.", StringComparison.Ordinal);

        Assert.True(aspIndex < atcIndex);
    }

    // ========== BuildHeader Tests ==========
    [Fact]
    public void BuildHeader_IncludesAutoGeneratedComment()
    {
        var result = UsingStatementHelper.BuildHeader(string.Empty, "System.CodeDom.Compiler");
        Assert.Contains("// <auto-generated />", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHeader_IncludesNullableEnable()
    {
        var result = UsingStatementHelper.BuildHeader(string.Empty, "System.CodeDom.Compiler");
        Assert.Contains("#nullable enable", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHeader_IncludesDetectedUsings()
    {
        var content = "public Dictionary<string, int> Items { get; set; }";
        var result = UsingStatementHelper.BuildHeader(content, "System.CodeDom.Compiler");

        Assert.Contains("using System.CodeDom.Compiler;", result, StringComparison.Ordinal);
        Assert.Contains("using System.Collections.Generic;", result, StringComparison.Ordinal);
    }

    // ========== Specific Type Pattern Tests ==========
    [Theory]
    [InlineData("ReadFromJsonAsync<Pet>()", "System.Net.Http.Json")]
    [InlineData("PostAsJsonAsync()", "System.Net.Http.Json")]
    [InlineData("[JsonPropertyName(\"name\")]", "System.Text.Json.Serialization")]
    [InlineData("[EnumMember(Value = \"foo\")]", "System.Runtime.Serialization")]
    [InlineData("ApiVersionSet versionSet", "Asp.Versioning")]
    [InlineData("ValidationFilter<MyParams>", "Atc.Rest.MinimalApi.Filters.Endpoints")]
    [InlineData("BinaryEndpointResponse", "Atc.Rest.Client")]
    [InlineData("IHttpMessageFactory factory", "Atc.Rest.Client.Builder")]
    public void GetRequiredUsings_DetectsSpecificPatterns(
        string content,
        string expectedUsing)
    {
        var result = UsingStatementHelper.GetRequiredUsings(content);
        Assert.Contains(expectedUsing, result);
    }

    [Theory]
    [InlineData("[FromQuery(Name = \"id\")]", "Microsoft.AspNetCore.Mvc")]
    [InlineData("[FromRoute(Name = \"id\")]", "Microsoft.AspNetCore.Mvc")]
    [InlineData("[FromBody]", "Microsoft.AspNetCore.Mvc")]
    [InlineData("[AsParameters]", "Microsoft.AspNetCore.Mvc")]
    public void GetRequiredUsings_DetectsBindingAttributes(
        string content,
        string expectedUsing)
    {
        var result = UsingStatementHelper.GetRequiredUsings(content);
        Assert.Contains(expectedUsing, result);
    }

    [Theory]
    [InlineData("Results.Ok(pet)", "Microsoft.AspNetCore.Http")]
    [InlineData("Results.Created(\"/pets/1\", pet)", "Microsoft.AspNetCore.Http")]
    [InlineData("Results.NotFound()", "Microsoft.AspNetCore.Http")]
    [InlineData("Results.NoContent()", "Microsoft.AspNetCore.Http")]
    [InlineData("Results.BadRequest()", "Microsoft.AspNetCore.Http")]
    public void GetRequiredUsings_DetectsResultsPatterns(
        string content,
        string expectedUsing)
    {
        var result = UsingStatementHelper.GetRequiredUsings(content);
        Assert.Contains(expectedUsing, result);
    }

    [Fact]
    public void GetRequiredUsings_HttpStatusCode_ReturnsSystemNet()
    {
        var content = "if (response.StatusCode == HttpStatusCode.OK)";
        var result = UsingStatementHelper.GetRequiredUsings(content);
        Assert.Contains("System.Net", result);
    }

    [Fact]
    public void GetRequiredUsings_Stream_ReturnsSystemIO()
    {
        var content = "public Stream GetFileStream()";
        var result = UsingStatementHelper.GetRequiredUsings(content);
        Assert.Contains("System.IO", result);
    }
}