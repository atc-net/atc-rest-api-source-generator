// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// End-to-end body media-type matrix: the generated server runs on Kestrel with handwritten handlers, and every
/// generated client calls it over HTTP - files of every kind (PDF, Excel, PNG, JPEG, octet-stream; as bytes, as a
/// stream, and streamed to the client with <c>x-return-stream</c>), raw uploads, XML bodies (XML-only, a list, a request
/// body, JSON or XML by Accept, an XML error model), a model or a file, and the errors on each (a handler's 404, an
/// exception's 500, a validation 400, a proxy's HTML 502). Each cell compares what the client exposes with what the
/// server wrote: the same bytes (SHA-256), content type and file name; the model's fields; the error's status.
/// </summary>
/// <remarks>
/// Verdicts: <c>pass</c> - the client exposes what the server wrote; <c>fail</c> - something was lost or changed (the
/// loss names it: bytes, content type, file name, fields, status) or the call threw where the contract says it does
/// not. The Axios client runs on Node's default http adapter and on its fetch adapter (a browser's default XHR adapter
/// is not run).
/// </remarks>
public partial class ErrorResponseBodyRuntimeTests
{
    private const string LossBytes = "bytes";
    private const string LossContentType = "content-type";
    private const string LossFileName = "file-name";
    private const string LossStatus = "status";
    private const string LossShape = "shape";

    private const string BodyMediaYaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /files/pdf/{id}:
            get:
              operationId: getPdf
              parameters: [{ $ref: '#/components/parameters/Id' }]
              responses:
                '200': { description: A PDF, content: { application/pdf: { schema: { type: string, format: binary } } } }
                '404': { description: Not found }
          /files/excel/{id}:
            get:
              operationId: getExcel
              parameters: [{ $ref: '#/components/parameters/Id' }]
              responses:
                '200': { description: An Excel workbook, content: { application/vnd.openxmlformats-officedocument.spreadsheetml.sheet: { schema: { type: string, format: binary } } } }
                '404': { description: Not found }
          /files/png/{id}:
            get:
              operationId: getPng
              parameters: [{ $ref: '#/components/parameters/Id' }]
              responses:
                '200': { description: A PNG, content: { image/png: { schema: { type: string, format: binary } } } }
                '404': { description: Not found }
          /files/jpeg/{id}:
            get:
              operationId: getJpeg
              parameters: [{ $ref: '#/components/parameters/Id' }]
              responses:
                '200': { description: A JPEG, content: { image/jpeg: { schema: { type: string, format: binary } } } }
                '404': { description: Not found }
          /files/octet/{id}:
            get:
              operationId: getOctet
              parameters: [{ $ref: '#/components/parameters/Id' }]
              responses:
                '200': { description: Bytes, content: { application/octet-stream: { schema: { type: string, format: binary } } } }
                '404': { description: Not found }
          /files/statement/{id}:
            get:
              operationId: getStatement
              parameters: [{ $ref: '#/components/parameters/Id' }]
              responses:
                '200': { description: A PDF statement, content: { application/pdf: { schema: { type: string, format: binary } } } }
                '404': { description: Not found, content: { application/xml: { schema: { $ref: '#/components/schemas/ErrorInfo' } } } }
          /files/document/{id}:
            get:
              operationId: getDocument
              parameters: [{ $ref: '#/components/parameters/Id' }]
              responses:
                '200': { description: A document of one of several types, content: { application/pdf: { schema: { type: string, format: binary } }, image/png: { schema: { type: string, format: binary } }, image/jpeg: { schema: { type: string, format: binary } } } }
          /files/pdf-stream/{id}:
            get:
              operationId: getPdfStreamed
              x-return-stream: true
              parameters: [{ $ref: '#/components/parameters/Id' }]
              responses:
                '200': { description: A streamed PDF, content: { application/pdf: { schema: { type: string, format: binary } } } }
                '404': { description: Not found }
          /uploads/pdf:
            post:
              operationId: uploadPdf
              requestBody: { required: true, content: { application/pdf: { schema: { type: string, format: binary } } } }
              responses:
                '200': { description: Received, content: { application/json: { schema: { $ref: '#/components/schemas/Receipt' } } } }
          /uploads/png:
            post:
              operationId: uploadPng
              requestBody: { required: true, content: { image/png: { schema: { type: string, format: binary } } } }
              responses:
                '200': { description: Received, content: { application/json: { schema: { $ref: '#/components/schemas/Receipt' } } } }
          /uploads/document:
            post:
              operationId: uploadDocument
              requestBody: { required: true, content: { application/pdf: { schema: { type: string, format: binary } }, image/png: { schema: { type: string, format: binary } }, image/jpeg: { schema: { type: string, format: binary } } } }
              responses:
                '200': { description: Received, content: { application/json: { schema: { $ref: '#/components/schemas/Receipt' } } } }
          /users-xml:
            get:
              operationId: listUsersXml
              responses:
                '200': { description: The users, content: { application/xml: { schema: { type: array, items: { $ref: '#/components/schemas/User' } } } } }
            post:
              operationId: createUserXml
              requestBody: { required: true, content: { application/xml: { schema: { $ref: '#/components/schemas/User' } } } }
              responses:
                '201': { description: Created, content: { application/xml: { schema: { $ref: '#/components/schemas/User' } } } }
                '400': { description: Invalid }
          /users-xml/batch:
            post:
              operationId: createUsersXml
              requestBody: { required: true, content: { application/xml: { schema: { type: array, xml: { name: users }, items: { $ref: '#/components/schemas/User' } } } } }
              responses:
                '200': { description: Created, content: { application/xml: { schema: { type: array, items: { $ref: '#/components/schemas/User' } } } } }
                '400': { description: Invalid }
          /users-xml/{id}:
            get:
              operationId: getUserXml
              parameters: [{ $ref: '#/components/parameters/Id' }]
              responses:
                '200': { description: The user, content: { application/xml: { schema: { $ref: '#/components/schemas/User' } } } }
                '404': { description: Not found, content: { application/xml: { schema: { $ref: '#/components/schemas/ErrorInfo' } } } }
          /users/{id}:
            get:
              operationId: getUser
              parameters: [{ $ref: '#/components/parameters/Id' }]
              responses:
                '200': { description: The user, content: { application/json: { schema: { $ref: '#/components/schemas/User' } }, application/xml: { schema: { $ref: '#/components/schemas/User' } } } }
                '404': { description: Not found }
          /reports/{id}:
            get:
              operationId: getReport
              parameters: [{ $ref: '#/components/parameters/Id' }]
              responses:
                '200': { description: The report or its PDF, content: { application/json: { schema: { $ref: '#/components/schemas/Report' } }, application/pdf: { schema: { type: string, format: binary } } } }
        components:
          parameters:
            Id: { name: id, in: path, required: true, schema: { type: integer } }
          schemas:
            User:
              type: object
              required: [id, name]
              properties:
                id: { type: integer, format: int64, xml: { attribute: true } }
                name: { type: string, maxLength: 20 }
                tags: { type: array, xml: { wrapped: true }, items: { type: string, xml: { name: tag } } }
              xml: { name: user }
            ErrorInfo:
              type: object
              required: [code, message]
              properties:
                code: { type: string, xml: { attribute: true } }
                message: { type: string }
              xml: { name: error }
            Receipt:
              type: object
              required: [sha256, contentType, length]
              properties:
                sha256: { type: string }
                contentType: { type: string }
                length: { type: integer, format: int64 }
            Report:
              type: object
              required: [title]
              properties:
                title: { type: string }
        """;

    private const string BodyMediaHandlerSource = """"
        using System.Linq;
        using System.Security.Cryptography;
        using Microsoft.AspNetCore.Http;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class BodyMediaData
        {
            // Deterministic bytes of every value (0x00 and 0xFF included), longer than one read buffer
            public static byte[] Bytes(string kind)
            {
                var bytes = new byte[70_001];
                var seed = kind.Aggregate(17, (h, c) => (h * 31) + c);
                for (var i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = (byte)((i * 31) + seed + (i >> 8));
                }

                return bytes;
            }

            public static System.Threading.Tasks.Task<T> File<T>(int id, string kind, string extension, System.Func<byte[], string, T> withName, System.Func<System.IO.Stream, T> inline, System.Func<T> notFound)
                => id switch
                {
                    1 => System.Threading.Tasks.Task.FromResult(withName(Bytes(kind), $"Rapport-Å {kind}.{extension}")),
                    2 => System.Threading.Tasks.Task.FromResult(inline(new System.IO.MemoryStream(Bytes(kind)))),
                    404 => System.Threading.Tasks.Task.FromResult(notFound()),
                    _ => throw new System.InvalidOperationException($"boom {kind}"),
                };

            public static User User1 => new(Id: 1, Name: "Ada & <Bob>", Tags: ["a", "b"]);

            public static User User2 => new(Id: 2, Name: "Linus", Tags: []);
        }

        public sealed class GetPdfHandlerImpl : IGetPdfHandler
        {
            public System.Threading.Tasks.Task<GetPdfResult> ExecuteAsync(GetPdfParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => BodyMediaData.File(parameters.Id, "pdf", "pdf", (b, n) => GetPdfResult.Ok(b, fileName: n), s => GetPdfResult.Ok(s), () => GetPdfResult.NotFound());
        }

        public sealed class GetExcelHandlerImpl : IGetExcelHandler
        {
            public System.Threading.Tasks.Task<GetExcelResult> ExecuteAsync(GetExcelParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => BodyMediaData.File(parameters.Id, "excel", "xlsx", (b, n) => GetExcelResult.Ok(b, fileName: n), s => GetExcelResult.Ok(s), () => GetExcelResult.NotFound());
        }

        public sealed class GetPngHandlerImpl : IGetPngHandler
        {
            public System.Threading.Tasks.Task<GetPngResult> ExecuteAsync(GetPngParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => BodyMediaData.File(parameters.Id, "png", "png", (b, n) => GetPngResult.Ok(b, fileName: n), s => GetPngResult.Ok(s), () => GetPngResult.NotFound());
        }

        public sealed class GetJpegHandlerImpl : IGetJpegHandler
        {
            public System.Threading.Tasks.Task<GetJpegResult> ExecuteAsync(GetJpegParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => BodyMediaData.File(parameters.Id, "jpeg", "jpg", (b, n) => GetJpegResult.Ok(b, fileName: n), s => GetJpegResult.Ok(s), () => GetJpegResult.NotFound());
        }

        public sealed class GetOctetHandlerImpl : IGetOctetHandler
        {
            public System.Threading.Tasks.Task<GetOctetResult> ExecuteAsync(GetOctetParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => BodyMediaData.File(parameters.Id, "octet", "bin", (b, n) => GetOctetResult.Ok(b, fileName: n), s => GetOctetResult.Ok(s), () => GetOctetResult.NotFound());
        }

        public sealed class GetStatementHandlerImpl : IGetStatementHandler
        {
            public System.Threading.Tasks.Task<GetStatementResult> ExecuteAsync(GetStatementParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(parameters.Id == 1
                    ? GetStatementResult.Ok(BodyMediaData.Bytes("statement"), fileName: "Rapport-Å statement.pdf")
                    : GetStatementResult.NotFound(new ErrorInfo(Code: "S404", Message: "No statement & more")));
        }

        public sealed class GetDocumentHandlerImpl : IGetDocumentHandler
        {
            public System.Threading.Tasks.Task<GetDocumentResult> ExecuteAsync(GetDocumentParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(GetDocumentResult.Ok(BodyMediaData.Bytes("document"), "image/jpeg", "Rapport-Å document.jpg"));
        }

        public sealed class UploadDocumentHandlerImpl : IUploadDocumentHandler
        {
            // The type that arrived, from the generated parameters (the upload declares several)
            public async System.Threading.Tasks.Task<UploadDocumentResult> ExecuteAsync(UploadDocumentParameters parameters, System.Threading.CancellationToken cancellationToken = default)
            {
                using var buffer = new System.IO.MemoryStream();
                await parameters.File.CopyToAsync(buffer, cancellationToken);
                var bytes = buffer.ToArray();
                return UploadDocumentResult.Ok(new Receipt(
                    Sha256: System.Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                    ContentType: parameters.ContentType,
                    Length: bytes.LongLength));
            }
        }

        public sealed class GetPdfStreamedHandlerImpl : IGetPdfStreamedHandler
        {
            public System.Threading.Tasks.Task<GetPdfStreamedResult> ExecuteAsync(GetPdfStreamedParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => BodyMediaData.File(parameters.Id, "pdf-stream", "pdf", (b, n) => GetPdfStreamedResult.Ok(b, fileName: n), s => GetPdfStreamedResult.Ok(s), () => GetPdfStreamedResult.NotFound());
        }

        public sealed class UploadPdfHandlerImpl(IHttpContextAccessor accessor) : IUploadPdfHandler
        {
            public async System.Threading.Tasks.Task<UploadPdfResult> ExecuteAsync(UploadPdfParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => UploadPdfResult.Ok(await BodyMediaReceipt.ReadAsync(parameters.File, accessor, cancellationToken));
        }

        public sealed class UploadPngHandlerImpl(IHttpContextAccessor accessor) : IUploadPngHandler
        {
            public async System.Threading.Tasks.Task<UploadPngResult> ExecuteAsync(UploadPngParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => UploadPngResult.Ok(await BodyMediaReceipt.ReadAsync(parameters.File, accessor, cancellationToken));
        }

        public static class BodyMediaReceipt
        {
            public static async System.Threading.Tasks.Task<Receipt> ReadAsync(System.IO.Stream file, IHttpContextAccessor accessor, System.Threading.CancellationToken cancellationToken)
            {
                using var buffer = new System.IO.MemoryStream();
                await file.CopyToAsync(buffer, cancellationToken);
                var bytes = buffer.ToArray();
                return new Receipt(
                    Sha256: System.Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                    ContentType: accessor.HttpContext?.Request.ContentType ?? "(none)",
                    Length: bytes.LongLength);
            }
        }

        public sealed class ListUsersXmlHandlerImpl : IListUsersXmlHandler
        {
            public System.Threading.Tasks.Task<ListUsersXmlResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(ListUsersXmlResult.Ok([BodyMediaData.User1, BodyMediaData.User2]));
        }

        public sealed class CreateUserXmlHandlerImpl : ICreateUserXmlHandler
        {
            public System.Threading.Tasks.Task<CreateUserXmlResult> ExecuteAsync(CreateUserXmlParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(CreateUserXmlResult.Created(parameters.Request with { Id = 3 }));
        }

        public sealed class CreateUsersXmlHandlerImpl : ICreateUsersXmlHandler
        {
            // The list arrives as XML (a RequestBodyList<User>): each user back with its new id
            public System.Threading.Tasks.Task<CreateUsersXmlResult> ExecuteAsync(CreateUsersXmlParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(CreateUsersXmlResult.Ok(parameters.Request.Select((user, index) => user with { Id = 10 + index }).ToList()));
        }

        public sealed class GetUserXmlHandlerImpl : IGetUserXmlHandler
        {
            public System.Threading.Tasks.Task<GetUserXmlResult> ExecuteAsync(GetUserXmlParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => parameters.Id switch
                {
                    1 => System.Threading.Tasks.Task.FromResult(GetUserXmlResult.Ok(BodyMediaData.User1)),
                    404 => System.Threading.Tasks.Task.FromResult(GetUserXmlResult.NotFound(new ErrorInfo(Code: "U404", Message: "No user 404 & more"))),
                    _ => throw new System.InvalidOperationException("boom user-xml"),
                };
        }

        public sealed class GetUserHandlerImpl : IGetUserHandler
        {
            public System.Threading.Tasks.Task<GetUserResult> ExecuteAsync(GetUserParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => parameters.Id switch
                {
                    1 => System.Threading.Tasks.Task.FromResult(GetUserResult.Ok(BodyMediaData.User1)),
                    404 => System.Threading.Tasks.Task.FromResult(GetUserResult.NotFound()),
                    _ => throw new System.InvalidOperationException("boom user"),
                };
        }

        public sealed class GetReportHandlerImpl : IGetReportHandler
        {
            public System.Threading.Tasks.Task<GetReportResult> ExecuteAsync(GetReportParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(parameters.Id == 1
                    ? GetReportResult.Ok(new Report(Title: "Q3 report"))
                    : GetReportResult.Ok(BodyMediaData.Bytes("report"), fileName: "Rapport-Å.pdf"));
        }

        public static class BodyMediaRegistration
        {
            public static void Register(IServiceCollection services)
            {
                services.AddHttpContextAccessor();
                services.AddScoped<IGetPdfHandler, GetPdfHandlerImpl>();
                services.AddScoped<IGetExcelHandler, GetExcelHandlerImpl>();
                services.AddScoped<IGetPngHandler, GetPngHandlerImpl>();
                services.AddScoped<IGetJpegHandler, GetJpegHandlerImpl>();
                services.AddScoped<IGetOctetHandler, GetOctetHandlerImpl>();
                services.AddScoped<IGetPdfStreamedHandler, GetPdfStreamedHandlerImpl>();
                services.AddScoped<IGetStatementHandler, GetStatementHandlerImpl>();
                services.AddScoped<IGetDocumentHandler, GetDocumentHandlerImpl>();
                services.AddScoped<IUploadDocumentHandler, UploadDocumentHandlerImpl>();
                services.AddScoped<IUploadPdfHandler, UploadPdfHandlerImpl>();
                services.AddScoped<IUploadPngHandler, UploadPngHandlerImpl>();
                services.AddScoped<IListUsersXmlHandler, ListUsersXmlHandlerImpl>();
                services.AddScoped<ICreateUserXmlHandler, CreateUserXmlHandlerImpl>();
                services.AddScoped<ICreateUsersXmlHandler, CreateUsersXmlHandlerImpl>();
                services.AddScoped<IGetUserXmlHandler, GetUserXmlHandlerImpl>();
                services.AddScoped<IGetUserHandler, GetUserHandlerImpl>();
                services.AddScoped<IGetReportHandler, GetReportHandlerImpl>();
            }
        }
        """";

    /// <summary>
    /// Calls every case through a generated C# client (EndpointPerOperation or TypedClient) against the live host and
    /// describes what the client exposes: a file (SHA-256, content type, file name), a model (its JSON), an error (its
    /// status, type and detail) or an exception.
    /// </summary>
    private const string BodyMediaProbeSource = """"
        using System.Net.Http;
        using System.Security.Cryptography;
        using System.Text.Json;
        using System.Text.Json.Nodes;
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class BodyMediaProbe
        {
            private static readonly JsonSerializerOptions Camel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

            public static async System.Threading.Tasks.Task<Dictionary<string, string>> RunAsync(string baseAddress, string casesJson, bool typed)
            {
                var assembly = typeof(BodyMediaProbe).Assembly;
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
                        var inner = ex is System.Reflection.TargetInvocationException { InnerException: { } e } ? e : ex;
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

            private static object Parameters(Type type, JsonObject item)
            {
                var parameter = type.GetConstructors()[0].GetParameters()[0];
                if (item["id"] is { } id)
                {
                    return Activator.CreateInstance(type, (int)id)!;
                }

                if (item["upload"] is { } upload)
                {
                    return Activator.CreateInstance(type, new Upload((string)upload!, Convert.FromBase64String((string)item["bytes"]!)))!;
                }

                return Activator.CreateInstance(type, JsonSerializer.Deserialize(item["user"]!.ToJsonString(), parameter.ParameterType, Camel))!;
            }

            private static async System.Threading.Tasks.Task<JsonObject> CallEndpointAsync(System.Reflection.Assembly assembly, ServiceProvider provider, string httpClientName, JsonObject item)
            {
                var operation = (string)item["operation"]!;
                var endpointType = assembly.GetTypes().Single(t => t.IsInterface && t.Name == $"I{operation}Endpoint");
                var execute = endpointType.GetMethod("ExecuteAsync")!;
                var arguments = execute.GetParameters().Length == 3
                    ? new object?[] { Parameters(execute.GetParameters()[0].ParameterType, item), httpClientName, System.Threading.CancellationToken.None }
                    : new object?[] { httpClientName, System.Threading.CancellationToken.None };
                var task = (System.Threading.Tasks.Task)execute.Invoke(provider.GetRequiredService(endpointType), arguments)!;
                await task;
                var result = task.GetType().GetProperty("Result")!.GetValue(task)!;

                if (result is StreamBinaryEndpointResponse streamed)
                {
                    using (streamed)
                    {
                        if (streamed.IsSuccess && streamed.Content is { } content)
                        {
                            using var buffer = new System.IO.MemoryStream();
                            await content.CopyToAsync(buffer);
                            return File(buffer.ToArray(), streamed.ContentType, streamed.FileName);
                        }

                        return Error((int)streamed.StatusCode, streamed.ErrorContentObject, streamed.ErrorContent, streamed.Headers);
                    }
                }

                var response = (EndpointResponse)result;
                var type = result.GetType();
                if (response.IsSuccess)
                {
                    if (type.GetProperty("IsOkFile") is { } isFile && (bool)isFile.GetValue(result)!)
                    {
                        return Describe(type.GetProperty("OkFile")!.GetValue(result));
                    }

                    var accessor = new[] { "Ok", "Created" }.First(n => type.GetProperty("Is" + n) is { } p && (bool)p.GetValue(result)!);
                    var described = WithMediaType(Describe(type.GetProperty(accessor + "Content")!.GetValue(result)), response.Headers);
                    described["status"] = (int)response.StatusCode;
                    described["accessor"] = accessor + "Content";
                    return described;
                }

                // The typed error accessor of the status, else the raw body
                foreach (var property in type.GetProperties().Where(p => p.PropertyType == typeof(bool) && p.Name.StartsWith("Is", StringComparison.Ordinal) && p.Name is not ("IsSuccess" or "IsOk" or "IsCreated" or "IsOkFile")))
                {
                    if ((bool)property.GetValue(result)! && type.GetProperty(property.Name[2..] + "Content") is { } content)
                    {
                        return Error((int)response.StatusCode, content.GetValue(result), response.Content, response.Headers);
                    }
                }

                return Error((int)response.StatusCode, null, response.Content, response.Headers);
            }

            private static async System.Threading.Tasks.Task<JsonObject> CallTypedAsync(System.Reflection.Assembly assembly, string baseAddress, JsonObject item)
            {
                var operation = (string)item["operation"]!;
                var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod(operation + "Async") is not null);
                using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };
                var client = Activator.CreateInstance(clientType, http)!;
                var method = clientType.GetMethod(operation + "Async")!;
                var arguments = method.GetParameters().Length == 2
                    ? new object?[] { Parameters(method.GetParameters()[0].ParameterType, item), System.Threading.CancellationToken.None }
                    : new object?[] { System.Threading.CancellationToken.None };
                var task = (System.Threading.Tasks.Task)method.Invoke(client, arguments)!;
                await task;
                var value = task.GetType().GetProperty("Result")!.GetValue(task);
                if (value is EndpointResponse response)
                {
                    if (!response.IsSuccess)
                    {
                        return Error((int)response.StatusCode, null, response.Content, response.Headers);
                    }

                    var described = WithMediaType(Describe(value!.GetType().GetProperty("SuccessContent")!.GetValue(value)), response.Headers);
                    described["status"] = (int)response.StatusCode;
                    return described;
                }

                // The Throw style returns the value only: its media type and status are not exposed
                return Describe(value);
            }

            private static JsonObject Describe(object? value)
            {
                var type = value?.GetType();
                if (type is null)
                {
                    return new JsonObject { ["kind"] = "empty" };
                }

                if (type.Name == "DownloadedFile")
                {
                    return File((byte[])type.GetProperty("Content")!.GetValue(value)!, (string?)type.GetProperty("ContentType")!.GetValue(value), (string?)type.GetProperty("FileName")!.GetValue(value));
                }

                if (type.Name.StartsWith("ContentOrFile", StringComparison.Ordinal))
                {
                    return Describe(type.GetProperty("File")!.GetValue(value) ?? type.GetProperty("Content")!.GetValue(value));
                }

                return new JsonObject { ["kind"] = "model", ["type"] = TypeName(type), ["model"] = JsonSerializer.SerializeToNode(value, type, Camel) };
            }

            private static JsonObject WithMediaType(JsonObject description, IReadOnlyDictionary<string, IEnumerable<string>> headers)
            {
                var contentType = headers.FirstOrDefault(h => string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)).Value?.FirstOrDefault();
                if (contentType is not null && (string?)description["kind"] is "model" or "file")
                {
                    description["mediaType"] = contentType.Split(';')[0].Trim();
                }

                return description;
            }

            private static JsonObject File(byte[] content, string? contentType, string? fileName)
                => new()
                {
                    ["kind"] = "file",
                    ["sha256"] = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
                    ["length"] = content.LongLength,
                    ["contentType"] = contentType?.Split(';')[0].Trim(),
                    ["fileName"] = fileName,
                };

            private static JsonObject Error(int status, object? typed, string? raw, IReadOnlyDictionary<string, IEnumerable<string>> headers)
            {
                var type = typed?.GetType();
                var detail = type?.GetProperty("Detail")?.GetValue(typed) ?? type?.GetProperty("Message")?.GetValue(typed) ?? type?.GetProperty("Title")?.GetValue(typed);
                var errors = type?.GetProperty("Errors")?.GetValue(typed) as System.Collections.IDictionary;
                var contentType = headers.FirstOrDefault(h => string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)).Value?.FirstOrDefault();
                return new JsonObject
                {
                    ["kind"] = "error",
                    ["status"] = status,
                    ["type"] = type is null ? "(raw)" : TypeName(type),
                    ["mediaType"] = contentType?.Split(';')[0].Trim(),
                    ["detail"] = detail is string text ? text : Short(raw ?? string.Empty),
                    ["raw"] = raw,
                    ["fields"] = typed is null || typed is string ? null : JsonSerializer.SerializeToNode(typed, type!, Camel),
                    ["errors"] = errors is null ? null : new JsonArray([.. errors.Keys.Cast<object>().Select(k => (JsonNode)JsonValue.Create(k.ToString())!)]),
                };
            }

            private static string TypeName(Type type)
                => type.IsGenericType ? type.Name.Split('`')[0] + "<" + string.Join(",", type.GetGenericArguments().Select(TypeName)) + ">" : type.Name;

            private static string Short(string text)
                => text.Length > 120 ? text[..120] + "..." : text;

            private sealed class Upload(string contentType, byte[] bytes) : IFileContent
            {
                public string FileName => "upload";

                public string? ContentType => contentType;

                public System.IO.Stream OpenReadStream()
                    => new System.IO.MemoryStream(bytes);
            }
        }
        """";

    internal static readonly (string Id, string Group, string Label, string Marker, bool Typed)[] BodyMediaClients =
    [
        ("cs-epo", "C# EndpointPerOperation", "errorResponseFormat: ProblemDetails", """{ "generationMode": "EndpointPerOperation", "errorResponseFormat": "ProblemDetails" }""", false),
        ("cs-epo-preferXml", "C# EndpointPerOperation", "preferXml: true", """{ "generationMode": "EndpointPerOperation", "errorResponseFormat": "ProblemDetails", "preferXml": true }""", false),
        ("cs-typed-Throw", "C# TypedClient", "typedClientResultStyle: Throw", """{ "generationMode": "TypedClient", "typedClientResultStyle": "Throw" }""", true),
        ("cs-typed-Result", "C# TypedClient", "typedClientResultStyle: Result", """{ "generationMode": "TypedClient", "typedClientResultStyle": "Result" }""", true),
        ("cs-typed-Result-preferXml", "C# TypedClient", "Result, preferXml: true", """{ "generationMode": "TypedClient", "typedClientResultStyle": "Result", "preferXml": true }""", true),
        ("ts-fetch", "TypeScript", "Fetch client", string.Empty, false),
        ("ts-axios", "TypeScript", "Axios client (Node's default http adapter)", string.Empty, false),
        ("ts-axios-fetch", "TypeScript", "Axios client (fetch adapter)", string.Empty, false),
    ];

    /// <summary>
    /// The files the server writes: kind, operation, media type and file extension.
    /// </summary>
    private static readonly (string Kind, string Operation, string MediaType, string Extension)[] BodyMediaFiles =
    [
        ("pdf", "GetPdf", "application/pdf", "pdf"),
        ("excel", "GetExcel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
        ("png", "GetPng", "image/png", "png"),
        ("jpeg", "GetJpeg", "image/jpeg", "jpg"),
        ("octet", "GetOctet", "application/octet-stream", "bin"),
        ("pdf-stream", "GetPdfStreamed", "application/pdf", "pdf"),
        ("statement", "GetStatement", "application/pdf", "pdf"),
        ("document", "GetDocument", "image/jpeg", "jpg"),
    ];

    private static readonly (string Kind, string Operation, string MediaType)[] BodyMediaUploads =
    [
        ("pdf", "UploadPdf", "application/pdf"),
        ("png", "UploadPng", "image/png"),
    ];

    /// <summary>
    /// The users the handlers return (the model every model cell is compared with).
    /// </summary>
    private static readonly JsonObject BodyMediaUser1 = new() { ["id"] = 1, ["name"] = "Ada & <Bob>", ["tags"] = new JsonArray("a", "b") };

    private static readonly JsonObject BodyMediaUser2 = new() { ["id"] = 2, ["name"] = "Linus", ["tags"] = new JsonArray() };

    private static readonly JsonObject BodyMediaNewUser = new() { ["id"] = 3, ["name"] = "Grace", ["tags"] = new JsonArray("x") };

    private static readonly JsonObject BodyMediaReport = new() { ["title"] = "Q3 report" };

    /// <summary>
    /// One call of the matrix.
    /// </summary>
    /// <param name="Name">The case name (unique).</param>
    /// <param name="Group">The report group.</param>
    /// <param name="Segment">The client segment (<c>files</c>, <c>uploads</c>, ...).</param>
    /// <param name="Operation">The operation, PascalCase.</param>
    /// <param name="Method">The HTTP method of the wire capture.</param>
    /// <param name="Path">The path of the wire capture.</param>
    /// <param name="Id">The <c>id</c> argument, or <see langword="null"/>.</param>
    /// <param name="Upload">The uploaded media type (the bytes are <see cref="BodyMediaBytes"/> of it), or <see langword="null"/>.</param>
    /// <param name="User">The XML request body - a user, or a list of users - or <see langword="null"/>.</param>
    /// <param name="Model">The model the handler returns, or <see langword="null"/> (a file, an error).</param>
    /// <param name="UploadTyped">Whether the uploaded file carries its own media type (else the client must send the declared one).</param>
    internal sealed record BodyMediaCase(
        string Name,
        string Group,
        string Segment,
        string Operation,
        string Method,
        string Path,
        int? Id = null,
        string? Upload = null,
        JsonNode? User = null,
        JsonNode? Model = null,
        bool UploadTyped = false);

    /// <summary>
    /// The cases: every file kind (bytes with a file name, a stream inline, a 404, a 500, a proxy's 502), the uploads,
    /// the XML bodies and the model or file.
    /// </summary>
    internal static List<BodyMediaCase> BodyMediaCases()
    {
        var cases = new List<BodyMediaCase>();
        foreach (var file in BodyMediaFiles.Where(f => f.Kind is not ("statement" or "document")))
        {
            var group = file.Kind == "pdf-stream" ? "download (x-return-stream)" : "download";
            foreach (var (id, label) in ((int, string)[])[(1, "bytes + file name"), (2, "stream, inline"), (404, "404 handler"), (500, "500 exception"), (502, "502 proxy HTML")])
            {
                cases.Add(new BodyMediaCase($"{file.Kind} {label}", group, "files", file.Operation, "GET", $"/files/{file.Kind}/{id}", Id: id));
            }
        }

        // A download that declares an XML error model
        cases.Add(new BodyMediaCase("statement bytes + file name", "download", "files", "GetStatement", "GET", "/files/statement/1", Id: 1));
        cases.Add(new BodyMediaCase("statement 404 xml error model", "download", "files", "GetStatement", "GET", "/files/statement/404", Id: 404));

        // A download and an upload that declare several media types: the one that arrived
        cases.Add(new BodyMediaCase("document jpeg (one of several types)", "download", "files", "GetDocument", "GET", "/files/document/1", Id: 1));
        cases.Add(new BodyMediaCase("upload jpeg (one of several types)", "upload", "uploads", "UploadDocument", "POST", "/uploads/document", Upload: "image/jpeg", Model: Receipt("image/jpeg"), UploadTyped: true));

        foreach (var upload in BodyMediaUploads)
        {
            cases.Add(new BodyMediaCase($"upload {upload.Kind}", "upload", "uploads", upload.Operation, "POST", $"/uploads/{upload.Kind}", Upload: upload.MediaType, Model: Receipt(upload.MediaType)));
        }

        cases.Add(new BodyMediaCase("xml-only user", "xml", "usersXml", "GetUserXml", "GET", "/users-xml/1", Id: 1, Model: BodyMediaUser1));
        cases.Add(new BodyMediaCase("xml-only 404 error model", "xml", "usersXml", "GetUserXml", "GET", "/users-xml/404", Id: 404));
        cases.Add(new BodyMediaCase("xml-only 500 exception", "xml", "usersXml", "GetUserXml", "GET", "/users-xml/500", Id: 500));
        cases.Add(new BodyMediaCase("xml-only 502 proxy HTML", "xml", "usersXml", "GetUserXml", "GET", "/users-xml/502", Id: 502));
        cases.Add(new BodyMediaCase("xml list", "xml", "usersXml", "ListUsersXml", "GET", "/users-xml", Model: new JsonArray(BodyMediaUser1.DeepClone(), BodyMediaUser2.DeepClone())));
        cases.Add(new BodyMediaCase("xml request body", "xml", "usersXml", "CreateUserXml", "POST", "/users-xml", User: BodyMediaNewUser, Model: BodyMediaNewUser));
        cases.Add(new BodyMediaCase("xml request body 400 validation", "xml", "usersXml", "CreateUserXml", "POST", "/users-xml", User: new JsonObject { ["id"] = 0, ["name"] = new string('n', 30), ["tags"] = new JsonArray() }));
        cases.Add(new BodyMediaCase("xml list request body", "xml", "usersXml", "CreateUsersXml", "POST", "/users-xml/batch", User: new JsonArray(BodyMediaNewUser.DeepClone(), new JsonObject { ["id"] = 4, ["name"] = "Margaret", ["tags"] = new JsonArray() }), Model: new JsonArray(new JsonObject { ["id"] = 10, ["name"] = "Grace", ["tags"] = new JsonArray("x") }, new JsonObject { ["id"] = 11, ["name"] = "Margaret", ["tags"] = new JsonArray() })));
        cases.Add(new BodyMediaCase("xml list request body 400 validation", "xml", "usersXml", "CreateUsersXml", "POST", "/users-xml/batch", User: new JsonArray(BodyMediaNewUser.DeepClone(), new JsonObject { ["id"] = 0, ["name"] = new string('n', 30), ["tags"] = new JsonArray() })));
        cases.Add(new BodyMediaCase("json or xml user", "json or xml", "users", "GetUser", "GET", "/users/1", Id: 1, Model: BodyMediaUser1));
        cases.Add(new BodyMediaCase("json or xml 404 problem", "json or xml", "users", "GetUser", "GET", "/users/404", Id: 404));
        cases.Add(new BodyMediaCase("json or xml 500 exception", "json or xml", "users", "GetUser", "GET", "/users/500", Id: 500));
        cases.Add(new BodyMediaCase("model or file: the model", "model or file", "reports", "GetReport", "GET", "/reports/1", Id: 1, Model: BodyMediaReport));
        cases.Add(new BodyMediaCase("model or file: the file", "model or file", "reports", "GetReport", "GET", "/reports/2", Id: 2));
        return cases;
    }

    /// <summary>
    /// The receipt an upload of <see cref="BodyMediaBytes"/> gets back.
    /// </summary>
    private static JsonObject Receipt(string mediaType)
    {
        var bytes = BodyMediaBytes(mediaType);
        return new JsonObject
        {
            ["sha256"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(),
            ["contentType"] = mediaType,
            ["length"] = bytes.LongLength,
        };
    }

    /// <summary>
    /// The bytes uploaded as a media type (every value, 0x00 and 0xFF included).
    /// </summary>
    internal static byte[] BodyMediaBytes(string mediaType)
    {
        var bytes = new byte[50_003];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)((i * 17) + mediaType.Length + (i >> 7));
        }

        return bytes;
    }
}