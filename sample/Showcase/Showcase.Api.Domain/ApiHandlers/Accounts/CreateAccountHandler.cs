namespace Showcase.Api.Domain.ApiHandlers.Accounts;

/// <summary>
/// Handler business logic for the CreateAccount operation.
/// </summary>
public sealed class CreateAccountHandler : ICreateAccountHandler
{
    private readonly AccountInMemoryRepository repository;

    public CreateAccountHandler(AccountInMemoryRepository repository)
        => this.repository = repository;

    public async Task<CreateAccountResult> ExecuteAsync(
        CreateAccountParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var account = await repository.Create(
            parameters.Request.Id,
            parameters.Request.Name,
            parameters.Request.Tag);

        return CreateAccountResult.Created(account);
    }
}