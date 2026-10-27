namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A server whose marker sets <c>extensibleEnums</c> generates its string enums as the clients do: a value the
/// specification does not list reaches the handler with <c>IsKnown</c> false instead of a 400, in every place a value is
/// bound - the path, the query (alone, as a list, in a deepObject), a header, a JSON body (a property and a list) and a
/// form field - and a value whose wire form is no C# name (<c>dark-blue</c>) arrives as it was sent, not as its member
/// name. The response writes both back as they are: in its body and in a typed response header.
/// </summary>
[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The form disposes its parts.")]
[Collection(nameof(ServerExtensibleEnumRuntimeTests))]
[CollectionDefinition(nameof(ServerExtensibleEnumRuntimeTests), DisableParallelization = true)]
public sealed class ServerExtensibleEnumRuntimeTests
{
    private const string Marker = """
        {
          "useMinimalApiPackage": "Enabled",
          "useValidationFilter": "Enabled",
          "useGlobalErrorHandler": "Enabled",
          "extensibleEnums": true
        }
        """;

    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /paints/{shade}:
            get:
              operationId: getPaint
              parameters:
                - name: shade
                  in: path
                  required: true
                  schema:
                    $ref: '#/components/schemas/Shade'
                - name: tone
                  in: query
                  schema:
                    $ref: '#/components/schemas/Shade'
                - name: tones
                  in: query
                  schema:
                    type: array
                    items:
                      $ref: '#/components/schemas/Shade'
                - name: X-Shade
                  in: header
                  schema:
                    $ref: '#/components/schemas/Shade'
                - name: filter
                  in: query
                  style: deepObject
                  schema:
                    $ref: '#/components/schemas/PaintFilter'
                - name: finish
                  in: query
                  schema:
                    allOf:
                      - $ref: '#/components/schemas/Shade'
                    default: dark-blue
                - name: mood
                  in: cookie
                  schema:
                    $ref: '#/components/schemas/Shade'
              responses:
                '200':
                  description: The paint
                  headers:
                    X-Shade:
                      schema:
                        $ref: '#/components/schemas/Shade'
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Paint'
          /paints:
            post:
              operationId: createPaint
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Paint'
              responses:
                '200':
                  description: The paint as it was read
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Paint'
          /paint-uploads:
            post:
              operationId: uploadPaint
              requestBody:
                required: true
                content:
                  multipart/form-data:
                    schema:
                      $ref: '#/components/schemas/PaintForm'
              responses:
                '200':
                  description: What the server read
                  content:
                    application/json:
                      schema:
                        type: string
          /paint-xml:
            post:
              operationId: createPaintXml
              requestBody:
                required: true
                content:
                  application/xml:
                    schema:
                      $ref: '#/components/schemas/Paint'
              responses:
                '200':
                  description: The paint as it was read
                  content:
                    application/xml:
                      schema:
                        $ref: '#/components/schemas/Paint'
          /paint-forms:
            post:
              operationId: createPaintForm
              requestBody:
                required: true
                content:
                  application/x-www-form-urlencoded:
                    schema:
                      $ref: '#/components/schemas/PaintForm'
              responses:
                '200':
                  description: What the server read
                  content:
                    application/json:
                      schema:
                        type: string
        components:
          schemas:
            Shade:
              type: string
              enum: [light, dark-blue]
            Paint:
              type: object
              required: [name, shade]
              properties:
                name:
                  type: string
                shade:
                  $ref: '#/components/schemas/Shade'
                others:
                  type: array
                  items:
                    $ref: '#/components/schemas/Shade'
            PaintFilter:
              type: object
              properties:
                shade:
                  $ref: '#/components/schemas/Shade'
            PaintForm:
              type: object
              required: [shade]
              properties:
                shade:
                  $ref: '#/components/schemas/Shade'
                shades:
                  type: array
                  items:
                    $ref: '#/components/schemas/Shade'
        """;

    /// <summary>
    /// Describes every value it was given as <c>Value:IsKnown</c>, and answers them.
    /// </summary>
    private const string HandlerSource = """"
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        internal static class Describe
        {
            public static string Of(Shade? shade)
                => shade is { } value ? $"{value.Value}:{value.IsKnown}" : "-";

            public static string Of(System.Collections.Generic.IEnumerable<Shade>? shades)
                => shades is null ? "-" : string.Join(",", shades.Select(s => Of(s)));
        }

        public sealed class GetPaintHandlerImpl : IGetPaintHandler
        {
            public System.Threading.Tasks.Task<GetPaintResult> ExecuteAsync(GetPaintParameters parameters, System.Threading.CancellationToken cancellationToken = default)
            {
                var name = string.Join(
                    "|",
                    Describe.Of(parameters.Shade),
                    Describe.Of(parameters.Tone),
                    Describe.Of(parameters.Tones),
                    Describe.Of(parameters.ShadeHeader),
                    Describe.Of(parameters.Filter?.Shade),
                    Describe.Of(parameters.Finish),
                    Describe.Of(parameters.Mood));
                var paint = new Paint(Name: name, Shade: parameters.Shade, Others: parameters.Tones?.ToList());
                return System.Threading.Tasks.Task.FromResult(GetPaintResult.Ok(paint, xShade: parameters.ShadeHeader));
            }
        }

        public sealed class CreatePaintHandlerImpl : ICreatePaintHandler
        {
            public System.Threading.Tasks.Task<CreatePaintResult> ExecuteAsync(CreatePaintParameters parameters, System.Threading.CancellationToken cancellationToken = default)
            {
                var paint = parameters.Request;
                return System.Threading.Tasks.Task.FromResult(CreatePaintResult.Ok(paint with { Name = Describe.Of(paint.Shade) + "|" + Describe.Of(paint.Others) }));
            }
        }

        public sealed class CreatePaintFormHandlerImpl : ICreatePaintFormHandler
        {
            public System.Threading.Tasks.Task<CreatePaintFormResult> ExecuteAsync(CreatePaintFormParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(CreatePaintFormResult.Ok(Describe.Of(parameters.Request.Shade) + "|" + Describe.Of(parameters.Request.Shades)));
        }

        public sealed class UploadPaintHandlerImpl : IUploadPaintHandler
        {
            public System.Threading.Tasks.Task<UploadPaintResult> ExecuteAsync(UploadPaintParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(UploadPaintResult.Ok(Describe.Of(parameters.Request.Shade) + "|" + Describe.Of(parameters.Request.Shades)));
        }

        public sealed class CreatePaintXmlHandlerImpl : ICreatePaintXmlHandler
        {
            public System.Threading.Tasks.Task<CreatePaintXmlResult> ExecuteAsync(CreatePaintXmlParameters parameters, System.Threading.CancellationToken cancellationToken = default)
            {
                var paint = parameters.Request;
                return System.Threading.Tasks.Task.FromResult(CreatePaintXmlResult.Ok(paint with { Name = Describe.Of(paint.Shade) + "|" + Describe.Of(paint.Others) }));
            }
        }

        public static class PaintRegistration
        {
            public static void Register(IServiceCollection services)
            {
                services.AddScoped<IUploadPaintHandler, UploadPaintHandlerImpl>();
                services.AddScoped<ICreatePaintXmlHandler, CreatePaintXmlHandlerImpl>();
                services.AddScoped<IGetPaintHandler, GetPaintHandlerImpl>();
                services.AddScoped<ICreatePaintHandler, CreatePaintHandlerImpl>();
                services.AddScoped<ICreatePaintFormHandler, CreatePaintFormHandlerImpl>();
            }
        }
        """";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Parameters_TakeAnyValue_AsItWasSent(
        bool useRequestDelegateGenerator)
    {
        await using var host = await StartAsync(useRequestDelegateGenerator);
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/paints/dark-blue?tone=mauve&tones=light&tones=teal&filter[shade]=dark-blue&finish=teal");
        request.Headers.Add("X-Shade", "ochre");
        request.Headers.Add("Cookie", "mood=sepia");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
        var paint = JsonNode.Parse(body)!;
        Assert.Equal("dark-blue:True|mauve:False|light:True,teal:False|ochre:False|dark-blue:True|teal:False|sepia:False", paint["name"]!.GetValue<string>());
        Assert.Equal("dark-blue", paint["shade"]!.GetValue<string>());
        Assert.Equal(["light", "teal"], paint["others"]!.AsArray().Select(n => n!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal("ochre", Assert.Single(response.Headers.GetValues("X-Shade")));
    }

    [Fact]
    public async Task AParameterNotSent_IsItsDefault()
    {
        await using var host = await StartAsync(useRequestDelegateGenerator: false);
        using var client = new HttpClient { BaseAddress = host.BaseAddress };

        using var response = await client.GetAsync(new Uri("/paints/light", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
        Assert.Equal("light:True|-|-|-|-|dark-blue:True|-", JsonNode.Parse(body)!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task JsonBody_TakesAnyValue_AndAnswersIt()
    {
        await using var host = await StartAsync(useRequestDelegateGenerator: false);
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        using var content = new StringContent("""{"name":"a","shade":"teal","others":["dark-blue","x"]}""", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(new Uri("/paints", UriKind.Relative), content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
        var paint = JsonNode.Parse(body)!;
        Assert.Equal("teal:False|dark-blue:True,x:False", paint["name"]!.GetValue<string>());
        Assert.Equal("teal", paint["shade"]!.GetValue<string>());
        Assert.Equal(["dark-blue", "x"], paint["others"]!.AsArray().Select(n => n!.GetValue<string>()), StringComparer.Ordinal);
    }

    [Fact]
    public async Task FormFields_TakeAnyValue_AsItWasSent()
    {
        await using var host = await StartAsync(useRequestDelegateGenerator: false);
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        using var content = new FormUrlEncodedContent([new("shade", "teal"), new("shades", "dark-blue"), new("shades", "x")]);

        using var response = await client.PostAsync(new Uri("/paint-forms", UriKind.Relative), content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
        Assert.Equal("\"teal:False|dark-blue:True,x:False\"", body);
    }

    [Fact]
    public async Task MultipartFields_TakeAnyValue_AsItWasSent()
    {
        await using var host = await StartAsync(useRequestDelegateGenerator: false);
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        using var content = new MultipartFormDataContent
        {
            { new StringContent("teal"), "shade" },
            { new StringContent("dark-blue"), "shades" },
            { new StringContent("x"), "shades" },
        };

        using var response = await client.PostAsync(new Uri("/paint-uploads", UriKind.Relative), content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
        Assert.Equal("\"teal:False|dark-blue:True,x:False\"", body);
    }

    [Fact]
    public async Task XmlBody_TakesAnyValue_AndAnswersIt()
    {
        await using var host = await StartAsync(useRequestDelegateGenerator: false);
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        using var content = new StringContent("<Paint><name>a</name><shade>teal</shade><others>dark-blue</others><others>x</others></Paint>", Encoding.UTF8, "application/xml");

        using var response = await client.PostAsync(new Uri("/paint-xml", UriKind.Relative), content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
        Assert.Contains("<name>teal:False|dark-blue:True,x:False</name>", body, StringComparison.Ordinal);
        Assert.Contains("<shade>teal</shade>", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARequiredValueThatIsMissing_IsStillA400()
    {
        await using var host = await StartAsync(useRequestDelegateGenerator: false);
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        using var content = new FormUrlEncodedContent([new("shades", "x")]);

        using var response = await client.PostAsync(new Uri("/paint-forms", UriKind.Relative), content, TestContext.Current.CancellationToken);

        Assert.Equal(400, (int)response.StatusCode);
    }

    private static Task<XmlTestHost> StartAsync(
        bool useRequestDelegateGenerator)
        => XmlTestHost.StartAsync(Yaml, HandlerSource, "ErrorBodies.RuntimeTest.PaintRegistration", serverMarker: Marker, useRequestDelegateGenerator: useRequestDelegateGenerator);
}