// ReSharper disable CheckNamespace
namespace MontaPartner.ApiClient.Generated.Sites.Models;

public partial record Site : IEntity
{
    // The Monta specification leaves `id` optional on Site, so the generated property is a long?; every Site the
    // API returns has one.
    long IEntity.Id => Id.GetValueOrDefault();
}