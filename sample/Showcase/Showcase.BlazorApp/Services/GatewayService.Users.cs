namespace Showcase.BlazorApp.Services;

/// <summary>
/// Gateway service - Users operations using generated endpoints.
/// </summary>
public sealed partial class GatewayService
{
    /// <summary>
    /// List users with optional filters.
    /// </summary>
    public async Task<User[]?> ListUsersAsync(
        string? search = null,
        string? country = null,
        ListUsersParametersRole? role = null,
        bool? isActive = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new ListUsersParameters(
            Search: search,
            Country: country,
            Role: role,
            IsActive: isActive,
            Limit: limit);

        var result = await listUsersEndpoint.ExecuteAsync(parameters, cancellationToken: cancellationToken);

        return result.IsOk
            ? result.OkContent.ToArray()
            : null;
    }

    /// <summary>
    /// Query users by a set of ids (HTTP QUERY - the ids travel in the request body). Unknown ids are skipped.
    /// </summary>
    public async Task<(User[]? Users, string? Error)> QueryUsersAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var parameters = new QueryUsersParameters(Request: new UserQuery(Ids: [.. ids]));

        var result = await queryUsersEndpoint.ExecuteAsync(parameters, cancellationToken: cancellationToken);

        return result.IsOk
            ? (result.OkContent.ToArray(), null)
            : (null, $"{(int)result.StatusCode} {result.StatusCode}");
    }

    /// <summary>
    /// Create a new user.
    /// </summary>
    public async Task<(User? User, string? Error, IReadOnlyDictionary<string, string[]>? FieldErrors)> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new CreateUserParameters(Request: request);
            var result = await createUserEndpoint.ExecuteAsync(parameters, cancellationToken: cancellationToken);

            if (result.IsCreated)
            {
                return (result.CreatedContent, null, null);
            }

            if (result.IsBadRequest)
            {
                return BadRequest(result.BadRequestContent);
            }

            if (result.IsConflict)
            {
                return (null, "User with this email already exists", null);
            }

            return (null, $"Server error: {(int)result.StatusCode} {result.StatusCode}", null);
        }
        catch (Exception ex)
        {
            return (null, $"Request failed: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Get a user by ID.
    /// </summary>
    public async Task<User?> GetUserByIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return null;
        }

        var parameters = new GetUserByIdParameters(UserId: parsedUserId);
        var result = await getUserByIdEndpoint.ExecuteAsync(parameters, cancellationToken: cancellationToken);

        return result.IsOk
            ? result.OkContent
            : null;
    }

    /// <summary>
    /// Delete a user by ID.
    /// </summary>
    public async Task DeleteUserByIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            throw new ArgumentException("Invalid user ID format", nameof(userId));
        }

        var parameters = new DeleteUserByIdParameters(UserId: parsedUserId);
        var result = await deleteUserByIdEndpoint.ExecuteAsync(parameters, cancellationToken: cancellationToken);

        if (!result.IsNoContent)
        {
            throw new HttpRequestException($"Failed to delete user: {result.StatusCode}");
        }
    }

    /// <summary>
    /// Update a user by ID.
    /// </summary>
    public async Task<(User? User, string? Error, IReadOnlyDictionary<string, string[]>? FieldErrors)> UpdateUserByIdAsync(
        string userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return (null, "Invalid user ID format", null);
        }

        try
        {
            var parameters = new UpdateUserByIdParameters(Request: request, UserId: parsedUserId);
            var result = await updateUserByIdEndpoint.ExecuteAsync(parameters, cancellationToken: cancellationToken);

            if (result.IsOk)
            {
                return (result.OkContent, null, null);
            }

            if (result.IsNotFound)
            {
                return (null, "User not found", null);
            }

            if (result.IsBadRequest)
            {
                return BadRequest(result.BadRequestContent);
            }

            return (null, $"Server error: {(int)result.StatusCode} {result.StatusCode}", null);
        }
        catch (Exception ex)
        {
            return (null, $"Request failed: {ex.Message}", null);
        }
    }

    /// <summary>
    /// A 400: the validation errors by the wire names the request used (<c>firstName</c>, <c>address.city</c>) - the
    /// specification's (DataAnnotations) and the domain's (FluentValidation) alike - for the form to show at each field.
    /// </summary>
    private static (User? User, string? Error, IReadOnlyDictionary<string, string[]>? FieldErrors) BadRequest(
        ValidationProblemDetails? content)
        => content?.Errors is { Count: > 0 } errors
            ? (null, "The server did not accept the user - see the marked fields.", errors.AsReadOnly())
            : (null, $"Bad Request: {content?.Detail ?? content?.Title ?? "Validation error"}", null);
}