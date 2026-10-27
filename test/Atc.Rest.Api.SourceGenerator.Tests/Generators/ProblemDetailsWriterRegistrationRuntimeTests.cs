namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The generated <c>Add{Project}Api()</c> of an API with XML bodies registers its <c>XmlProblemDetailsWriter</c> once,
/// guarded by a look at the service collection. That look matches only an unkeyed <c>IProblemDetailsWriter</c>
/// registration of the writer, and never reads <c>ServiceDescriptor.ImplementationType</c> of a keyed registration -
/// Microsoft.Extensions.DependencyInjection.Abstractions 8.0.0 and 8.0.1 throw there.
/// </summary>
public sealed class ProblemDetailsWriterRegistrationRuntimeTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: XmlThings
          version: 1.0.0
        paths:
          /things:
            get:
              operationId: listThings
              responses:
                '200':
                  description: OK
                  content:
                    application/xml: { schema: { $ref: '#/components/schemas/Thing' } }
        components:
          schemas:
            Thing:
              type: object
              properties:
                name: { type: string }
        """;

    private const string WriterTypeName = "XmlThings.Generated.XmlProblemDetailsWriter";

    private static readonly Lazy<Assembly> Server = new(() =>
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(
            new ApiServerGenerator(),
            ".atc-rest-api-server",
            ErrorResponseBodyRuntimeTests.ServerMarker,
            includeMinimalApi: true,
            Yaml);
        return CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: true);
    });

    [Fact]
    public void AddApi_AfterAKeyedRegistration_DoesNotThrow_AndRegistersTheWriterOnce()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IComparer<string>>("ordinal", StringComparer.Ordinal);
        services.AddKeyedSingleton<IThingMarker, ThingMarker>("a");

        AddApi(services);

        Assert.Equal(1, WriterRegistrations(services));
    }

    [Fact]
    public void AddApi_CalledTwice_AfterAKeyedRegistration_RegistersTheWriterOnce()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IThingMarker, ThingMarker>("a");

        AddApi(services);
        AddApi(services);

        Assert.Equal(1, WriterRegistrations(services));
    }

    [Fact]
    public void AddApi_WhenTheHostRegisteredTheWriterItself_DoesNotRegisterItAgain()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IThingMarker, ThingMarker>("a");
        services.AddSingleton(typeof(Microsoft.AspNetCore.Http.IProblemDetailsWriter), WriterType());

        AddApi(services);

        Assert.Equal(1, WriterRegistrations(services));
    }

    [Fact]
    public void AddApi_WithTheWriterOnlyKeyed_StillRegistersTheUnkeyedWriter()
    {
        // A keyed IProblemDetailsWriter is never among the writers ProblemDetails resolves
        var services = new ServiceCollection();
        services.AddKeyedSingleton(typeof(Microsoft.AspNetCore.Http.IProblemDetailsWriter), "a", WriterType());

        AddApi(services);

        Assert.Equal(1, WriterRegistrations(services));
    }

    [Fact]
    public void AddApi_WhenTheHostRegisteredTheWriterAsItsOwnServiceType_StillRegistersItAsAProblemDetailsWriter()
    {
        // The writer registered as itself is no IProblemDetailsWriter ProblemDetails resolves
        var services = new ServiceCollection();
        services.AddSingleton(WriterType());

        AddApi(services);

        Assert.Equal(1, WriterRegistrations(services));
    }

    private static Type WriterType()
        => Server.Value.GetType(WriterTypeName, throwOnError: true)!;

    private static void AddApi(IServiceCollection services)
        => Server.Value
            .GetType("XmlThings.Generated.Extensions.UnifiedServiceCollectionExtensions", throwOnError: true)!
            .GetMethod("AddXmlThingsApi")!
            .Invoke(null, [services, null]);

    /// <summary>The unkeyed <c>IProblemDetailsWriter</c> registrations of the generated XML writer.</summary>
    private static int WriterRegistrations(IServiceCollection services)
    {
        var writerType = WriterType();
        return services.Count(d =>
            !d.IsKeyedService &&
            d.ServiceType == typeof(Microsoft.AspNetCore.Http.IProblemDetailsWriter) &&
            d.ImplementationType == writerType);
    }

    private interface IThingMarker;

    private sealed class ThingMarker : IThingMarker;
}