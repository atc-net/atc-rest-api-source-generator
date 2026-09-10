namespace Showcase.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the QueryUsers operation.
/// </summary>
/// <remarks>
/// This is the OpenAPI 3.2 <c>QUERY</c> method: a read, safe and idempotent like <c>GET</c>, whose
/// criteria travel in the request body because they do not fit in a URL. Asking for 1000 users by
/// id as a <c>GET</c> would serialize to roughly 40 KB of query string, and Kestrel rejects a
/// request line over 8 KB with <c>414 URI Too Long</c> before any handler runs.
/// <para>
/// Nothing about the handler shape is special — the generator emits the same
/// <c>IQueryUsersHandler</c> / <c>QueryUsersParameters</c> / <c>QueryUsersResult</c> trio it emits
/// for any other verb, and the request body arrives as <c>parameters.Request</c>.
/// </para>
/// </remarks>
public sealed class QueryUsersHandler : IQueryUsersHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Handlers.Users.QueryUsers");
    private readonly UserInMemoryRepository repository;

    public QueryUsersHandler(UserInMemoryRepository repository)
        => this.repository = repository;

    public async Task<QueryUsersResult> ExecuteAsync(
        QueryUsersParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("QueryUsers");
        ArgumentNullException.ThrowIfNull(parameters);

        activity?.SetTag("query.id_count", parameters.Request.Ids.Count);

        var users = await repository.GetByIds(parameters.Request.Ids);

        // Unknown ids are skipped rather than erroring, so the result can be shorter than the
        // requested set. That is the useful behaviour for a bulk lookup.
        List<User> result = [.. users.Select(MapToApiModel)];

        return QueryUsersResult.Ok(result);
    }

    private static User MapToApiModel(UserEntity user)
        => new(
            Id: user.Id,
            FirstName: user.FirstName,
            LastName: user.LastName,
            Email: user.Email,
            Phone: user.Phone,
            Website: string.IsNullOrEmpty(user.Website) ? null : new Uri(user.Website),
            DateOfBirth: new DateTimeOffset(user.DateOfBirth.ToDateTime(TimeOnly.MinValue)),
            Age: user.Age,
            Bio: user.Bio,
            AvatarUrl: string.IsNullOrEmpty(user.AvatarUrl) ? null : new Uri(user.AvatarUrl),
            Role: Enum.TryParse<UserRole>(user.Role.ToString(), out var apiRole) ? apiRole : UserRole.Guest,
            Address: new Address(
                Street: user.Address.Street,
                City: user.Address.City,
                State: user.Address.State,
                PostalCode: user.Address.PostalCode,
                Country: user.Address.Country,
                CountryCode: user.Address.CountryCode,
                Latitude: user.Address.Latitude,
                Longitude: user.Address.Longitude),
            CreatedAt: new DateTimeOffset(user.CreatedAt),
            UpdatedAt: new DateTimeOffset(user.UpdatedAt),
            IsActive: user.IsActive);
}