namespace MontaPartner.ApiClient;

/// <summary>
/// Common interface for entities with Id and UpdatedAt properties.
/// </summary>
public interface IEntity
{
    long Id { get; }

    DateTimeOffset UpdatedAt { get; }
}