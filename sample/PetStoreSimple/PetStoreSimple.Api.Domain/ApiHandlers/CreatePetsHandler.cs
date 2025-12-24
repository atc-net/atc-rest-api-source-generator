namespace PetStoreSimple.Api.Domain.ApiHandlers;

public class CreatePetsHandler : ICreatePetsHandler
{
    private readonly PetInMemoryRepository repository;

    public CreatePetsHandler(PetInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public Task<CreatePetsResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // According to the OpenAPI spec, POST /pets returns 201 with no response body
        // In a real implementation, this would accept a request body with pet data
        // For now, create a sample pet
        var newPet = repository.Add("NewPet", "sample");
        return Task.FromResult(CreatePetsResult.Created($"/pets/{newPet.Id}"));
    }
}