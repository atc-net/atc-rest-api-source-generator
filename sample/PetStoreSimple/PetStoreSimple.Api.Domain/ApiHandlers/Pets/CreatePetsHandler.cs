namespace PetStoreSimple.Api.Domain.ApiHandlers.Pets;

/// <summary>
/// Handler business logic for the CreatePets operation.
/// </summary>
public sealed class CreatePetsHandler : ICreatePetsHandler
{
    private readonly PetInMemoryRepository repository;

    public CreatePetsHandler(PetInMemoryRepository repository)
        => this.repository = repository;

    public Task<CreatePetsResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // The operation takes no body in the specification, so a pet with a generated name is created
        var pet = repository.Add($"Pet {DateTimeOffset.UtcNow:HHmmss}");

        return Task.FromResult(CreatePetsResult.Created($"/v1/pets/{pet.Id}"));
    }
}