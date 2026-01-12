namespace PetStoreSimple.Api.Domain.Repositories;

public class PetInMemoryRepository
{
    private readonly List<Pet> pets = [];
    private long nextId = 1;

    public PetInMemoryRepository()
    {
        // Seed with some initial data
        pets.Add(new Pet(nextId++, "Fluffy", "cat"));
        pets.Add(new Pet(nextId++, "Buddy", "dog"));
        pets.Add(new Pet(nextId++, "Max", "dog"));
    }

    public List<Pet> GetAll(int? limit = null)
    {
        var query = pets.AsEnumerable();

        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        return query.ToList();
    }

    public Pet? GetById(string id)
    {
        if (!long.TryParse(id, out var petId))
        {
            return null;
        }

        return pets.FirstOrDefault(p => p.Id == petId);
    }

    public Pet Add(
        string name,
        string? tag = null)
    {
        var newPet = new Pet(nextId++, name, tag ?? string.Empty);
        pets.Add(newPet);
        return newPet;
    }
}