namespace Showcase.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the UpdateUserById operation.
/// </summary>
public sealed class UpdateUserByIdHandler : IUpdateUserByIdHandler
{
    private readonly UserInMemoryRepository repository;

    public UpdateUserByIdHandler(UserInMemoryRepository repository)
        => this.repository = repository;

    public async Task<UpdateUserByIdResult> ExecuteAsync(
        UpdateUserByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var request = parameters.Request;

        // Create address
        var address = new AddressEntity(
            Street: request.Address.Street,
            City: request.Address.City,
            State: request.Address.State,
            PostalCode: request.Address.PostalCode,
            Country: request.Address.Country,
            CountryCode: request.Address.CountryCode,
            Latitude: request.Address.Latitude ?? 0,
            Longitude: request.Address.Longitude ?? 0);

        // Map API role to domain role
        var role = Enum.TryParse<UserRoleType>(request.Role.ToString(), ignoreCase: true, out var parsedRole)
            ? parsedRole
            : UserRoleType.Guest;

        var user = await repository.Update(
            id: parameters.UserId,
            firstName: request.FirstName,
            lastName: request.LastName,
            email: request.Email,
            phone: request.Phone,
            website: request.Website?.ToString(),
            dateOfBirth: DateOnly.FromDateTime(request.DateOfBirth.DateTime),
            bio: request.Bio,
            avatarUrl: request.AvatarUrl?.ToString(),
            isActive: request.IsActive,
            role: role,
            address: address);

        if (user is null)
        {
            return UpdateUserByIdResult.NotFound();
        }

        return UpdateUserByIdResult.Ok(MapToApiModel(user));
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