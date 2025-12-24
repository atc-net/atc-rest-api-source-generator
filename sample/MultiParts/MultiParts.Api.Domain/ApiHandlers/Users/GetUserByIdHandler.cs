namespace MultiParts.Api.Domain.ApiHandlers.Users;

public class GetUserByIdHandler : IGetUserByIdHandler
{
    private readonly UserInMemoryRepository repository;

    public GetUserByIdHandler(UserInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public Task<GetUserByIdResult> ExecuteAsync(
        GetUserByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var user = repository.GetById(parameters.UserId);
        if (user is null)
        {
            return Task.FromResult(GetUserByIdResult.NotFound());
        }

        return Task.FromResult(GetUserByIdResult.Ok(user));
    }
}
