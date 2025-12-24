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
        string? role = null,
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
    /// Create a new user.
    /// </summary>
    public async Task<(User? User, string? Error)> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new CreateUserParameters(Request: request);
            var result = await createUserEndpoint.ExecuteAsync(parameters, cancellationToken: cancellationToken);

            if (result.IsCreated)
            {
                return (result.CreatedContent, null);
            }

            if (result.IsBadRequest)
            {
                try
                {
                    var content = result.BadRequestContent;
                    if (content?.Errors is { Count: > 0 })
                    {
                        var errorMessages = content
                            .Errors
                            .SelectMany(e => e.Value.Select(v => $"{e.Key}: {v}"));
                        return (null, $"Validation errors: {string.Join("; ", errorMessages)}");
                    }

                    return (null, $"Bad Request: {content?.Detail ?? content?.Title ?? "Validation error"}");
                }
                catch
                {
                    return (null, "Bad Request: Validation error");
                }
            }

            if (result.IsConflict)
            {
                return (null, "User with this email already exists");
            }

            return (null, $"Server error: {(int)result.StatusCode} {result.StatusCode}");
        }
        catch (Exception ex)
        {
            return (null, $"Request failed: {ex.Message}");
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
    public async Task<(User? User, string? Error)> UpdateUserByIdAsync(
        string userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return (null, "Invalid user ID format");
        }

        try
        {
            var parameters = new UpdateUserByIdParameters(Request: request, UserId: parsedUserId);
            var result = await updateUserByIdEndpoint.ExecuteAsync(parameters, cancellationToken: cancellationToken);

            if (result.IsOk)
            {
                return (result.OkContent, null);
            }

            if (result.IsNotFound)
            {
                return (null, "User not found");
            }

            if (result.IsBadRequest)
            {
                try
                {
                    var content = result.BadRequestContent;
                    if (content?.Errors is { Count: > 0 })
                    {
                        var errorMessages = content
                            .Errors
                            .SelectMany(e => e.Value.Select(v => $"{e.Key}: {v}"));
                        return (null, $"Validation errors: {string.Join("; ", errorMessages)}");
                    }

                    return (null, $"Bad Request: {content?.Detail ?? content?.Title ?? "Validation error"}");
                }
                catch
                {
                    return (null, "Bad Request: Validation error");
                }
            }

            return (null, $"Server error: {(int)result.StatusCode} {result.StatusCode}");
        }
        catch (Exception ex)
        {
            return (null, $"Request failed: {ex.Message}");
        }
    }
}