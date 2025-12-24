namespace MultipartDemo.Api.Domain.ApiHandlers.Accounts;

/// <summary>
/// Handler business logic for the UpdateAccountById operation.
/// </summary>
public sealed class UpdateAccountByIdHandler : IUpdateAccountByIdHandler
{
    public System.Threading.Tasks.Task<UpdateAccountByIdResult> ExecuteAsync(
        UpdateAccountByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement updateAccountById logic
        throw new NotImplementedException("updateAccountById not implemented");
    }
}