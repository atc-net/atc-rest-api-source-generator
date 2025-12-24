namespace MultipartDemo.Api.Domain.ApiHandlers.Accounts;

/// <summary>
/// Handler business logic for the CreateAccount operation.
/// </summary>
public sealed class CreateAccountHandler : ICreateAccountHandler
{
    public System.Threading.Tasks.Task<CreateAccountResult> ExecuteAsync(
        CreateAccountParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement createAccount logic
        throw new NotImplementedException("createAccount not implemented");
    }
}