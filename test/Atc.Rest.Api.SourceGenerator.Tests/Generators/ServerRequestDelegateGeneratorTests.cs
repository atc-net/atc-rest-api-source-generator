namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The generated server builds with ASP.NET Core's Request Delegate Generator (RDG) - what a Native AOT build binds
/// the endpoints with - and binds every parameter the same way through RDG's request delegates as through
/// <c>RequestDelegateFactory</c>: an absent optional value is null, an absent value with a default is the default, a
/// missing required value is a 400, and a sent value is the value. RDG wrote <c>typeof(string?)</c> for a nullable
/// reference-type parameter of a positional parameters record (CS8639), so the server did not build.
/// </summary>
[Collection(nameof(ServerRequestDelegateGeneratorTests))]
[CollectionDefinition(nameof(ServerRequestDelegateGeneratorTests), DisableParallelization = true)]
public sealed class ServerRequestDelegateGeneratorTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /items:
            get:
              operationId: listItems
              parameters:
                - name: search
                  in: query
                  schema: { type: string }
                - name: sort
                  in: query
                  schema: { type: string, default: name }
                - name: tag
                  in: query
                  required: true
                  schema: { type: string }
                - name: tags
                  in: query
                  schema:
                    type: array
                    items: { type: string }
                - name: legacy
                  in: query
                  deprecated: true
                  schema: { type: string }
                - name: limit
                  in: query
                  schema: { type: integer }
              responses:
                '200':
                  description: The bound values
                  content:
                    text/plain:
                      schema: { type: string }
        """;

    private const string HandlerSource = """"
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public sealed class ListItemsHandlerImpl : IListItemsHandler
        {
        #pragma warning disable CS0618 // the deprecated parameter is read on purpose
            public System.Threading.Tasks.Task<ListItemsResult> ExecuteAsync(ListItemsParameters parameters, System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(ListItemsResult.Ok(
                    $"search={parameters.Search ?? "<null>"} sort={parameters.Sort ?? "<null>"} tag={parameters.Tag} " +
                    $"tags={(parameters.Tags is null ? "<null>" : string.Join("|", parameters.Tags))} legacy={parameters.Legacy ?? "<null>"} " +
                    $"limit={parameters.Limit?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<null>"}"));
        #pragma warning restore CS0618
        }

        public static class ItemsRegistration
        {
            public static void Register(IServiceCollection services)
                => services.AddScoped<IListItemsHandler, ListItemsHandlerImpl>();
        }
        """";

    public static TheoryData<bool, string, int, string> Cases
    {
        get
        {
            var data = new TheoryData<bool, string, int, string>();
            foreach (var useRequestDelegateGenerator in new[] { false, true })
            {
                data.Add(useRequestDelegateGenerator, "/items?tag=t", 200, "search=<null> sort=name tag=t tags=<null> legacy=<null> limit=<null>");
                data.Add(useRequestDelegateGenerator, "/items?tag=t&search=s&sort=x&tags=a&tags=b&legacy=l&limit=3", 200, "search=s sort=x tag=t tags=a|b legacy=l limit=3");
                data.Add(useRequestDelegateGenerator, "/items?search=s", 400, string.Empty);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task EveryParameter_BindsTheSame_WithAndWithoutTheRequestDelegateGenerator(
        bool useRequestDelegateGenerator,
        string path,
        int expectedStatus,
        string expectedBody)
    {
        await using var host = await XmlTestHost.StartAsync(
            Yaml,
            HandlerSource,
            "ErrorBodies.RuntimeTest.ItemsRegistration",
            useRequestDelegateGenerator: useRequestDelegateGenerator);

        var (status, _, body) = await host.SendRawAsync(HttpMethod.Get, path, accept: null);

        Assert.True(status == expectedStatus, $"{status}: {body}");
        if (expectedStatus == 200)
        {
            Assert.Equal(expectedBody, body);
        }
    }
}