#pragma warning disable CA5394, SCS0005 // Random is used only for sample data generation

namespace MultipartDemo.Api.Domain.Repositories;

public sealed class UserInMemoryRepository
{
    private static readonly string[] FirstNames = ["John", "Jane", "Bob", "Alice", "Charlie", "Diana", "Eve", "Frank"];
    private static readonly string[] LastNames = ["Smith", "Johnson", "Williams", "Brown", "Jones", "Davis", "Miller", "Wilson"];
    private static readonly string[] Countries = ["USA", "UK", "Canada", "Germany", "France", "Japan", "Australia"];

    private readonly ConcurrentDictionary<Guid, UserEntity> users = new();

    public UserInMemoryRepository()
    {
        var random = new Random(42);

        for (var i = 0; i < 25; i++)
        {
            var firstName = FirstNames[random.Next(FirstNames.Length)];
            var lastName = LastNames[random.Next(LastNames.Length)];
            var country = Countries[random.Next(Countries.Length)];
            var role = (UserRole)random.Next(4);

            var user = new UserEntity
            {
                Id = Guid.NewGuid(),
                FirstName = firstName,
                LastName = lastName,
                Email = $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}@example.com",
                Phone = $"+1-555-{random.Next(100, 999):D3}-{random.Next(1000, 9999):D4}",
                Role = role,
                IsActive = random.Next(10) > 2,
                Address = new AddressEntity
                {
                    Street = $"{random.Next(100, 9999)} Main St",
                    City = "Sample City",
                    State = "State",
                    PostalCode = $"{random.Next(10000, 99999)}",
                    Country = country,
                },
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-random.Next(1, 365)),
            };
            users[user.Id] = user;
        }
    }

    public Task<IReadOnlyList<UserEntity>> GetAllAsync(
        string? search = null,
        string? country = null,
        UserRole? role = null,
        bool? isActive = null,
        int pageSize = 10,
        int pageIndex = 0)
    {
        var query = users.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                u.FirstName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                u.LastName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                u.Email.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(country))
        {
            query = query.Where(u => u.Address?.Country?.Equals(country, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        if (role.HasValue)
        {
            query = query.Where(u => u.Role == role.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        var result = query
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult<IReadOnlyList<UserEntity>>(result);
    }

    public Task<UserEntity?> GetByIdAsync(Guid id)
    {
        users.TryGetValue(id, out var user);
        return Task.FromResult(user);
    }

    public Task<UserEntity> CreateAsync(UserEntity user)
    {
        user.Id = Guid.NewGuid();
        user.CreatedAt = DateTimeOffset.UtcNow;
        users[user.Id] = user;
        return Task.FromResult(user);
    }

    public Task<UserEntity?> UpdateAsync(
        Guid id,
        Action<UserEntity> updateAction)
    {
        if (!users.TryGetValue(id, out var user))
        {
            return Task.FromResult<UserEntity?>(null);
        }

        updateAction(user);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.FromResult<UserEntity?>(user);
    }

    public Task<bool> DeleteAsync(Guid id)
        => Task.FromResult(users.TryRemove(id, out _));
}