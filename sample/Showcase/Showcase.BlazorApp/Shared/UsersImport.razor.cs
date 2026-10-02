namespace Showcase.BlazorApp.Shared;

/// <summary>
/// Imports a list of users as one XML document: the generated <c>CreateUserRequestXml</c> writes it, the generated
/// server binds the list by its Content-Type.
/// </summary>
public partial class UsersImport
{
    [Inject]
    private IHttpClientFactory HttpClientFactory { get; set; } = null!;

    private string xml = string.Empty;
    private string? status;
    private List<string> imported = [];
    private bool isImporting;

    protected override void OnInitialized()
    {
        // Unique emails, so every import creates new users
        var stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        List<CreateUserRequest> requests =
        [
            new CreateUserRequest(
                FirstName: "Ada",
                LastName: "Lovelace",
                Email: $"ada.{stamp}@example.com",
                DateOfBirth: new DateOnly(1815, 12, 10),
                Role: UserRole.Admin,
                Address: new AddressRequest(Street: "12 St James's Square", City: "London", PostalCode: "SW1Y 4JH", Country: "England", CountryCode: "GB")),
            new CreateUserRequest(
                FirstName: "Grace",
                LastName: "Hopper",
                Email: $"grace.{stamp}@example.com",
                DateOfBirth: new DateOnly(1906, 12, 9),
                Role: UserRole.Manager,
                Address: new AddressRequest(Street: "1 Navy Yard", City: "Arlington", PostalCode: "22201", Country: "United States", CountryCode: "US")),
        ];

        // The generated mapping writes the list; indented for reading
        xml = System.Xml.Linq.XElement.Parse(CreateUserRequestXml.ListToXml(requests, "users")).ToString();
    }

    private async Task ImportAsync()
    {
        isImporting = true;
        try
        {
            var client = HttpClientFactory.CreateClient(Constants.HttpClientName);
            using var content = new StringContent(xml, System.Text.Encoding.UTF8, "application/xml");
            using var response = await client.PostAsync(new Uri("/api/v1/users/import", UriKind.Relative), content);
            var body = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(body);
            if (response.IsSuccessStatusCode)
            {
                imported = document.RootElement
                    .EnumerateArray()
                    .Select(u => $"{u.GetProperty("firstName").GetString()} {u.GetProperty("lastName").GetString()} · {u.GetProperty("email").GetString()}")
                    .ToList();
                status = $"Imported {imported.Count} users from XML";
            }
            else
            {
                imported = [];
                status = $"{(int)response.StatusCode}: {ProblemText(document.RootElement)}";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            imported = [];
            status = $"Import failed: {ex.Message}";
        }
        finally
        {
            isImporting = false;
        }
    }

    /// <summary>
    /// A ProblemDetails as one line: each validation error's key and first message, else the title.
    /// </summary>
    private static string ProblemText(JsonElement problem)
        => problem.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object
            ? string.Join("; ", errors.EnumerateObject().Select(e => $"{e.Name}: {e.Value.EnumerateArray().FirstOrDefault().GetString()}"))
            : problem.TryGetProperty("title", out var title) ? title.GetString() ?? string.Empty : string.Empty;
}