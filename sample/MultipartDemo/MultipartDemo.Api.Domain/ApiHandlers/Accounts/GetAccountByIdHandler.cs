namespace MultipartDemo.Api.Domain.ApiHandlers.Accounts;

/// <summary>
/// Handler business logic for the GetAccountById operation.
/// </summary>
public sealed class GetAccountByIdHandler : IGetAccountByIdHandler
{
    public System.Threading.Tasks.Task<GetAccountByIdResult> ExecuteAsync(
        GetAccountByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement getAccountById logic
        throw new NotImplementedException("getAccountById not implemented");
    }
}