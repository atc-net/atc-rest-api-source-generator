namespace Atc.Rest.Api.Client.Testing.Tests;

public sealed class HttpResponseMessageFactoryTests
{
    [Fact]
    public async Task Json_SerializesThePayload()
    {
        using var response = HttpResponseMessageFactory.Json(new { name = "Fido" }, HttpStatusCode.Created);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("{\"name\":\"Fido\"}", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProblemDetails_IsProblemJson()
    {
        using var response = HttpResponseMessageFactory.ProblemDetails(HttpStatusCode.NotFound, detail: "No pet 7");

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"status\":404", body, StringComparison.Ordinal);
        Assert.Contains("\"detail\":\"No pet 7\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidationProblem_CarriesTheErrorsKeyedByField()
    {
        using var response = HttpResponseMessageFactory.ValidationProblem(
            new Dictionary<string, string[]>(StringComparer.Ordinal) { ["name"] = ["The name is required."] });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"errors\":{\"name\":[\"The name is required.\"]}", body, StringComparison.Ordinal);
        Assert.Contains("\"status\":400", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Xml_IsApplicationXml()
    {
        using var response = HttpResponseMessageFactory.Xml("<pet><name>Fido</name></pet>");

        Assert.Equal("application/xml", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("<pet><name>Fido</name></pet>", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProblemXml_IsAnRfc9457XmlProblem()
    {
        using var response = HttpResponseMessageFactory.ProblemXml(HttpStatusCode.Conflict, detail: "Pet & owner differ");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+xml", response.Content.Headers.ContentType!.MediaType);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("<problem xmlns=\"urn:ietf:rfc:7807\">", body, StringComparison.Ordinal);
        Assert.Contains("<status>409</status>", body, StringComparison.Ordinal);
        Assert.Contains("<detail>Pet &amp; owner differ</detail>", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task File_HasItsMediaTypeAndFileName()
    {
        using var response = HttpResponseMessageFactory.File([1, 2, 3], "application/pdf", "report.pdf");

        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("report.pdf", response.Content.Headers.ContentDisposition!.FileName);
        Assert.Equal([1, 2, 3], await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ServerSentEvents_WritesOneDataEventPerItem()
    {
        using var response = HttpResponseMessageFactory.ServerSentEvents(new[] { new { id = 1 }, new { id = 2 } });

        Assert.Equal("text/event-stream", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("data: {\"id\":1}\n\ndata: {\"id\":2}\n\n", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void NoContent_HasNoBody()
    {
        using var response = HttpResponseMessageFactory.NoContent();

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, response.Content.Headers.ContentLength);
    }
}