namespace Showcase.BlazorApp.Shared;

/// <summary>
/// Looks users up by a set of ids with the OpenAPI 3.2 QUERY method (<c>queryUsers</c>): the ids travel in the request
/// body, so a set too large for a URL is still one request. Unknown ids are skipped.
/// </summary>
public partial class UsersQuery
{
    private static readonly char[] Separators = [' ', ',', '\r', '\n', '\t'];

    [Inject]
    private GatewayService Gateway { get; set; } = null!;

    private string idsText = string.Empty;
    private string? status;
    private List<string> found = [];
    private bool isQuerying;

    private async Task QueryAsync()
    {
        var texts = idsText.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        var ids = new List<Guid>();
        foreach (var text in texts)
        {
            if (!Guid.TryParse(text, out var id))
            {
                status = $"Not an id: {text}";
                found = [];
                return;
            }

            ids.Add(id);
        }

        if (ids.Count == 0)
        {
            status = null;
            found = [];
            return;
        }

        isQuerying = true;
        try
        {
            var (users, error) = await Gateway.QueryUsersAsync(ids);
            found = users?.Select(u => $"{u.FirstName} {u.LastName} · {u.Email}").ToList() ?? [];
            status = error is null
                ? $"Found {found.Count} of {ids.Count} users"
                : $"Query failed: {error}";
        }
        finally
        {
            isQuerying = false;
        }
    }
}