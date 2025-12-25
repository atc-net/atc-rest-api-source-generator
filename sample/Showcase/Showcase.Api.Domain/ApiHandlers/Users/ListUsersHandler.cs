namespace Showcase.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the ListUsers operation.
/// </summary>
public sealed class ListUsersHandler : IListUsersHandler
{
    private readonly UserInMemoryRepository repository;

    public ListUsersHandler(UserInMemoryRepository repository)
        => this.repository = repository;

    public async Task<ListUsersResult> ExecuteAsync(
        ListUsersParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        // Parse role if provided
        UserRoleType? role = null;
        if (parameters.Role is not null)
        {
            var roleString = parameters.Role.ToString();
            if (Enum.TryParse<UserRoleType>(roleString, ignoreCase: true, out var parsedRole))
            {
                role = parsedRole;
            }
        }

        var users = await repository.GetAll(
            search: parameters.Search,
            country: parameters.Country,
            role: role,
            isActive: parameters.IsActive,
            limit: parameters.Limit);

        // Map domain users to API models
        var result = users
            .Select(MapToApiModel)
            .ToArray();

        return ListUsersResult.Ok(result);
    }

    private static User MapToApiModel(UserEntity user)
        => new(
            Id: user.Id,
            FirstName: user.FirstName,
            LastName: user.LastName,
            Email: user.Email,
            Phone: user.Phone,
            Website: string.IsNullOrEmpty(user.Website) ? null : new Uri(user.Website),
            DateOfBirth: new DateTimeOffset(user.DateOfBirth.ToDateTime(TimeOnly.MinValue)),
            Age: user.Age,
            Bio: user.Bio,
            AvatarUrl: string.IsNullOrEmpty(user.AvatarUrl) ? null : new Uri(user.AvatarUrl),
            Role: Enum.TryParse<UserRole>(user.Role.ToString(), out var apiRole) ? apiRole : UserRole.Guest,
            Address: new Address(
                Street: user.Address.Street,
                City: user.Address.City,
                State: user.Address.State,
                PostalCode: user.Address.PostalCode,
                Country: user.Address.Country,
                CountryCode: user.Address.CountryCode,
                Latitude: user.Address.Latitude,
                Longitude: user.Address.Longitude),
            CreatedAt: new DateTimeOffset(user.CreatedAt),
            UpdatedAt: new DateTimeOffset(user.UpdatedAt),
            IsActive: user.IsActive);
}