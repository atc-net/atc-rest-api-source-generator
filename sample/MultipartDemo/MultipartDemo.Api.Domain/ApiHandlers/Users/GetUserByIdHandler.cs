namespace MultipartDemo.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the GetUserById operation.
/// </summary>
public sealed class GetUserByIdHandler : IGetUserByIdHandler
{
    public System.Threading.Tasks.Task<GetUserByIdResult> ExecuteAsync(
        GetUserByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement getUserById logic
        throw new NotImplementedException("getUserById not implemented");
    }
}