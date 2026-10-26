namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Every factory of a generated <c>{Operation}Result</c>, for every status code: the result reads back what it writes -
/// <c>StatusCode</c>, <c>Value</c>, the <c>Location</c> and the response headers (<c>Headers</c>: the declared ones,
/// <c>Location</c>, <c>Retry-After</c>) - whatever it is wrapped in, so a handler test asserts them without casting to a
/// wrapper type of a Results namespace. An XML body (only XML, or negotiated with JSON) reads back its model too.
/// </summary>
public sealed class ResultHeadersTests
{
    private const string Yaml = """
        openapi: 3.1.0
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /s200:
            get:
              operationId: s200
              responses:
                '200':
                  description: Status 200
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Order'
          /s201:
            get:
              operationId: s201
              responses:
                '201':
                  description: Status 201
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Order'
          /s202:
            get:
              operationId: s202
              responses:
                '202':
                  description: Status 202
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Order'
          /s203:
            get:
              operationId: s203
              responses:
                '203':
                  description: Status 203
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Order'
          /s204:
            get:
              operationId: s204
              responses:
                '204':
                  description: Status 204
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s205:
            get:
              operationId: s205
              responses:
                '205':
                  description: Status 205
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s206:
            get:
              operationId: s206
              responses:
                '206':
                  description: Status 206
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Order'
          /s300:
            get:
              operationId: s300
              responses:
                '300':
                  description: Status 300
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s301:
            get:
              operationId: s301
              responses:
                '301':
                  description: Status 301
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s302:
            get:
              operationId: s302
              responses:
                '302':
                  description: Status 302
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s303:
            get:
              operationId: s303
              responses:
                '303':
                  description: Status 303
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s304:
            get:
              operationId: s304
              responses:
                '304':
                  description: Status 304
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s307:
            get:
              operationId: s307
              responses:
                '307':
                  description: Status 307
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s308:
            get:
              operationId: s308
              responses:
                '308':
                  description: Status 308
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s400:
            get:
              operationId: s400
              responses:
                '400':
                  description: Status 400
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s401:
            get:
              operationId: s401
              responses:
                '401':
                  description: Status 401
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s402:
            get:
              operationId: s402
              responses:
                '402':
                  description: Status 402
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s403:
            get:
              operationId: s403
              responses:
                '403':
                  description: Status 403
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s404:
            get:
              operationId: s404
              responses:
                '404':
                  description: Status 404
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s405:
            get:
              operationId: s405
              responses:
                '405':
                  description: Status 405
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s406:
            get:
              operationId: s406
              responses:
                '406':
                  description: Status 406
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s407:
            get:
              operationId: s407
              responses:
                '407':
                  description: Status 407
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s408:
            get:
              operationId: s408
              responses:
                '408':
                  description: Status 408
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s409:
            get:
              operationId: s409
              responses:
                '409':
                  description: Status 409
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s410:
            get:
              operationId: s410
              responses:
                '410':
                  description: Status 410
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s411:
            get:
              operationId: s411
              responses:
                '411':
                  description: Status 411
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s412:
            get:
              operationId: s412
              responses:
                '412':
                  description: Status 412
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s413:
            get:
              operationId: s413
              responses:
                '413':
                  description: Status 413
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s414:
            get:
              operationId: s414
              responses:
                '414':
                  description: Status 414
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s415:
            get:
              operationId: s415
              responses:
                '415':
                  description: Status 415
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s416:
            get:
              operationId: s416
              responses:
                '416':
                  description: Status 416
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s417:
            get:
              operationId: s417
              responses:
                '417':
                  description: Status 417
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s418:
            get:
              operationId: s418
              responses:
                '418':
                  description: Status 418
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s421:
            get:
              operationId: s421
              responses:
                '421':
                  description: Status 421
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s422:
            get:
              operationId: s422
              responses:
                '422':
                  description: Status 422
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s423:
            get:
              operationId: s423
              responses:
                '423':
                  description: Status 423
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s424:
            get:
              operationId: s424
              responses:
                '424':
                  description: Status 424
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s425:
            get:
              operationId: s425
              responses:
                '425':
                  description: Status 425
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s426:
            get:
              operationId: s426
              responses:
                '426':
                  description: Status 426
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s428:
            get:
              operationId: s428
              responses:
                '428':
                  description: Status 428
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s429:
            get:
              operationId: s429
              responses:
                '429':
                  description: Status 429
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s431:
            get:
              operationId: s431
              responses:
                '431':
                  description: Status 431
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s451:
            get:
              operationId: s451
              responses:
                '451':
                  description: Status 451
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s500:
            get:
              operationId: s500
              responses:
                '500':
                  description: Status 500
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s501:
            get:
              operationId: s501
              responses:
                '501':
                  description: Status 501
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s502:
            get:
              operationId: s502
              responses:
                '502':
                  description: Status 502
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s503:
            get:
              operationId: s503
              responses:
                '503':
                  description: Status 503
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s504:
            get:
              operationId: s504
              responses:
                '504':
                  description: Status 504
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /s505:
            get:
              operationId: s505
              responses:
                '505':
                  description: Status 505
                  headers:
                    X-Contoso-Trace:
                      schema:
                        type: string
          /xml-only:
            get:
              operationId: xmlOnly
              responses:
                '200':
                  description: XML
                  content:
                    application/xml:
                      schema:
                        $ref: '#/components/schemas/Order'
          /negotiated:
            get:
              operationId: negotiated
              responses:
                '200':
                  description: JSON or XML
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Order'
                    application/xml:
                      schema:
                        $ref: '#/components/schemas/Order'
        components:
          schemas:
            Order:
              type: object
              properties:
                id:
                  type: string
        """;

    private const string Location = "/orders/o-1";
    private const string Trace = "t-1";

    private static readonly int[] Codes =
    [
        200, 201, 202, 203, 204, 205, 206, 300, 301, 302, 303, 304, 307, 308,
        400, 401, 402, 403, 404, 405, 406, 407, 408, 409, 410, 411, 412, 413,
        414, 415, 416, 417, 418, 421, 422, 423, 424, 425, 426, 428, 429, 431,
        451, 500, 501, 502, 503, 504, 505,
    ];

    [Fact]
    public void EveryFactory_OfEveryStatus_ReadsBackItsStatusValueLocationAndHeaders()
    {
        // Arrange
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true, Yaml);
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: true);
        var orderType = assembly.GetTypes().Single(t => t.Name == "Order");
        var failures = new List<string>();
        var calls = 0;

        // Act
        foreach (var code in Codes)
        {
            var resultType = assembly.GetTypes().Single(t => t.Name == $"S{code}Result");
            foreach (var factory in resultType.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.ReturnType == resultType && !m.IsSpecialName))
            {
                var parameters = factory.GetParameters();
                var arguments = parameters.Select(p => ArgumentFor(p, orderType)).ToArray();
                var result = factory.Invoke(null, arguments)!;
                calls++;
                Check(failures, $"S{code}Result.{factory.Name}({string.Join(", ", parameters.Select(p => p.Name))})", result, code, parameters, arguments, orderType);
            }
        }

        foreach (var name in new[] { "XmlOnlyResult", "NegotiatedResult" })
        {
            var resultType = assembly.GetTypes().Single(t => t.Name == name);
            var ok = resultType.GetMethods(BindingFlags.Public | BindingFlags.Static).Single(m => m.Name == "Ok" && m.ReturnType == resultType);
            var arguments = ok.GetParameters().Select(p => ArgumentFor(p, orderType)).ToArray();
            Check(failures, $"{name}.Ok", ok.Invoke(null, arguments)!, 200, ok.GetParameters(), arguments, orderType);
        }

        // Assert
        Assert.True(calls > Codes.Length, $"Only {calls} factories were called.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static object? ArgumentFor(
        ParameterInfo parameter,
        Type orderType)
    {
        var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
        if (type == orderType)
        {
            return Activator.CreateInstance(orderType, "o-1");
        }

        if (type == typeof(Uri))
        {
            return new Uri(Location, UriKind.Relative);
        }

        if (type == typeof(int))
        {
            return 7;
        }

        if (type == typeof(string))
        {
            return IsLocation(parameter) ? Location
                : string.Equals(parameter.Name, "xContosoTrace", StringComparison.Ordinal) ? Trace
                : "text";
        }

        return parameter.HasDefaultValue ? parameter.DefaultValue : null;
    }

    private static bool IsLocation(ParameterInfo parameter)
        => parameter.Name is "location" or "uri";

    private static void Check(
        List<string> failures,
        string call,
        object result,
        int code,
        ParameterInfo[] parameters,
        object?[] arguments,
        Type orderType)
    {
        dynamic r = result;
        int? status = r.StatusCode;
        object? value = r.Value;
        string? location = r.Location;
        IReadOnlyDictionary<string, string> headers = r.Headers;

        if (status != code)
        {
            failures.Add($"{call}: StatusCode {status?.ToString(CultureInfo.InvariantCulture) ?? "null"}, not {code}");
        }

        if (parameters.Any(p => p.ParameterType == orderType) && (value is null || value.GetType() != orderType))
        {
            failures.Add($"{call}: Value {value?.GetType().Name ?? "null"}, not the Order");
        }

        if (parameters.Any(IsLocation) && !string.Equals(location, Location, StringComparison.Ordinal))
        {
            failures.Add($"{call}: Location {location ?? "null"}");
        }

        if (location is not null && !(headers.TryGetValue("Location", out var header) && header == location))
        {
            failures.Add($"{call}: Location {location} is not in Headers");
        }

        if (parameters.Any(p => p.Name == "xContosoTrace") && !(headers.TryGetValue("X-Contoso-Trace", out var trace) && trace == Trace))
        {
            failures.Add($"{call}: X-Contoso-Trace missing from Headers [{string.Join(", ", headers.Select(h => h.Key + "=" + h.Value))}]");
        }

        var retryAfter = Array.FindIndex(parameters, p => p.Name == "retryAfterSeconds");
        if (retryAfter >= 0 && arguments[retryAfter] is int seconds && !(headers.TryGetValue("Retry-After", out var retry) && retry == seconds.ToString(CultureInfo.InvariantCulture)))
        {
            failures.Add($"{call}: Retry-After missing from Headers");
        }
    }
}