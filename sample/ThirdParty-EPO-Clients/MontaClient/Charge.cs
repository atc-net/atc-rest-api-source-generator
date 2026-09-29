// ReSharper disable CheckNamespace
namespace MontaPartner.ApiClient.Generated.Charges.Models;

public partial record Charge : IEntity
{
    // The Monta specification leaves `id` optional on Charge, so the generated property is a long?; every Charge the
    // API returns has one.
    long IEntity.Id => Id.GetValueOrDefault();
}