namespace Showcase.BlazorApp.Shared;

/// <summary>
/// Imports a list of users as one XML document: the generated <c>CreateUserRequestXml</c> writes and reads it, the
/// generated endpoint sends it as XML (this client prefers XML where an operation offers it), and the generated server
/// binds the list by its Content-Type.
/// </summary>
public partial class UsersImport
{
    [Inject]
    private IImportUsersEndpoint ImportUsers { get; set; } = null!;

    private string xml = string.Empty;
    private string? status;
    private List<string> imported = [];
    private bool isImporting;

    protected override void OnInitialized()
    {
        // Unique emails and surnames, so every import creates new users (the server refuses a second user with the same
        // name, an item of a list as well as a single user)
        var stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var suffix = Guid.NewGuid().ToString("N")[..5];
        List<CreateUserRequest> requests =
        [
            new CreateUserRequest(
                FirstName: "Ada",
                LastName: $"Lovelace-{suffix}",
                Email: $"ada.{stamp}@example.com",
                DateOfBirth: new DateOnly(1815, 12, 10),
                Role: UserRole.Admin,
                Address: new AddressRequest(Street: "12 St James's Square", City: "London", PostalCode: "SW1Y 4JH", Country: "England", CountryCode: "GB")),
            new CreateUserRequest(
                FirstName: "Grace",
                LastName: $"Hopper-{suffix}",
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
            // The edited document, read by the generated mapping and sent by the generated endpoint
            var requests = CreateUserRequestXml.ListFromXml(xml);
            var result = await ImportUsers.ExecuteAsync(new ImportUsersParameters(requests));
            if (result.IsOk)
            {
                imported = result.OkContent
                    .Select(u => $"{u.FirstName} {u.LastName} · {u.Email}")
                    .ToList();
                status = $"Imported {imported.Count} users from XML";
            }
            else
            {
                imported = [];
                status = $"{(int)result.StatusCode}: {ProblemText(result.ContentObject)}";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Xml.XmlException or FormatException or InvalidOperationException)
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
    private static string ProblemText(object? problem)
        => problem switch
        {
            ValidationProblemDetails { Errors.Count: > 0 } validation
                => string.Join("; ", validation.Errors.Select(e => $"{e.Key}: {e.Value.FirstOrDefault()}")),
            ProblemDetails details => details.Title ?? details.Detail ?? string.Empty,
            _ => string.Empty,
        };
}