namespace Showcase.Api.Domain.Repositories;

/// <summary>
/// Domain model for Address entity.
/// </summary>
public sealed record AddressEntity(
    string Street,
    string City,
    string? State,
    string PostalCode,
    string Country,
    string? CountryCode,
    double Latitude,
    double Longitude);