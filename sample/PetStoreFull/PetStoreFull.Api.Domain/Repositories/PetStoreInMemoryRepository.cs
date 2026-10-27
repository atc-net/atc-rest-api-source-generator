namespace PetStoreFull.Api.Domain.Repositories;

/// <summary>
/// One in-memory store for the three aggregates of the pet store (pets, orders, users),
/// seeded so the GET operations answer with data from the first request.
/// </summary>
public sealed class PetStoreInMemoryRepository
{
    private readonly ConcurrentDictionary<long, Pet> pets = new();
    private readonly ConcurrentDictionary<long, Order> orders = new();
    private readonly ConcurrentDictionary<string, User> users = new(StringComparer.Ordinal);
    private long nextPetId;
    private long nextOrderId;
    private long nextUserId;

    public PetStoreInMemoryRepository()
    {
        var dogs = new Category(1, "Dogs");
        var cats = new Category(2, "Cats");
        var fish = new Category(3, "Fish");

        AddPet(new Pet("Rex", ["https://contoso.example/photos/rex.jpg"], Category: dogs, Tags: [new Tag(1, "friendly"), new Tag(2, "trained")], Status: PetStatus.Available));
        AddPet(new Pet("Whiskers", ["https://contoso.example/photos/whiskers.jpg"], Category: cats, Tags: [new Tag(1, "friendly")], Status: PetStatus.Available));
        AddPet(new Pet("Goldie", ["https://contoso.example/photos/goldie.jpg"], Category: fish, Tags: [new Tag(3, "quiet")], Status: PetStatus.Pending));
        AddPet(new Pet("Buddy", ["https://contoso.example/photos/buddy.jpg"], Category: dogs, Tags: [new Tag(2, "trained")], Status: PetStatus.Sold));

        AddOrder(new Order(PetId: 1, Quantity: 1, ShipDate: new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero), Status: OrderStatus.Placed, Complete: false));
        AddOrder(new Order(PetId: 4, Quantity: 1, ShipDate: new DateTimeOffset(2026, 9, 20, 14, 30, 0, TimeSpan.Zero), Status: OrderStatus.Delivered, Complete: true));

        AddUser(new User(Username: "alice", FirstName: "Alice", LastName: "Andersen", Email: "alice@contoso.example", Password: "alice-secret", Phone: "555-0100", UserStatus: 1));
        AddUser(new User(Username: "bob", FirstName: "Bob", LastName: "Berg", Email: "bob@contoso.example", Password: "bob-secret", Phone: "555-0101", UserStatus: 1));
    }

    public List<Pet> FindPetsByStatus(string status)
        => pets.Values
            .Where(pet => string.Equals(pet.Status.ToWireValue(), status, StringComparison.Ordinal))
            .OrderBy(pet => pet.Id)
            .ToList();

    public List<Pet> FindPetsByTags(IEnumerable<string> tags)
    {
        var wanted = tags.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return pets.Values
            .Where(pet => pet.Tags is not null && pet.Tags.Any(tag => tag.Name is not null && wanted.Contains(tag.Name)))
            .OrderBy(pet => pet.Id)
            .ToList();
    }

    public Pet? GetPet(long id)
        => pets.GetValueOrDefault(id);

    public Pet AddPet(Pet pet)
    {
        var stored = pet with { Id = pet.Id ?? Interlocked.Increment(ref nextPetId) };
        pets[stored.Id!.Value] = stored;
        return stored;
    }

    public Pet? UpdatePet(Pet pet)
    {
        if (pet.Id is not { } id || !pets.ContainsKey(id))
        {
            return null;
        }

        pets[id] = pet;
        return pet;
    }

    public bool RemovePet(long id)
        => pets.TryRemove(id, out _);

    public Dictionary<string, int> GetInventory()
        => pets.Values
            .GroupBy(pet => pet.Status.ToWireValue() ?? "unknown", StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    public Order? GetOrder(long id)
        => orders.GetValueOrDefault(id);

    public Order AddOrder(Order order)
    {
        var stored = order with { Id = order.Id ?? Interlocked.Increment(ref nextOrderId) };
        orders[stored.Id!.Value] = stored;
        return stored;
    }

    public bool RemoveOrder(long id)
        => orders.TryRemove(id, out _);

    public User? GetUser(string username)
        => users.GetValueOrDefault(username);

    public User AddUser(User user)
    {
        var stored = user with { Id = user.Id ?? Interlocked.Increment(ref nextUserId) };
        users[stored.Username!] = stored;
        return stored;
    }

    public User? UpdateUser(
        string username,
        User user)
    {
        if (!users.TryGetValue(username, out var existing))
        {
            return null;
        }

        var stored = user with { Id = existing.Id, Username = username };
        users[username] = stored;
        return stored;
    }

    public bool RemoveUser(string username)
        => users.TryRemove(username, out _);
}