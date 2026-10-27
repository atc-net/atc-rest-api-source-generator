namespace Showcase.Api.Domain.ApiHandlers.Testing;

/// <summary>
/// Handler for testing a string enum on the wire.
/// Answers the body as the server read it. The server marker sets <c>readEnumMemberNames</c>: <c>Shade</c> is read by
/// its value (<c>DARK_BLUE</c>) and by its member name (<c>DarkBlue</c>, what a client built on 2.x sent), and the answer
/// writes the value.
/// </summary>
public sealed class EchoEnumWireHandler : IEchoEnumWireHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Handlers.Testings.EchoEnumWire");

    public Task<EchoEnumWireResult> ExecuteAsync(
        EchoEnumWireParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("EchoEnumWire");

        return Task.FromResult(EchoEnumWireResult.Ok(parameters.Request));
    }
}