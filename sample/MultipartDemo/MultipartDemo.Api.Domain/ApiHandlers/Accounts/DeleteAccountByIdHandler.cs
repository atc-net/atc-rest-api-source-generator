namespace MultipartDemo.Api.Domain.ApiHandlers.Accounts;

/// <summary>
/// Handler business logic for the DeleteAccountById operation.
/// </summary>
public sealed class DeleteAccountByIdHandler : IDeleteAccountByIdHandler
{
    public System.Threading.Tasks.Task<DeleteAccountByIdResult> ExecuteAsync(
        DeleteAccountByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement deleteAccountById logic
        throw new NotImplementedException("deleteAccountById not implemented");
    }
}