namespace Showcase.Api.Domain.ApiHandlers.Files;

/// <summary>
/// Handler business logic for the GetFileById operation.
/// Returns a file from the in-memory repository by its ID.
/// </summary>
public sealed class GetFileByIdHandler : IGetFileByIdHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Handlers.Files.GetFileById");
    private readonly FileInMemoryRepository repository;

    public GetFileByIdHandler(FileInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public async Task<GetFileByIdResult> ExecuteAsync(
        GetFileByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("GetFileById");
        var file = await repository
            .GetByIdAsync(parameters.Id, cancellationToken)
            .ConfigureAwait(false);

        if (file is null)
        {
            return GetFileByIdResult.NotFound();
        }

        return GetFileByIdResult.Ok(
            file.Content.ToArray(),
            file.ContentType,
            file.FileName);
    }
}