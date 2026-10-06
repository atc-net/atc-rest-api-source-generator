// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// End-to-end request-binding matrix: the generated server runs on Kestrel with handlers that answer with what they
/// were given, and every generated client calls it over HTTP with values in the path, the query string, headers,
/// cookies and the body (JSON, a <c>+json</c> media type, multipart, a URL-encoded form). Each cell compares what the
/// server bound with what the client was given. A second group sends, without a client, the requests no typed client
/// can send - a value of the wrong type, a missing required value, a malformed body, a media type the operation does
/// not take, a body that is too large - and checks the server's answer.
/// </summary>
/// <remarks>
/// Not exercised: cookies from a browser (a page cannot set the Cookie header; the TypeScript cells run on Node).
/// </remarks>
public partial class ErrorResponseBodyRuntimeTests
{
    internal static readonly (string Id, string Group, string Label, string Marker, bool Typed)[] RequestBindingClients =
    [
        ("cs-epo", "C# EndpointPerOperation", "errorResponseFormat: ProblemDetails", """{ "generationMode": "EndpointPerOperation", "errorResponseFormat": "ProblemDetails" }""", false),
        ("cs-typed-Throw", "C# TypedClient", "typedClientResultStyle: Throw", """{ "generationMode": "TypedClient", "typedClientResultStyle": "Throw" }""", true),
        ("cs-typed-Result", "C# TypedClient", "typedClientResultStyle: Result", """{ "generationMode": "TypedClient", "typedClientResultStyle": "Result" }""", true),
        ("ts-fetch", "TypeScript", "Fetch client", string.Empty, false),
        ("ts-axios", "TypeScript", "Axios client (Node's default http adapter)", string.Empty, false),
        ("ts-axios-fetch", "TypeScript", "Axios client (fetch adapter)", string.Empty, false),
    ];

    private const string RequestBindingYaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /bind/path/{s}/{i}/{l}/{n}/{b}/{u}/{d}/{dt}/{e}:
            get:
              operationId: bindPath
              parameters:
                - { name: s, in: path, required: true, schema: { type: string } }
                - { name: i, in: path, required: true, schema: { type: integer, format: int32 } }
                - { name: l, in: path, required: true, schema: { type: integer, format: int64 } }
                - { name: n, in: path, required: true, schema: { type: number, format: double } }
                - { name: b, in: path, required: true, schema: { type: boolean } }
                - { name: u, in: path, required: true, schema: { type: string, format: uuid } }
                - { name: d, in: path, required: true, schema: { type: string, format: date } }
                - { name: dt, in: path, required: true, schema: { type: string, format: date-time } }
                - { name: e, in: path, required: true, schema: { $ref: '#/components/schemas/Color' } }
              responses:
                '200': { description: What the server bound, content: { application/json: { schema: { $ref: '#/components/schemas/Echo' } } } }
          /bind/query:
            get:
              operationId: bindQuery
              parameters:
                - { name: req, in: query, required: true, schema: { type: string } }
                - { name: s, in: query, schema: { type: string } }
                - { name: i, in: query, schema: { type: integer, format: int32 } }
                - { name: l, in: query, schema: { type: integer, format: int64 } }
                - { name: n, in: query, schema: { type: number, format: double } }
                - { name: b, in: query, schema: { type: boolean } }
                - { name: u, in: query, schema: { type: string, format: uuid } }
                - { name: d, in: query, schema: { type: string, format: date } }
                - { name: dt, in: query, schema: { type: string, format: date-time } }
                - { name: e, in: query, schema: { $ref: '#/components/schemas/Color' } }
                - { name: level, in: query, schema: { type: string, enum: [low, very-high] } }
                - { name: size, in: query, schema: { type: integer, format: int32, default: 10 } }
              responses:
                '200': { description: What the server bound, content: { application/json: { schema: { $ref: '#/components/schemas/Echo' } } } }
          /bind/query-arrays:
            get:
              operationId: bindQueryArrays
              parameters:
                - { name: tags, in: query, schema: { type: array, items: { type: string } } }
                - { name: csv, in: query, explode: false, schema: { type: array, items: { type: string } } }
                - { name: pipes, in: query, style: pipeDelimited, explode: false, schema: { type: array, items: { type: string } } }
                - { name: nums, in: query, schema: { type: array, items: { type: integer, format: int32 } } }
                - { name: colors, in: query, schema: { type: array, items: { $ref: '#/components/schemas/Color' } } }
              responses:
                '200': { description: What the server bound, content: { application/json: { schema: { $ref: '#/components/schemas/Echo' } } } }
          /bind/headers:
            get:
              operationId: bindHeaders
              parameters:
                - { name: X-Text, in: header, schema: { type: string } }
                - { name: X-Count, in: header, schema: { type: integer, format: int32 } }
                - { name: X-Flag, in: header, schema: { type: boolean } }
                - { name: X-Id, in: header, schema: { type: string, format: uuid } }
                - { name: X-When, in: header, schema: { type: string, format: date-time } }
                - { name: X-Color, in: header, schema: { $ref: '#/components/schemas/Color' } }
              responses:
                '200': { description: What the server bound, content: { application/json: { schema: { $ref: '#/components/schemas/Echo' } } } }
          /bind/cookies:
            get:
              operationId: bindCookies
              parameters:
                - { name: session, in: cookie, schema: { type: string } }
                - { name: count, in: cookie, schema: { type: integer, format: int32 } }
              responses:
                '200': { description: What the server bound, content: { application/json: { schema: { $ref: '#/components/schemas/Echo' } } } }
          /bind/body/json:
            post:
              operationId: bindJson
              requestBody: { required: true, content: { application/json: { schema: { $ref: '#/components/schemas/Thing' } } } }
              responses:
                '200': { description: What the server bound, content: { application/json: { schema: { $ref: '#/components/schemas/Echo' } } } }
          /bind/body/vendor-json:
            post:
              operationId: bindVendorJson
              requestBody: { required: true, content: { application/vnd.contoso.thing+json: { schema: { $ref: '#/components/schemas/Thing' } } } }
              responses:
                '200': { description: What the server bound, content: { application/json: { schema: { $ref: '#/components/schemas/Echo' } } } }
          /bind/body/multipart:
            post:
              operationId: bindMultipart
              requestBody: { required: true, content: { multipart/form-data: { schema: { $ref: '#/components/schemas/Upload' } } } }
              responses:
                '200': { description: What the server bound, content: { application/json: { schema: { $ref: '#/components/schemas/Echo' } } } }
          /bind/body/form:
            post:
              operationId: bindForm
              requestBody: { required: true, content: { application/x-www-form-urlencoded: { schema: { $ref: '#/components/schemas/Form' } } } }
              responses:
                '200': { description: What the server bound, content: { application/json: { schema: { $ref: '#/components/schemas/Echo' } } } }
        components:
          schemas:
            Color:
              type: string
              enum: [red, dark-blue]
            Echo:
              type: object
              required: [values]
              properties:
                values:
                  type: object
                  additionalProperties: { type: string }
            Upload:
              type: object
              required: [name, file]
              properties:
                name: { type: string }
                count: { type: integer, format: int32 }
                file: { type: string, format: binary }
            Thing:
              type: object
              required: [name]
              properties:
                name: { type: string }
                count: { type: integer, format: int32 }
                ratio: { type: number, format: double }
                when: { type: string, format: date-time }
                day: { type: string, format: date }
                color: { $ref: '#/components/schemas/Color' }
                tags: { type: array, items: { type: string } }
            Form:
              type: object
              required: [name]
              properties:
                name: { type: string }
                count: { type: integer, format: int32 }
                ratio: { type: number, format: double }
                flag: { type: boolean }
                when: { type: string, format: date-time }
                day: { type: string, format: date }
                color: { $ref: '#/components/schemas/Color' }
                tags: { type: array, items: { type: string } }
        """;

    /// <summary>
    /// The same specification as an OpenAPI 3.1 document, with one more body operation that uses what 3.1 adds: a
    /// nullable member and a nullable array as type arrays (<c>type: [string, 'null']</c>), a <c>const</c>, a tuple
    /// (<c>prefixItems</c>) and the <c>examples</c> keyword. The 3.0 operations and cases run unchanged against it.
    /// </summary>
    private static string RequestBindingYaml31 { get; } = Spliced(
        Spliced(
            RequestBindingYaml.Replace("openapi: 3.0.3", "openapi: 3.1.0", StringComparison.Ordinal),
            "  /bind/body/form:",
            """
              /bind/body/json31:
                post:
                  operationId: bindJson31
                  requestBody: { required: true, content: { application/json: { schema: { $ref: '#/components/schemas/Thing31' } } } }
                  responses:
                    '200': { description: What the server bound, content: { application/json: { schema: { $ref: '#/components/schemas/Echo' } } } }
            """),
        "    Form:",
        """
                Thing31:
                  type: object
                  required: [name, kind, pair]
                  properties:
                    name: { type: string }
                    note: { type: [string, 'null'] }
                    tags: { type: [array, 'null'], items: { type: string } }
                    kind: { const: fixed }
                    pair: { $ref: '#/components/schemas/Pair' }
                    count: { type: integer, format: int32, examples: [3] }
                Pair:
                  type: array
                  prefixItems:
                    - { type: string, description: Label }
                    - { type: integer, format: int32, description: Weight }
            """);

    /// <summary>
    /// The handlers of <see cref="RequestBindingYaml31"/>: the 3.0 ones and one for the 3.1 body, which also says
    /// when a nullable member arrived as <c>null</c> (the echo leaves an absent value out).
    /// </summary>
    private static string RequestBindingHandlerSource31 { get; } = Spliced(
        Spliced(
            RequestBindingHandlerSource,
            "public static class RequestBindingRegistration",
            """
            public sealed class BindJson31HandlerImpl(IHttpContextAccessor accessor) : IBindJson31Handler
            {
                public System.Threading.Tasks.Task<BindJson31Result> ExecuteAsync(BindJson31Parameters parameters, System.Threading.CancellationToken cancellationToken = default)
                {
                    var echo = BindEcho.Of(parameters, accessor);
                    if (parameters.Request.Note is null)
                    {
                        echo.Values["Note"] = "null";
                    }

                    if (parameters.Request.Tags is null)
                    {
                        echo.Values["Tags"] = "null";
                    }

                    return System.Threading.Tasks.Task.FromResult(BindJson31Result.Ok(echo));
                }
            }

            """),
        "        services.AddScoped<IBindFormHandler, BindFormHandlerImpl>();",
        "        services.AddScoped<IBindJson31Handler, BindJson31HandlerImpl>();");

    /// <summary>
    /// The text with a block inserted before the anchor line - and a complaint, not a silent no-op, when the anchor
    /// is not there (a raw string literal strips the closing quotes' indentation, so an anchor is easy to get wrong).
    /// </summary>
    private static string Spliced(
        string text,
        string anchor,
        string block)
        => text.Contains(anchor, StringComparison.Ordinal)
            ? text.Replace(anchor, block + Environment.NewLine + anchor, StringComparison.Ordinal)
            : throw new InvalidOperationException($"The anchor '{anchor}' is not in the text to splice into.");

    /// <summary>
    /// Every handler answers with what it was given: each bound value in one canonical text form (a number and a
    /// date in the invariant culture, a date-time as its UTC instant, an enum as its member name, a list joined
    /// with <c>|</c>, a file as its length and hash), keyed by the property name.
    /// </summary>
    private const string RequestBindingHandlerSource = """"
        using System.Linq;
        using System.Reflection;
        using System.Security.Cryptography;
        using Microsoft.AspNetCore.Http;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class BindEcho
        {
            private static readonly System.Globalization.CultureInfo Invariant = System.Globalization.CultureInfo.InvariantCulture;

            public static Echo Of(object? parameters, IHttpContextAccessor? accessor = null)
            {
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                if (parameters is not null)
                {
                    Add(values, parameters);
                }

                if (accessor?.HttpContext?.Request.ContentType is { } contentType)
                {
                    values["@contentType"] = contentType.Split(';')[0].Trim();
                }

                return new Echo(values);
            }

            private static void Add(Dictionary<string, string> values, object source)
            {
                foreach (var property in source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (property.GetIndexParameters().Length > 0 || property.GetValue(source) is not { } value)
                    {
                        continue;
                    }

                    if (IsModel(value))
                    {
                        Add(values, value);
                    }
                    else
                    {
                        values[property.Name] = Text(value);
                    }
                }
            }

            private static bool IsModel(object value)
                => value is not string and not System.Collections.IEnumerable and not IFormFile &&
                   !value.GetType().IsValueType &&
                   value.GetType().Namespace?.EndsWith(".Models", StringComparison.Ordinal) == true;

            private static string Text(object value)
                => value switch
                {
                    string text => text,
                    bool flag => flag ? "true" : "false",
                    double number => number.ToString("R", Invariant),
                    DateOnly date => date.ToString("yyyy-MM-dd", Invariant),
                    DateTimeOffset instant => instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", Invariant),
                    IFormFile file => FileText(file),
                    System.Collections.IEnumerable list => string.Join("|", list.Cast<object>().Select(Text)),
                    IFormattable formattable => formattable.ToString(null, Invariant),
                    _ => value.ToString() ?? string.Empty,
                };

            private static string FileText(IFormFile file)
            {
                using var buffer = new System.IO.MemoryStream();
                file.CopyTo(buffer);
                return $"{file.FileName};{file.ContentType};{buffer.Length};{Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant()[..16]}";
            }
        }

        public sealed class BindPathHandlerImpl : IBindPathHandler
        {
            public System.Threading.Tasks.Task<BindPathResult> ExecuteAsync(BindPathParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(BindPathResult.Ok(BindEcho.Of(parameters)));
        }

        public sealed class BindQueryHandlerImpl : IBindQueryHandler
        {
            public System.Threading.Tasks.Task<BindQueryResult> ExecuteAsync(BindQueryParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(BindQueryResult.Ok(BindEcho.Of(parameters)));
        }

        public sealed class BindQueryArraysHandlerImpl : IBindQueryArraysHandler
        {
            public System.Threading.Tasks.Task<BindQueryArraysResult> ExecuteAsync(BindQueryArraysParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(BindQueryArraysResult.Ok(BindEcho.Of(parameters)));
        }

        public sealed class BindHeadersHandlerImpl : IBindHeadersHandler
        {
            public System.Threading.Tasks.Task<BindHeadersResult> ExecuteAsync(BindHeadersParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(BindHeadersResult.Ok(BindEcho.Of(parameters)));
        }

        public sealed class BindCookiesHandlerImpl : IBindCookiesHandler
        {
            public System.Threading.Tasks.Task<BindCookiesResult> ExecuteAsync(BindCookiesParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(BindCookiesResult.Ok(BindEcho.Of(parameters)));
        }

        public sealed class BindJsonHandlerImpl(IHttpContextAccessor accessor) : IBindJsonHandler
        {
            public System.Threading.Tasks.Task<BindJsonResult> ExecuteAsync(BindJsonParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(BindJsonResult.Ok(BindEcho.Of(parameters, accessor)));
        }

        public sealed class BindVendorJsonHandlerImpl(IHttpContextAccessor accessor) : IBindVendorJsonHandler
        {
            public System.Threading.Tasks.Task<BindVendorJsonResult> ExecuteAsync(BindVendorJsonParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(BindVendorJsonResult.Ok(BindEcho.Of(parameters, accessor)));
        }

        public sealed class BindMultipartHandlerImpl(IHttpContextAccessor accessor) : IBindMultipartHandler
        {
            public System.Threading.Tasks.Task<BindMultipartResult> ExecuteAsync(BindMultipartParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(BindMultipartResult.Ok(BindEcho.Of(parameters, accessor)));
        }

        public sealed class BindFormHandlerImpl(IHttpContextAccessor accessor) : IBindFormHandler
        {
            public System.Threading.Tasks.Task<BindFormResult> ExecuteAsync(BindFormParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(BindFormResult.Ok(BindEcho.Of(parameters, accessor)));
        }

        public static class RequestBindingRegistration
        {
            public static void Register(IServiceCollection services)
            {
                services.AddHttpContextAccessor();
                services.AddScoped<IBindPathHandler, BindPathHandlerImpl>();
                services.AddScoped<IBindQueryHandler, BindQueryHandlerImpl>();
                services.AddScoped<IBindQueryArraysHandler, BindQueryArraysHandlerImpl>();
                services.AddScoped<IBindHeadersHandler, BindHeadersHandlerImpl>();
                services.AddScoped<IBindCookiesHandler, BindCookiesHandlerImpl>();
                services.AddScoped<IBindJsonHandler, BindJsonHandlerImpl>();
                services.AddScoped<IBindVendorJsonHandler, BindVendorJsonHandlerImpl>();
                services.AddScoped<IBindMultipartHandler, BindMultipartHandlerImpl>();
                services.AddScoped<IBindFormHandler, BindFormHandlerImpl>();
            }
        }
        """";

    /// <summary>
    /// Calls every case through a generated C# client against the live host: the arguments are built from the case's
    /// wire values (an enum by its wire name), and the outcome is the server's echo, an error or an exception.
    /// </summary>
    private const string RequestBindingProbeSource = """"
        using System.Net.Http;
        using System.Reflection;
        using System.Runtime.Serialization;
        using System.Text.Json;
        using System.Text.Json.Nodes;
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class RequestBindingProbe
        {
            private static readonly System.Globalization.CultureInfo Invariant = System.Globalization.CultureInfo.InvariantCulture;

            public static async System.Threading.Tasks.Task<Dictionary<string, string>> RunAsync(string baseAddress, string casesJson, bool typed)
            {
                var assembly = typeof(RequestBindingProbe).Assembly;
                var results = new Dictionary<string, string>(StringComparer.Ordinal);
                ServiceProvider? provider = null;
                string? httpClientName = null;
                if (!typed)
                {
                    httpClientName = (string)assembly.GetTypes().Single(t => t.Name == "Constants").GetField("HttpClientName")!.GetValue(null)!;
                    var services = new ServiceCollection();
                    services.AddHttpClient(httpClientName, c => c.BaseAddress = new Uri(baseAddress));
                    foreach (var register in assembly.GetTypes().SelectMany(t => t.GetMethods()).Where(m => m.IsStatic && m.Name.StartsWith("AddErrorBodies", StringComparison.Ordinal) && m.Name.EndsWith("Endpoints", StringComparison.Ordinal)))
                    {
                        register.Invoke(null, [services]);
                    }

                    provider = services.BuildServiceProvider();
                }

                foreach (var item in JsonNode.Parse(casesJson)!.AsArray())
                {
                    var name = (string)item!["name"]!;
                    try
                    {
                        results[name] = (typed
                            ? await CallTypedAsync(assembly, baseAddress, item.AsObject())
                            : await CallEndpointAsync(assembly, provider!, httpClientName!, item.AsObject())).ToJsonString();
                    }
                    catch (Exception ex)
                    {
                        var inner = ex is TargetInvocationException { InnerException: { } e } ? e : ex;
                        results[name] = new JsonObject
                        {
                            ["kind"] = "threw",
                            ["status"] = inner is HttpRequestException { StatusCode: { } code } ? (int)code : null,
                            ["detail"] = inner.GetType().Name + ": " + Short(inner.Message),
                        }.ToJsonString();
                    }
                }

                provider?.Dispose();
                return results;
            }

            private static async System.Threading.Tasks.Task<JsonObject> CallEndpointAsync(Assembly assembly, ServiceProvider provider, string httpClientName, JsonObject item)
            {
                var operation = (string)item["operation"]!;
                var endpointType = assembly.GetTypes().Single(t => t.IsInterface && t.Name == $"I{operation}Endpoint");
                var execute = endpointType.GetMethod("ExecuteAsync")!;
                var arguments = execute.GetParameters().Length == 3
                    ? new object?[] { Build(item["args"], execute.GetParameters()[0].ParameterType), httpClientName, System.Threading.CancellationToken.None }
                    : new object?[] { httpClientName, System.Threading.CancellationToken.None };
                var task = (System.Threading.Tasks.Task)execute.Invoke(provider.GetRequiredService(endpointType), arguments)!;
                await task;
                var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
                var response = (EndpointResponse)result;
                return response.IsSuccess
                    ? Echo(result.GetType().GetProperty("OkContent")!.GetValue(result))
                    : Error((int)response.StatusCode, response.Content);
            }

            private static async System.Threading.Tasks.Task<JsonObject> CallTypedAsync(Assembly assembly, string baseAddress, JsonObject item)
            {
                var operation = (string)item["operation"]!;
                var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod(operation + "Async") is not null);
                using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };
                var client = Activator.CreateInstance(clientType, http)!;
                var method = clientType.GetMethod(operation + "Async")!;
                var arguments = method.GetParameters().Length == 2
                    ? new object?[] { Build(item["args"], method.GetParameters()[0].ParameterType), System.Threading.CancellationToken.None }
                    : new object?[] { System.Threading.CancellationToken.None };
                var task = (System.Threading.Tasks.Task)method.Invoke(client, arguments)!;
                await task;
                var value = task.GetType().GetProperty("Result")!.GetValue(task);
                if (value is EndpointResponse response)
                {
                    return response.IsSuccess
                        ? Echo(value.GetType().GetProperty("SuccessContent")!.GetValue(value))
                        : Error((int)response.StatusCode, response.Content);
                }

                return Echo(value);
            }

            private static JsonObject Echo(object? echo)
            {
                var values = echo?.GetType().GetProperty("Values")?.GetValue(echo) as IDictionary<string, string>;
                return new JsonObject
                {
                    ["kind"] = "echo",
                    ["values"] = new JsonObject((values ?? new Dictionary<string, string>()).Select(kv => new KeyValuePair<string, JsonNode?>(kv.Key, kv.Value))),
                };
            }

            private static JsonObject Error(int status, string? raw)
                => new()
                {
                    ["kind"] = "error",
                    ["status"] = status,
                    ["detail"] = Short(raw ?? string.Empty),
                };

            // A value of a generated type from its wire form
            private static object? Build(JsonNode? node, Type type)
            {
                if (node is null)
                {
                    return null;
                }

                var target = Nullable.GetUnderlyingType(type) ?? type;
                if (target.IsEnum)
                {
                    var wire = (string)node!;
                    var field = target.GetFields(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(f => f.GetCustomAttribute<EnumMemberAttribute>()?.Value == wire);
                    return field is not null ? field.GetValue(null) : Enum.Parse(target, wire, ignoreCase: true);
                }

                // A client's extensible enum: built from its value on the wire
                if (target.IsValueType && target.GetProperty("IsKnown") is not null && target.GetConstructor([typeof(string)]) is { } fromWire)
                {
                    return fromWire.Invoke([(string)node!]);
                }

                if (target == typeof(string))
                {
                    return (string)node!;
                }

                // An untyped member (object): the wire value as it is
                if (target == typeof(object) && node is JsonValue scalar)
                {
                    return scalar.GetValueKind() switch
                    {
                        JsonValueKind.String => scalar.GetValue<string>(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => scalar.TryGetValue<int>(out var whole) ? whole : scalar.GetValue<double>(),
                    };
                }

                if (target == typeof(int))
                {
                    return (int)node!;
                }

                if (target == typeof(long))
                {
                    return (long)node!;
                }

                if (target == typeof(double))
                {
                    return (double)node!;
                }

                if (target == typeof(bool))
                {
                    return (bool)node!;
                }

                if (target == typeof(Guid))
                {
                    return Guid.Parse((string)node!);
                }

                if (target == typeof(DateOnly))
                {
                    return DateOnly.ParseExact((string)node!, "yyyy-MM-dd", Invariant);
                }

                if (target == typeof(DateTimeOffset))
                {
                    return DateTimeOffset.Parse((string)node!, Invariant, System.Globalization.DateTimeStyles.RoundtripKind);
                }

                if (target == typeof(IFileContent))
                {
                    return new Upload((string)node["fileName"]!, (string)node["contentType"]!, Convert.FromBase64String((string)node["base64"]!));
                }

                if (target.IsArray)
                {
                    var items = node.AsArray();
                    var array = Array.CreateInstance(target.GetElementType()!, items.Count);
                    for (var i = 0; i < items.Count; i++)
                    {
                        array.SetValue(Build(items[i], target.GetElementType()!), i);
                    }

                    return array;
                }

                if (target.IsGenericType && typeof(System.Collections.IList).IsAssignableFrom(target))
                {
                    var list = (System.Collections.IList)Activator.CreateInstance(target)!;
                    foreach (var element in node.AsArray())
                    {
                        list.Add(Build(element, target.GetGenericArguments()[0]));
                    }

                    return list;
                }

                // A record: its constructor parameters by name (a header "X-Text" is the parameter "Text"); a tuple
                // record (prefixItems) from the JSON array, by position
                var constructor = target.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
                if (node is JsonArray tuple)
                {
                    return constructor.Invoke([.. constructor.GetParameters().Select((p, i) => Build(tuple[i], p.ParameterType))]);
                }

                var source = node.AsObject();
                return constructor.Invoke([.. constructor.GetParameters().Select(p => Build(Find(source, p.Name!), p.ParameterType))]);
            }

            private static JsonNode? Find(JsonObject source, string parameterName)
            {
                static string Normalize(string name)
                    => new string([.. name.Where(char.IsLetterOrDigit)]).ToLowerInvariant();

                var wanted = Normalize(parameterName);
                foreach (var property in source)
                {
                    var name = Normalize(property.Key);
                    if (name == wanted || (name.StartsWith('x') && name[1..] == wanted))
                    {
                        return property.Value;
                    }
                }

                return null;
            }

            private static string Short(string text)
                => text.Length > 200 ? text[..200] + "..." : text;

            private sealed class Upload(string fileName, string contentType, byte[] bytes) : IFileContent
            {
                public string FileName => fileName;

                public string? ContentType => contentType;

                public System.IO.Stream OpenReadStream()
                    => new System.IO.MemoryStream(bytes);
            }
        }
        """";

    private const string BindGuid = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";

    private const string BindInstant = "2026-03-04T05:06:07+02:00";

    private const string BindInstantUtc = "2026-03-04T03:06:07.000Z";

    /// <summary>
    /// One call of the matrix.
    /// </summary>
    /// <param name="Name">The case name (unique).</param>
    /// <param name="Group">The report group: where the values travel.</param>
    /// <param name="Operation">The operation, PascalCase.</param>
    /// <param name="Path">The path parameters in order (wire values), or <see langword="null"/>.</param>
    /// <param name="Query">The query parameters by name, or <see langword="null"/>.</param>
    /// <param name="Headers">The header parameters by name, or <see langword="null"/>.</param>
    /// <param name="Cookies">The cookie parameters by name, or <see langword="null"/>.</param>
    /// <param name="Body">The JSON request body, or <see langword="null"/>.</param>
    /// <param name="Multipart">The multipart fields (a file as <c>fileName</c>, <c>contentType</c>, <c>base64</c>), or <see langword="null"/>.</param>
    /// <param name="Expected">What the server has to bind: the echo, by property name.</param>
    internal sealed record RequestBindingCase(
        string Name,
        string Group,
        string Operation,
        (string Name, JsonNode Value)[]? Path = null,
        JsonObject? Query = null,
        JsonObject? Headers = null,
        JsonObject? Cookies = null,
        JsonObject? Body = null,
        JsonObject? Multipart = null,
        Dictionary<string, string>? Expected = null);

    /// <summary>
    /// Every generated client, called over HTTP against the generated server, sends its arguments so that the server
    /// binds the same values - in the path, the query string, headers, cookies and the body - and the server answers a
    /// request it cannot bind with the right status (no <c>fail</c> verdict in the request-binding matrix outside
    /// <see cref="KnownRequestBindingFailures"/>).
    /// </summary>
    [Fact]
    [Trait("Category", "TypeScriptToolchain")]
    public async Task EndToEnd_EveryClientSendsWhatTheServerBinds()
    {
        if (!IsNodeAvailable(out var reason))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Fail(reason + " (running under CI where the request-binding matrix is required)");
            }

            Assert.Skip(reason);
        }

        var matrix = await CollectRequestBindingMatrixAsync();
        if (Environment.GetEnvironmentVariable("REQUESTBINDING_E2E_JSON") is { Length: > 0 } dumpPath)
        {
            await File.WriteAllTextAsync(dumpPath, matrix.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), TestContext.Current.CancellationToken);
        }

        var failures = RequestBindingFailingCells(matrix);
        var unexpected = failures.Where(f => !KnownRequestBindingFailures.Contains(f.Key)).ToList();
        var fixedCells = KnownRequestBindingFailures.Where(k => failures.All(f => f.Key != k)).Order(StringComparer.Ordinal).ToList();

        Assert.True(
            unexpected.Count == 0,
            $"{unexpected.Count} failing cells:\n" + string.Join("\n", unexpected.Take(120).Select(f => $"{f.Key}: {f.Why}")));
        Assert.True(
            fixedCells.Count == 0,
            $"{fixedCells.Count} cells pass now. Remove them from {nameof(KnownRequestBindingFailures)}:\n" + string.Join("\n", fixedCells));
    }

    /// <summary>
    /// The same matrix from an OpenAPI 3.1 document (<see cref="RequestBindingYaml31"/>): every 3.0 case binds the
    /// same, and the 3.1 body proves a nullable member and a nullable array as type arrays, <c>null</c> on the wire, a
    /// <c>const</c> and a tuple (<c>prefixItems</c>) through every client - outside
    /// <see cref="KnownRequestBinding31Failures"/>.
    /// </summary>
    [Fact]
    [Trait("Category", "TypeScriptToolchain")]
    public async Task EndToEnd_EveryClientSendsWhatTheServerBinds_WithAnOpenApi31Document()
    {
        if (!IsNodeAvailable(out var reason))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Fail(reason + " (running under CI where the request-binding matrix is required)");
            }

            Assert.Skip(reason);
        }

        var matrix = await CollectRequestBindingMatrixAsync(openApi31: true);
        if (Environment.GetEnvironmentVariable("REQUESTBINDING_E2E_31_JSON") is { Length: > 0 } dumpPath)
        {
            await File.WriteAllTextAsync(dumpPath, matrix.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), TestContext.Current.CancellationToken);
        }

        var failures = RequestBindingFailingCells(matrix);
        var unexpected = failures.Where(f => !KnownRequestBinding31Failures.Contains(f.Key)).ToList();
        var fixedCells = KnownRequestBinding31Failures.Where(k => failures.All(f => f.Key != k)).Order(StringComparer.Ordinal).ToList();

        Assert.True(
            unexpected.Count == 0,
            $"{unexpected.Count} failing cells:\n" + string.Join("\n", unexpected.Take(120).Select(f => $"{f.Key}: {f.Why}")));
        Assert.True(
            fixedCells.Count == 0,
            $"{fixedCells.Count} cells pass now. Remove them from {nameof(KnownRequestBinding31Failures)}:\n" + string.Join("\n", fixedCells));
    }

    /// <summary>
    /// The same matrix against the plain host (<c>MapEndpoints()</c>, no <c>Atc.Rest.MinimalApi</c>): the generated
    /// binding is the same code, so every client's values bind the same; what the server answers a request it cannot
    /// bind comes from the framework and the host wiring there, outside <see cref="KnownPlainRequestBindingFailures"/>.
    /// </summary>
    [Fact]
    [Trait("Category", "TypeScriptToolchain")]
    public async Task EndToEnd_EveryClientSendsWhatTheServerBinds_OnTheMapEndpointsHost()
    {
        if (!IsNodeAvailable(out var reason))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Fail(reason + " (running under CI where the request-binding matrix is required)");
            }

            Assert.Skip(reason);
        }

        var matrix = await CollectRequestBindingMatrixAsync(plainHost: true);
        if (Environment.GetEnvironmentVariable("REQUESTBINDING_E2E_PLAIN_JSON") is { Length: > 0 } dumpPath)
        {
            await File.WriteAllTextAsync(dumpPath, matrix.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), TestContext.Current.CancellationToken);
        }

        var failures = RequestBindingFailingCells(matrix);
        var unexpected = failures.Where(f => !KnownPlainRequestBindingFailures.Contains(f.Key)).ToList();
        var fixedCells = KnownPlainRequestBindingFailures.Where(k => failures.All(f => f.Key != k)).Order(StringComparer.Ordinal).ToList();

        Assert.True(
            unexpected.Count == 0,
            $"{unexpected.Count} failing cells:\n" + string.Join("\n", unexpected.Take(120).Select(f => $"{f.Key}: {f.Why}")));
        Assert.True(
            fixedCells.Count == 0,
            $"{fixedCells.Count} cells pass now. Remove them from {nameof(KnownPlainRequestBindingFailures)}:\n" + string.Join("\n", fixedCells));
    }

    /// <summary>
    /// The verdict is not blind: a changed value, a missing value and a value the client did not send each fail,
    /// and the echo of what was sent passes.
    /// </summary>
    [Fact]
    public void RequestBindingVerdicts_FailAChangedMissingOrExtraValue()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal) { ["S"] = "a b", ["I"] = "1" };

        Assert.Equal(VerdictPass, (string?)RequestBindingVerdict(expected, Echo(("S", "a b"), ("I", "1")))["verdict"]);
        Assert.Equal(VerdictFail, (string?)RequestBindingVerdict(expected, Echo(("S", "a+b"), ("I", "1")))["verdict"]);
        Assert.Equal(VerdictFail, (string?)RequestBindingVerdict(expected, Echo(("S", "a b")))["verdict"]);
        Assert.Equal(VerdictFail, (string?)RequestBindingVerdict(expected, Echo(("S", "a b"), ("I", "1"), ("X", "extra")))["verdict"]);
        Assert.Equal(VerdictFail, (string?)RequestBindingVerdict(expected, new JsonObject { ["kind"] = "error", ["status"] = 400, ["detail"] = "bad" })["verdict"]);
        Assert.Equal(VerdictFail, (string?)RequestBindingVerdict(expected, new JsonObject { ["kind"] = "threw", ["detail"] = "boom" })["verdict"]);

        static JsonObject Echo(params (string Key, string Value)[] values)
            => new()
            {
                ["kind"] = "echo",
                ["values"] = new JsonObject(values.Select(v => new KeyValuePair<string, JsonNode?>(v.Key, v.Value))),
            };
    }

    /// <summary>
    /// A request no generated client can send (a client's arguments are typed): what the server answers a value it
    /// cannot bind, a body it cannot read, a media type it does not take and a body that is too large.
    /// </summary>
    internal sealed record RequestBindingWireCase(
        string Name,
        string Method,
        string Path,
        int Status,
        string? ExpectedKey = null,
        (string Name, string Value)[]? Headers = null,
        string? ContentType = null,
        string? Body = null);

    /// <summary>
    /// The cases of the 3.1 body (<see cref="RequestBindingYaml31"/>): every member with a value - the tuple as a JSON
    /// array, the const, the nullable array - and the nullable members as <c>null</c> on the wire.
    /// </summary>
    internal static List<RequestBindingCase> RequestBindingCases31()
    {
        var pair = new JsonArray("p é", 7);
        return
        [
            new RequestBindingCase(
                "body 3.1: every member with a value (a tuple, a const, a nullable array)",
                "body (OpenAPI 3.1)",
                "BindJson31",
                Body: new JsonObject { ["name"] = "n", ["note"] = "a note", ["tags"] = new JsonArray("a", "b"), ["kind"] = "fixed", ["pair"] = pair, ["count"] = 3 },
                Expected: Expect(("Name", "n"), ("Note", "a note"), ("Tags", "a|b"), ("Kind", "fixed"), ("Label", "p é"), ("Weight", "7"), ("Count", "3"), ("@contentType", "application/json"))),
            new RequestBindingCase(
                "body 3.1: the nullable members null on the wire",
                "body (OpenAPI 3.1)",
                "BindJson31",
                Body: new JsonObject { ["name"] = "n", ["note"] = null, ["tags"] = null, ["kind"] = "fixed", ["pair"] = pair.DeepClone() },
                Expected: Expect(("Name", "n"), ("Note", "null"), ("Tags", "null"), ("Kind", "fixed"), ("Label", "p é"), ("Weight", "7"), ("@contentType", "application/json"))),
            new RequestBindingCase(
                "body 3.1: the nullable members absent",
                "body (OpenAPI 3.1)",
                "BindJson31",
                Body: new JsonObject { ["name"] = "n", ["kind"] = "fixed", ["pair"] = pair.DeepClone() },
                Expected: Expect(("Name", "n"), ("Note", "null"), ("Tags", "null"), ("Kind", "fixed"), ("Label", "p é"), ("Weight", "7"), ("@contentType", "application/json"))),
        ];
    }

    /// <summary>
    /// The cases. The values are picked to break a careless encoder: a string with every character that means
    /// something in a URL, an offset with a <c>+</c>, a negative and a fractional number, an enum whose wire name is
    /// no identifier.
    /// </summary>
    internal static List<RequestBindingCase> RequestBindingCases()
    {
        const string text = "a b&c=d+é?#%;";
        var cases = new List<RequestBindingCase>
        {
            new(
                "path: every scalar type",
                "path",
                "BindPath",
                Path:
                [
                    ("s", text), ("i", -42), ("l", 4294967296123L), ("n", -1.5), ("b", true),
                    ("u", BindGuid), ("d", "2026-03-04"), ("dt", BindInstant), ("e", "dark-blue"),
                ],
                Expected: Expect(("S", text), ("I", "-42"), ("L", "4294967296123"), ("N", "-1.5"), ("B", "true"), ("U", BindGuid), ("D", "2026-03-04"), ("Dt", BindInstantUtc), ("E", "DarkBlue"))),
            new(
                "query: only the required one (the default applies)",
                "query",
                "BindQuery",
                Query: new JsonObject { ["req"] = "r q" },
                Expected: Expect(("Req", "r q"), ("Size", "10"))),
            new(
                "query: a default overridden",
                "query",
                "BindQuery",
                Query: new JsonObject { ["req"] = "r", ["size"] = 3 },
                Expected: Expect(("Req", "r"), ("Size", "3"))),
        };

        // One query case per type, so a cell names what broke
        (string Name, JsonNode Value, string Property, string Expected)[] queryValues =
        [
            ("s", text, "S", text), ("i", -42, "I", "-42"), ("l", 4294967296123L, "L", "4294967296123"), ("n", -1.5, "N", "-1.5"),
            ("b", false, "B", "false"), ("u", BindGuid, "U", BindGuid), ("d", "2026-03-04", "D", "2026-03-04"),
            ("dt", BindInstant, "Dt", BindInstantUtc), ("e", "dark-blue", "E", "DarkBlue"), ("level", "very-high", "Level", "VeryHigh"),
        ];
        foreach (var (name, value, property, expected) in queryValues)
        {
            cases.Add(new RequestBindingCase(
                $"query: {QueryLabel(name)}",
                "query",
                "BindQuery",
                Query: new JsonObject { ["req"] = "r", [name] = value },
                Expected: Expect(("Req", "r"), ("Size", "10"), (property, expected))));
        }

        cases.Add(new RequestBindingCase("query array: strings (form, explode)", "query array", "BindQueryArrays", Query: new JsonObject { ["tags"] = new JsonArray("a b", "c&d") }, Expected: Expect(("Tags", "a b|c&d"))));
        cases.Add(new RequestBindingCase("query array: a string with a comma", "query array", "BindQueryArrays", Query: new JsonObject { ["tags"] = new JsonArray("a,b", "c") }, Expected: Expect(("Tags", "a,b|c"))));
        cases.Add(new RequestBindingCase("query array: integers", "query array", "BindQueryArrays", Query: new JsonObject { ["nums"] = new JsonArray(1, -2) }, Expected: Expect(("Nums", "1|-2"))));
        cases.Add(new RequestBindingCase("query array: enums", "query array", "BindQueryArrays", Query: new JsonObject { ["colors"] = new JsonArray("red", "dark-blue") }, Expected: Expect(("Colors", "Red|DarkBlue"))));
        cases.Add(new RequestBindingCase("query array: form, explode false (csv)", "query array", "BindQueryArrays", Query: new JsonObject { ["csv"] = new JsonArray("x", "y") }, Expected: Expect(("Csv", "x|y"))));
        cases.Add(new RequestBindingCase("query array: pipeDelimited", "query array", "BindQueryArrays", Query: new JsonObject { ["pipes"] = new JsonArray("p", "q") }, Expected: Expect(("Pipes", "p|q"))));

        (string Name, JsonNode Value, string Property, string Expected)[] headerValues =
        [
            ("X-Text", "plain text; v=1", "Text", "plain text; v=1"), ("X-Count", -7, "Count", "-7"), ("X-Flag", true, "Flag", "true"),
            ("X-Id", BindGuid, "Id", BindGuid), ("X-When", BindInstant, "When", BindInstantUtc), ("X-Color", "dark-blue", "Color", "DarkBlue"),
        ];
        foreach (var (name, value, property, expected) in headerValues)
        {
            cases.Add(new RequestBindingCase($"header: {name}", "header", "BindHeaders", Headers: new JsonObject { [name] = value }, Expected: Expect((property, expected))));
        }

        cases.Add(new RequestBindingCase("cookie: a string", "cookie", "BindCookies", Cookies: new JsonObject { ["session"] = "s3ss ion=1" }, Expected: Expect(("Session", "s3ss ion=1"))));
        cases.Add(new RequestBindingCase("cookie: an integer", "cookie", "BindCookies", Cookies: new JsonObject { ["count"] = 5 }, Expected: Expect(("Count", "5"))));

        JsonObject Thing() => new()
        {
            ["name"] = "N é \"q\"", ["count"] = 3, ["ratio"] = 1.5, ["when"] = BindInstant, ["day"] = "2026-03-04", ["color"] = "dark-blue", ["tags"] = new JsonArray("a", "b"),
        };

        var thing = Expect(("Name", "N é \"q\""), ("Count", "3"), ("Ratio", "1.5"), ("When", BindInstantUtc), ("Day", "2026-03-04"), ("Color", "DarkBlue"), ("Tags", "a|b"));
        cases.Add(new RequestBindingCase("body: application/json", "body", "BindJson", Body: Thing(), Expected: With(thing, ("@contentType", "application/json"))));
        cases.Add(new RequestBindingCase("body: application/json, only the required property", "body", "BindJson", Body: new JsonObject { ["name"] = "only" }, Expected: Expect(("Name", "only"), ("@contentType", "application/json"))));
        cases.Add(new RequestBindingCase("body: a +json media type", "body", "BindVendorJson", Body: Thing(), Expected: With(thing, ("@contentType", BindVendorMediaType))));

        var bytes = RequestBindingFileBytes();
        var fileText = $"f é.bin;application/pdf;{bytes.Length};{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant()[..16]}";
        cases.Add(new RequestBindingCase(
            "body: multipart/form-data (fields and a file)",
            "body",
            "BindMultipart",
            Multipart: new JsonObject
            {
                ["name"] = "doc é",
                ["count"] = 2,
                ["file"] = new JsonObject { ["fileName"] = "f é.bin", ["contentType"] = "application/pdf", ["base64"] = Convert.ToBase64String(bytes) },
            },
            Expected: Expect(("Name", "doc é"), ("Count", "2"), ("File", fileText), ("@contentType", "multipart/form-data"))));

        // A URL-encoded form: every scalar formatting (a boolean, a fractional number, an offset, an enum wire name) and
        // a list as repeated keys; the values carry the characters a careless encoder gets wrong (+, &, =, %)
        cases.Add(new RequestBindingCase(
            "body: application/x-www-form-urlencoded (every field)",
            "body",
            "BindForm",
            Body: new JsonObject
            {
                ["name"] = text, ["count"] = -3, ["ratio"] = 1.5, ["flag"] = true, ["when"] = BindInstant, ["day"] = "2026-03-04", ["color"] = "dark-blue", ["tags"] = new JsonArray("a b", "c&d=e"),
            },
            Expected: Expect(("Name", text), ("Count", "-3"), ("Ratio", "1.5"), ("Flag", "true"), ("When", BindInstantUtc), ("Day", "2026-03-04"), ("Color", "DarkBlue"), ("Tags", "a b|c&d=e"), ("@contentType", "application/x-www-form-urlencoded"))));
        cases.Add(new RequestBindingCase(
            "body: application/x-www-form-urlencoded, only the required field",
            "body",
            "BindForm",
            Body: new JsonObject { ["name"] = "only" },
            Expected: Expect(("Name", "only"), ("@contentType", "application/x-www-form-urlencoded"))));

        return cases;

        static string QueryLabel(string name)
            => name switch
            {
                "s" => "a string",
                "i" => "an int32",
                "l" => "an int64",
                "n" => "a double",
                "b" => "a boolean",
                "u" => "a uuid",
                "d" => "a date",
                "dt" => "a date-time",
                "e" => "an enum ($ref)",
                _ => "an enum (inline)",
            };

        static Dictionary<string, string> With(
            Dictionary<string, string> source,
            (string Key, string Value) extra)
            => new(source, StringComparer.Ordinal) { [extra.Key] = extra.Value };
    }

    internal static Dictionary<string, string> Expect(
        params (string Key, string Value)[] values)
        => values.ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);

    /// <summary>
    /// The uploaded file: every byte value.
    /// </summary>
    internal static byte[] RequestBindingFileBytes()
    {
        var bytes = new byte[4_099];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)((i * 13) + (i >> 6));
        }

        return bytes;
    }

    internal static List<RequestBindingWireCase> RequestBindingWireCases()
        =>
        [
            new("path: an integer that is none", "GET", $"/bind/path/a/abc/1/1.5/true/{BindGuid}/2026-03-04/2026-03-04T05:06:07Z/red", 400, "i"),
            new("path: an enum value that is none", "GET", $"/bind/path/a/1/1/1.5/true/{BindGuid}/2026-03-04/2026-03-04T05:06:07Z/purple", 400, "e"),
            new("query: the required parameter is missing", "GET", "/bind/query", 400, "req"),
            new("query: an integer that is none", "GET", "/bind/query?req=r&i=abc", 400, "i"),
            new("query: a date that is none", "GET", "/bind/query?req=r&d=2026-13-45", 400, "d"),
            new("query: a uuid that is none", "GET", "/bind/query?req=r&u=not-a-guid", 400, "u"),
            new("header: an integer that is none", "GET", "/bind/headers", 400, "X-Count", Headers: [("X-Count", "abc")]),
            new("body: malformed JSON", "POST", "/bind/body/json", 400, ContentType: "application/json", Body: "{ \"name\": "),
            new("body: the required property is missing", "POST", "/bind/body/json", 400, "name", ContentType: "application/json", Body: "{ \"count\": 1 }"),
            new("body: a media type the operation does not take", "POST", "/bind/body/json", 415, ContentType: "text/plain", Body: "name=x"),
            new("body: larger than the server allows", "POST", "/bind/body/json", 413, Headers: [("X-Test-Body-Limit", "64")], ContentType: "application/json", Body: "{ \"name\": \"" + new string('x', 500) + "\" }"),
            new("form: the required field is missing", "POST", "/bind/body/form", 400, "name", ContentType: "application/x-www-form-urlencoded", Body: "count=1"),
            new("form: an integer that is none", "POST", "/bind/body/form", 400, "count", ContentType: "application/x-www-form-urlencoded", Body: "name=x&count=abc"),
            new("form: a media type the operation does not take", "POST", "/bind/body/form", 415, ContentType: "application/json", Body: "{ \"name\": \"x\" }"),
        ];

    private static List<(string Key, string Why)> RequestBindingFailingCells(
        JsonObject matrix)
    {
        var failures = new List<(string Key, string Why)>();
        foreach (var node in matrix["cases"]!.AsArray())
        {
            var item = node!.AsObject();
            var name = (string)item["id"]!;
            if ((string?)item["wire"]?["verdict"] == VerdictFail)
            {
                failures.Add(($"{name} [wire]", (string)item["wire"]!["why"]!));
            }

            foreach (var cell in item["clients"]!.AsObject())
            {
                if ((string?)cell.Value!["verdict"] == VerdictFail)
                {
                    failures.Add(($"{name} [{cell.Key}]", (string)cell.Value["why"]!));
                }
            }
        }

        return failures;
    }

    /// <summary>
    /// A cell's verdict: the server's echo against what the case expects it to bind.
    /// </summary>
    private static JsonObject RequestBindingVerdict(
        Dictionary<string, string> expected,
        JsonObject observed)
    {
        var kind = (string?)observed["kind"];
        if (kind != "echo")
        {
            var status = observed["status"] is { } s ? $"{s} " : string.Empty;
            return Cell(VerdictFail, $"{kind}: {status}{(string?)observed["detail"]}", "-");
        }

        var values = observed["values"]!.AsObject().ToDictionary(v => v.Key, v => (string?)v.Value ?? string.Empty, StringComparer.Ordinal);
        var differences = new List<string>();
        foreach (var pair in expected)
        {
            if (!values.TryGetValue(pair.Key, out var actual))
            {
                differences.Add($"{pair.Key}: not bound (expected '{pair.Value}')");
            }
            else if (!string.Equals(actual, pair.Value, StringComparison.Ordinal))
            {
                differences.Add($"{pair.Key}: bound '{actual}', expected '{pair.Value}'");
            }
        }

        differences.AddRange(values.Where(v => !expected.ContainsKey(v.Key)).Select(v => $"{v.Key}: bound '{v.Value}', expected nothing"));

        var summary = string.Join(", ", values.OrderBy(v => v.Key, StringComparer.Ordinal).Select(v => $"{v.Key}={v.Value}"));
        return differences.Count == 0
            ? Cell(VerdictPass, "the server bound what the client was given", summary)
            : Cell(VerdictFail, string.Join("; ", differences), summary);
    }

    private static async Task<JsonObject> CollectRequestBindingMatrixAsync(
        bool plainHost = false,
        bool openApi31 = false)
    {
        var started = DateTimeOffset.Now;
        var yaml = openApi31 ? RequestBindingYaml31 : RequestBindingYaml;
        var cases = RequestBindingCases();
        if (openApi31)
        {
            cases.AddRange(RequestBindingCases31());
        }

        // A request that carries X-Test-Body-Limit gets that body limit, so a "too large" body stays small
        await using var host = await XmlTestHost.StartAsync(
            yaml,
            openApi31 ? RequestBindingHandlerSource31 : RequestBindingHandlerSource,
            "ErrorBodies.RuntimeTest.RequestBindingRegistration",
            app => app.Use(async (context, next) =>
            {
                if (context.Request.Headers.TryGetValue("X-Test-Body-Limit", out var limit) &&
                    context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } feature)
                {
                    feature.MaxRequestBodySize = long.Parse(limit.ToString(), CultureInfo.InvariantCulture);
                }

                await next(context);
            }),
            plainHost: plainHost);

        var casesJson = RequestBindingCasesJson(cases).ToJsonString();
        var rows = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var client in RequestBindingClients.Where(c => c.Marker.Length > 0))
        {
            var sources = Generate(new ApiClientGenerator(), ".atc-rest-api-client", client.Marker, includeMinimalApi: false, yaml);
            var usings = sources
                .Select(s => Regex.Match(s.Source, @"^namespace (?<ns>[\w.]+);", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Groups["ns"].Value)
                .Where(ns => ns.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Select(ns => $"global using {ns};");
            sources.Add(("RequestBindingProbe.cs", string.Join(Environment.NewLine, usings) + Environment.NewLine + RequestBindingProbeSource));
            var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
            var run = (Task<Dictionary<string, string>>)assembly.GetType("ErrorBodies.RuntimeTest.RequestBindingProbe", throwOnError: true)!
                .GetMethod("RunAsync")!
                .Invoke(null, [host.BaseAddress.ToString(), casesJson, client.Typed])!;
            rows[client.Id] = await run;
        }

        var typeScript = RunRequestBindingTypeScriptClients(host.BaseAddress, casesJson, yaml);

        var casesArray = new JsonArray();
        foreach (var bindingCase in cases)
        {
            var clients = new JsonObject();
            foreach (var client in RequestBindingClients)
            {
                var observed = client.Id.StartsWith("ts-", StringComparison.Ordinal)
                    ? typeScript[client.Id].GetValueOrDefault(bindingCase.Name)
                    : rows[client.Id].GetValueOrDefault(bindingCase.Name) is { } row ? JsonNode.Parse(row)!.AsObject() : null;
                clients[client.Id] = HarnessCell(observed) ?? (observed is null
                    ? Cell(VerdictFail, "no result recorded for the case", "-")
                    : RequestBindingVerdict(bindingCase.Expected!, observed));
            }

            casesArray.Add(new JsonObject
            {
                ["id"] = bindingCase.Name,
                ["group"] = bindingCase.Group,
                ["operation"] = bindingCase.Operation,
                ["expects"] = string.Join(", ", bindingCase.Expected!.OrderBy(v => v.Key, StringComparer.Ordinal).Select(v => $"{v.Key}={v.Value}")),
                ["clients"] = clients,
            });
        }

        // What the server answers a request it cannot bind: no generated client sends one
        using (var http = new HttpClient { BaseAddress = host.BaseAddress })
        {
            foreach (var wireCase in RequestBindingWireCases())
            {
                using var request = new HttpRequestMessage(new HttpMethod(wireCase.Method), wireCase.Path);
                foreach (var header in wireCase.Headers ?? [])
                {
                    request.Headers.TryAddWithoutValidation(header.Name, header.Value);
                }

                if (wireCase.Body is not null)
                {
                    request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(wireCase.Body));
                    request.Content.Headers.TryAddWithoutValidation("Content-Type", wireCase.ContentType);
                }

                using var response = await http.SendAsync(request, TestContext.Current.CancellationToken);
                var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                var verdict = RequestBindingWireVerdict(wireCase, (int)response.StatusCode, response.Content.Headers.ContentType?.MediaType, body);
                casesArray.Add(new JsonObject
                {
                    ["id"] = "server: " + wireCase.Name,
                    ["group"] = "a request the server cannot bind",
                    ["operation"] = "(wire)",
                    ["expects"] = $"{wireCase.Status} application/problem+json" + (wireCase.ExpectedKey is null ? string.Empty : $", errors[{wireCase.ExpectedKey}]"),
                    ["wire"] = new JsonObject
                    {
                        ["status"] = (int)response.StatusCode,
                        ["mediaType"] = response.Content.Headers.ContentType?.MediaType,
                        ["body"] = body,
                        ["verdict"] = verdict.Verdict,
                        ["why"] = verdict.Why,
                    },
                    ["clients"] = new JsonObject(),
                });
            }
        }

        return new JsonObject
        {
            ["generatedAt"] = started.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            ["durationSeconds"] = System.Math.Round((DateTimeOffset.Now - started).TotalSeconds, 1),
            ["clients"] = new JsonArray([.. RequestBindingClients.Select(c => (JsonNode)new JsonObject { ["id"] = c.Id, ["group"] = c.Group, ["label"] = c.Label })]),
            ["cases"] = casesArray,
        };
    }

    /// <summary>
    /// The server's answer to a request it cannot bind: the expected status as an RFC 9457 problem, naming the value.
    /// </summary>
    private static (string Verdict, string Why) RequestBindingWireVerdict(
        RequestBindingWireCase wireCase,
        int status,
        string? mediaType,
        string body)
    {
        if (status != wireCase.Status)
        {
            return (VerdictFail, $"status {status}, expected {wireCase.Status}");
        }

        if (!string.Equals(mediaType, "application/problem+json", StringComparison.Ordinal))
        {
            return (VerdictFail, $"media type '{mediaType ?? "(none)"}', expected application/problem+json");
        }

        if (TryParseJson(body) is not JsonObject problem || (int?)problem["status"] != status)
        {
            return (VerdictFail, "the body is no problem with the response's status");
        }

        if (wireCase.ExpectedKey is { } key &&
            (problem["errors"] is not JsonObject errors || !errors.Any(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase))))
        {
            var keys = problem["errors"] is JsonObject found ? string.Join(", ", found.Select(e => e.Key)) : "(no errors)";
            return (VerdictFail, $"errors names [{keys}], expected '{key}'");
        }

        return (VerdictPass, "the expected status as a problem" + (wireCase.ExpectedKey is null ? string.Empty : $", naming '{wireCase.ExpectedKey}'"));
    }

    private static JsonArray RequestBindingCasesJson(
        IEnumerable<RequestBindingCase> cases)
        => new([
            .. cases.Select(c =>
            {
                // The C# probe builds the parameters record from one object; the TypeScript harness gets positional arguments
                var args = new JsonObject();
                var tsArgs = new JsonArray();
                foreach (var (name, value) in c.Path ?? [])
                {
                    args[name] = value.DeepClone();
                    tsArgs.Add(value.DeepClone());
                }

                foreach (var group in (JsonObject?[])[c.Query, c.Headers, c.Cookies])
                {
                    if (group is null)
                    {
                        continue;
                    }

                    foreach (var value in group)
                    {
                        args[value.Key] = value.Value!.DeepClone();
                    }

                    tsArgs.Add(group.DeepClone());
                }

                if (c.Body is not null)
                {
                    args["request"] = c.Body.DeepClone();
                    tsArgs.Add(c.Body.DeepClone());
                }

                if (c.Multipart is not null)
                {
                    args["request"] = c.Multipart.DeepClone();
                    tsArgs.Add(c.Multipart.DeepClone());
                }

                return (JsonNode)new JsonObject
                {
                    ["name"] = c.Name,
                    ["operation"] = c.Operation,
                    ["method"] = char.ToLowerInvariant(c.Operation[0]) + c.Operation[1..],
                    ["args"] = args,
                    ["tsArgs"] = tsArgs,
                };
            }),
        ]);

    private static Dictionary<string, Dictionary<string, JsonObject>> RunRequestBindingTypeScriptClients(
        Uri baseAddress,
        string casesJson,
        string yaml)
    {
        var work = Path.Combine(Path.GetTempPath(), "atc-requestbinding-e2e", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var yamlPath = Path.Combine(work, "ErrorBodies.yaml");
            File.WriteAllText(yamlPath, yaml);

            var cli = FindCliExecutable();
            foreach (var client in (string[])["Fetch", "Axios"])
            {
                var output = Path.Combine(work, client.ToLowerInvariant());
                var generate = RunTool(cli, $"generate client-typescript -s \"{yamlPath}\" -o \"{output}\" --client-type {client} --no-strict", work, TimeSpan.FromMinutes(3));
                Assert.True(generate.ExitCode == 0, $"generate client-typescript ({client}) failed:\n{generate.Output}");
            }

            File.WriteAllText(Path.Combine(work, "package.json"), BodyMediaPackageJson);
            File.WriteAllText(Path.Combine(work, "tsconfig.json"), E2ETsConfig);
            File.WriteAllText(Path.Combine(work, "harness.mts"), RequestBindingHarness);
            File.WriteAllText(Path.Combine(work, "cases.json"), new JsonObject { ["baseUrl"] = baseAddress.ToString().TrimEnd('/'), ["cases"] = JsonNode.Parse(casesJson) }.ToJsonString());

            NodeWorkspace.Link(work);

            var result = new Dictionary<string, Dictionary<string, JsonObject>>(StringComparer.Ordinal);
            foreach (var client in (string[])["fetch", "axios", "axios-fetch"])
            {
                var run = RunTool("npx", $"tsx harness.mts {client}", work, TimeSpan.FromMinutes(5));

                // A harness that did not run is one red column, not the end of the matrix
                result["ts-" + client] = ReadHarnessResults(work, client, run.ExitCode, run.Output, JsonNode.Parse(casesJson)!.AsArray().Select(c => (string)c!["name"]!));
            }

            return result;
        }
        finally
        {
            try
            {
                Directory.Delete(work, recursive: true);
            }
            catch (IOException)
            {
                // node_modules can stay locked for a moment; the temp folder is cleaned up by the OS.
            }
            catch (UnauthorizedAccessException)
            {
                // As above.
            }
        }
    }

    private const string BindVendorMediaType = "application/vnd.contoso.thing+json";

    // Calls every case through the generated ApiService against the live host: the positional arguments of the case
    // (a file as a File of its bytes), and the outcome is the server's echo, an error arm or an exception.
    private const string RequestBindingHarness = """
        import { readFileSync, writeFileSync } from 'node:fs';
        import axios from 'axios';

        const which = process.argv[2];
        const folder = which === 'axios-fetch' ? 'axios' : which;
        const { baseUrl, cases } = JSON.parse(readFileSync('cases.json', 'utf8'));
        if (which === 'axios-fetch') {
          axios.defaults.adapter = 'fetch';
        }

        const { ApiService } = await import(`./${folder}/client/ApiService.ts`);
        const api: any = new ApiService(baseUrl);

        const revive = (value: any): any => {
          if (value && typeof value === 'object' && !Array.isArray(value)) {
            if (typeof value.base64 === 'string' && typeof value.fileName === 'string') {
              return new File([Buffer.from(value.base64, 'base64')], value.fileName, { type: value.contentType });
            }
            return Object.fromEntries(Object.entries(value).map(([k, v]) => [k, revive(v)]));
          }
          return value;
        };

        const results: Record<string, unknown>[] = [];
        for (const c of cases) {
          let record: Record<string, unknown> = { name: c.name };
          try {
            const r = await api.bind[c.method](...c.tsArgs.map(revive)) as any;
            if (r.status === 'ok') {
              record = { ...record, kind: 'echo', values: r.data?.values ?? {} };
            } else {
              const e = r.error;
              const body = e?.body;
              record = {
                ...record,
                kind: 'error',
                status: e?.status ?? null,
                detail: (r.status + ': ' + (typeof body === 'string' ? body : JSON.stringify(body ?? e?.message ?? null))).slice(0, 200),
              };
            }
          } catch (err: any) {
            record = { ...record, kind: 'threw', status: null, detail: String(err?.message ?? err).slice(0, 200) };
          }
          results.push(record);
        }

        writeFileSync(`results-${which}.json`, JSON.stringify(results));
        """;

    /// <summary>
    /// The cells that fail today, as <c>case [client]</c>: a cell not listed here must pass, and a listed cell that passes
    /// fails the test asking to be removed. The matrix started with 46 (enum wire names in parameters, cookies, a +json
    /// body's media type, an array item with a comma, a 400 that did not name the parameter, a file's content type,
    /// and Axios on its fetch adapter labelling a FormData body from Node <c>application/x-www-form-urlencoded</c>,
    /// which the generated client now names multipart itself) - all fixed.
    /// </summary>
    private static readonly HashSet<string> KnownRequestBindingFailures = new(StringComparer.Ordinal)
    {
    };

    /// <summary>
    /// The cells that fail today on the plain host (<c>MapEndpoints()</c>, no <c>Atc.Rest.MinimalApi</c>), as
    /// <c>case [client]</c>, each with the reason it is accepted.
    /// </summary>
    /// <summary>
    /// The cells that fail today from the OpenAPI 3.1 document, as <c>case [client]</c>, each with the reason it is
    /// accepted.
    /// </summary>
    private static readonly HashSet<string> KnownRequestBinding31Failures = new(StringComparer.Ordinal)
    {
    };

    private static readonly HashSet<string> KnownPlainRequestBindingFailures = new(StringComparer.Ordinal)
    {
        // Nothing validates a request without Atc.Rest.MinimalApi's ValidationFilter: the framework's own 400 for a
        // missing required query parameter names no parameter (a ProblemDetails from the host wiring, no `errors`),
        // and a body missing a required property reaches the handler as null. Documented under "Without
        // Atc.Rest.MinimalApi" in Working with Error Responses; .NET 10's AddValidation() is the way to validate there.
        "server: query: the required parameter is missing [wire]",
        "server: body: the required property is missing [wire]",
    };
}