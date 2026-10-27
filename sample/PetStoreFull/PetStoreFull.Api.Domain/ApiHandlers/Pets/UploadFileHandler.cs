namespace PetStoreFull.Api.Domain.ApiHandlers.Pets;

/// <summary>
/// Handler business logic for the UploadFile operation.
/// </summary>
public sealed class UploadFileHandler : IUploadFileHandler
{
    private readonly PetStoreInMemoryRepository repository;

    public UploadFileHandler(PetStoreInMemoryRepository repository)
        => this.repository = repository;

    public async Task<UploadFileResult> ExecuteAsync(
        UploadFileParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (repository.GetPet(parameters.PetId) is null)
        {
            return UploadFileResult.NotFound($"Pet {parameters.PetId} not found");
        }

        // The image is read to measure it; nothing is kept
        using var buffer = new MemoryStream();
        await parameters.File.CopyToAsync(buffer, cancellationToken);

        return UploadFileResult.Ok(new ApiResponse(
            Code: StatusCodes.Status200OK,
            Type: "unknown",
            Message: $"additionalMetadata: {parameters.AdditionalMetadata ?? "null"}\nFile uploaded to ./{parameters.PetId}, {buffer.Length} bytes"));
    }
}