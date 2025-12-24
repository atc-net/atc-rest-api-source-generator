namespace MultipartDemo.Api.Domain.Repositories;

public sealed class UserEntity
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public UserRole Role { get; set; } = UserRole.Employee;

    public bool IsActive { get; set; } = true;

    public string? ProfileImageUrl { get; set; }

    public AddressEntity? Address { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}