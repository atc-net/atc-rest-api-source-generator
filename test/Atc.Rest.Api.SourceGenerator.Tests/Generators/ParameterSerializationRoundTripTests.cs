namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Round-trip tests for parameter serialization: the typed client emits form-explode repeated
/// keys (<c>?tags=a&amp;tags=b</c>) and the generated server binds them via <c>ParsableList&lt;T&gt;</c>.
/// Part D of the parameter-serialization feature.
/// </summary>
public class ParameterSerializationRoundTripTests
{
    [Fact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "HttpClient takes ownership of the handler and disposes it.")]
    public async Task Client_FormExplodeArrays_EmitRepeatedQueryKeys()
    {
        // Compile + load the generated typed client. This also proves the generated code COMPILES —
        // in particular that the $ref-to-array param (ids) is typed as an enumerable, not the
        // undefined bare ref name IdList (the silent-wrong gap from Part A).
        var assembly = CompilationVerificationHarness.EmitAndLoad(
            CompilationVerificationHarness.RunClient("ParameterSerialization", "ParameterSerialization.yaml"));

        // The Roslyn typed-client generator emits a per-segment client (ItemsClient) carrying the
        // ListItemsAsync method; locate it by the method rather than a fixed class name.
        var clientType = assembly.GetTypes().Single(t => t.GetMethod("ListItemsAsync") is not null);
        var parametersType = assembly.GetTypes().Single(t => t.Name == "ListItemsParameters");

        // Capture the outgoing request URI without a real server.
        Uri? capturedUri = null;
        var handler = new CapturingHandler(req =>
        {
            capturedUri = req.RequestUri;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json"),
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        var client = Activator.CreateInstance(clientType, httpClient);

        // ListItemsParameters(List<string>? Tags, string? Q, List<string>? Legacy, <ids> Ids).
        // Build positional args matching the primary constructor's parameter order/types.
        var ctor = parametersType.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        var ctorArgs = ctor.GetParameters()
            .Select(p => BuildArgFor(p.Name, p.ParameterType))
            .ToArray();
        var parameters = ctor.Invoke(ctorArgs);

        var method = clientType.GetMethod("ListItemsAsync");
        var task = (Task)method.Invoke(client, [parameters, CancellationToken.None]);
        await task;

        Assert.NotNull(capturedUri);
        var query = capturedUri.Query;

        // Form-explode repeated keys for both the inline array (tags) and the $ref array (ids).
        Assert.Contains("tags=a&tags=b", query, StringComparison.Ordinal);
        Assert.Contains("ids=x&ids=y", query, StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds a constructor argument for the generated <c>ListItemsParameters</c> record: the array
    /// params (tags, ids) get two-element lists; everything else gets null.
    /// </summary>
    private static object? BuildArgFor(
        string parameterName,
        Type parameterType)
    {
        if (string.Equals(parameterName, "Tags", StringComparison.OrdinalIgnoreCase))
        {
            return CreateStringList(parameterType, "a", "b");
        }

        if (string.Equals(parameterName, "Ids", StringComparison.OrdinalIgnoreCase))
        {
            return CreateStringList(parameterType, "x", "y");
        }

        return null;
    }

    /// <summary>
    /// Materializes a <c>List&lt;string&gt;</c> (the runtime type of the generated array param)
    /// populated with the given values.
    /// </summary>
    private static object CreateStringList(
        Type listType,
        params string[] values)
    {
        var concrete = Nullable.GetUnderlyingType(listType) ?? listType;
        var list = (System.Collections.IList)Activator.CreateInstance(concrete);
        foreach (var v in values)
        {
            list.Add(v);
        }

        return list;
    }

    [Fact]
    public async Task Server_ParsableList_BindsRepeatedQueryKeys()
    {
        // The client emits form-explode repeated keys: ?tags=a&tags=b.
        // This proves the SERVER's ParsableList<string> binds that exact wire format.
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateSlimBuilder();
        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");

        Microsoft.AspNetCore.Builder.EndpointRouteBuilderExtensions.MapGet(
            app,
            "/items",
            ([Microsoft.AspNetCore.Mvc.FromQuery(Name = "tags")] ParsableList<string> tags)
                => tags.Count);

        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var address = new Uri(app.Urls.First());
            using var client = new System.Net.Http.HttpClient { BaseAddress = address };
            var count = await client.GetStringAsync(
                new Uri("/items?tags=a&tags=b", UriKind.Relative),
                TestContext.Current.CancellationToken);

            Assert.Equal("2", count);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
            await app.DisposeAsync();
        }
    }

    /// <summary>
    /// An <see cref="System.Net.Http.HttpMessageHandler"/> that captures the outgoing request and
    /// returns a canned response, so the emitted client can be driven without a live server.
    /// </summary>
    private sealed class CapturingHandler(
        Func<System.Net.Http.HttpRequestMessage, System.Net.Http.HttpResponseMessage> respond)
        : System.Net.Http.HttpMessageHandler
    {
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }

    /// <summary>
    /// A compile-time copy of the generated <c>ParsableList&lt;T&gt;</c> server utility type. The
    /// integration snapshot test proves the generated source is byte-identical to this shape, so
    /// binding this type in a real host proves the generated server binds the same wire format.
    /// </summary>
    /// <typeparam name="T">The element type, which must itself be parsable.</typeparam>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Mirrors the generated ParsableList<T>.")]
    [SuppressMessage("Design", "CA1034:Do not nest type", Justification = "Test-local copy of the generated type.")]
    public sealed class ParsableList<T> : List<T>, IParsable<ParsableList<T>>
        where T : IParsable<T>
    {
        public static ParsableList<T> Parse(
            string s,
            IFormatProvider? provider)
        {
            var list = new ParsableList<T>();
            if (!string.IsNullOrEmpty(s))
            {
                foreach (var item in s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    list.Add(T.Parse(item, provider));
                }
            }

            return list;
        }

        public static bool TryParse(
            string? s,
            IFormatProvider? provider,
            out ParsableList<T> result)
        {
            result = new ParsableList<T>();
            if (string.IsNullOrEmpty(s))
            {
                return true;
            }

            foreach (var item in s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!T.TryParse(item, provider, out var parsed))
                {
                    return false;
                }

                result.Add(parsed);
            }

            return true;
        }
    }
}