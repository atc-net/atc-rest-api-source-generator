namespace Showcase.Api.Domain.Repositories;

/// <summary>
/// Domain model for User entity.
/// </summary>
public sealed record UserEntity(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? Website,
    DateOnly DateOfBirth,
    int Age,
    string? Bio,
    string? AvatarUrl,
    bool IsActive,
    UserRoleType Role,
    AddressEntity Address,
    DateTime CreatedAt,
    DateTime UpdatedAt);