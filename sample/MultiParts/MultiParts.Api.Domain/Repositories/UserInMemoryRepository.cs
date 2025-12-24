namespace MultiParts.Api.Domain.Repositories;

public class UserInMemoryRepository
{
    private readonly List<User> users = [];

    public UserInMemoryRepository()
    {
        // Seed with some initial data
        users.Add(new User(Guid.NewGuid(), "Alice Smith", "alice@example.com", DateTimeOffset.UtcNow));
        users.Add(new User(Guid.NewGuid(), "Bob Johnson", "bob@example.com", DateTimeOffset.UtcNow));
    }

    public User[] GetAll() => users.ToArray();

    public User? GetById(Guid id) => users.FirstOrDefault(u => u.Id == id);

    public User Add(string name, string email)
    {
        var newUser = new User(Guid.NewGuid(), name, email, DateTimeOffset.UtcNow);
        users.Add(newUser);
        return newUser;
    }
}
