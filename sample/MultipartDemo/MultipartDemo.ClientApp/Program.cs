#pragma warning disable CA1031, CA1303, SA1518, CA2000

// Redirect Console output to also write to VS Debug Output window
Console.SetOut(new DualWriter(Console.Out));

Console.WriteLine("MultipartDemo Client - CRUD Operations");
Console.WriteLine("========================================");
Console.WriteLine();

// Get API endpoint from environment variable or use default
var apiBaseUrl = Environment.GetEnvironmentVariable("services__api__http__0")
    ?? Environment.GetEnvironmentVariable("services__api__https__0")
    ?? "http://localhost:5050";

Console.WriteLine($"Connecting to API at: {apiBaseUrl}");
Console.WriteLine();

// Demo JWT token (validation is disabled in API's Program.cs for demo purposes)
#pragma warning disable S6418 // Secrets should not be hard-coded
const string demoToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJkZW1vLXVzZXIiLCJuYW1lIjoiRGVtbyBVc2VyIiwiaWF0IjoxNzE2MjM5MDIyfQ.demo-signature";
#pragma warning restore S6418 // Secrets should not be hard-coded

// Create HTTP client with base address and demo auth token
using var httpClient = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", demoToken);

var client = new AccountsClient(httpClient);

try
{
    // ========================================
    // CRUD OPERATIONS DEMONSTRATION
    // ========================================

    // 1. CREATE - Create a new account
    Console.WriteLine("1. CREATE - Creating a new account...");
    var newAccount = new Account(
        Id: 999,
        Name: "Demo Account",
        Tag: "demo-crud-test");

    var createdAccount = await client
        .CreateAccountAsync(
            new CreateAccountParameters(Request: newAccount),
            CancellationToken.None)
        .ConfigureAwait(false);

    if (createdAccount != null)
    {
        Console.WriteLine($"   Created account successfully:");
        Console.WriteLine($"   - ID: {createdAccount.Id}");
        Console.WriteLine($"   - Name: {createdAccount.Name}");
        Console.WriteLine($"   - Tag: {createdAccount.Tag}");
    }
    else
    {
        Console.WriteLine("   Failed to create account (null response)");
    }

    Console.WriteLine();

    // 2. READ (Single) - Get the account by ID
    Console.WriteLine("2. READ - Getting account by ID (999)...");
    var retrievedAccount = await client
        .GetAccountByIdAsync(
            new GetAccountByIdParameters(AccountId: "999"),
            CancellationToken.None)
        .ConfigureAwait(false);

    if (retrievedAccount != null)
    {
        Console.WriteLine($"   Retrieved account:");
        Console.WriteLine($"   - ID: {retrievedAccount.Id}");
        Console.WriteLine($"   - Name: {retrievedAccount.Name}");
        Console.WriteLine($"   - Tag: {retrievedAccount.Tag}");
    }
    else
    {
        Console.WriteLine("   Account not found");
    }

    Console.WriteLine();

    // 3. UPDATE - Update the account
    Console.WriteLine("3. UPDATE - Updating account (999)...");
    var updatedAccountData = new Account(
        Id: 999,
        Name: "Demo Account (Updated)",
        Tag: "demo-crud-updated");

    var updatedAccount = await client
        .UpdateAccountByIdAsync(
            new UpdateAccountByIdParameters(Request: updatedAccountData, AccountId: "999"),
            CancellationToken.None)
        .ConfigureAwait(false);

    if (updatedAccount != null)
    {
        Console.WriteLine($"   Updated account successfully:");
        Console.WriteLine($"   - ID: {updatedAccount.Id}");
        Console.WriteLine($"   - Name: {updatedAccount.Name}");
        Console.WriteLine($"   - Tag: {updatedAccount.Tag}");
    }
    else
    {
        Console.WriteLine("   Failed to update account");
    }

    Console.WriteLine();

    // 4. READ (List) - List all accounts
    Console.WriteLine("4. READ (List) - Listing all accounts (limit: 10)...");
    var accounts = await client
        .ListAccountsAsync(
            new ListAccountsParameters(Limit: 10),
            CancellationToken.None)
        .ConfigureAwait(false);

    if (accounts != null)
    {
        Console.WriteLine($"   Found {accounts.Count} accounts:");
        foreach (var account in accounts.Take(5))
        {
            Console.WriteLine($"   - {account.Name} (ID: {account.Id}, Tag: {account.Tag})");
        }

        if (accounts.Count > 5)
        {
            Console.WriteLine($"   ... and {accounts.Count - 5} more");
        }
    }

    Console.WriteLine();

    // 5. DELETE - Delete the account
    Console.WriteLine("5. DELETE - Deleting account (999)...");
    await client
        .DeleteAccountByIdAsync(
            new DeleteAccountByIdParameters(AccountId: "999"),
            CancellationToken.None)
        .ConfigureAwait(false);

    Console.WriteLine("   Account deleted successfully (204 No Content)");

    Console.WriteLine();

    // 6. VERIFY DELETE - Try to get deleted account
    Console.WriteLine("6. VERIFY DELETE - Attempting to get deleted account (999)...");
    try
    {
        var shouldBeNull = await client
            .GetAccountByIdAsync(
                new GetAccountByIdParameters(AccountId: "999"),
                CancellationToken.None)
            .ConfigureAwait(false);

        if (shouldBeNull != null)
        {
            Console.WriteLine("   Warning: Account still exists after delete!");
        }
        else
        {
            Console.WriteLine("   Account not found (expected - delete confirmed)");
        }
    }
    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        Console.WriteLine("   Account not found (404 - delete confirmed)");
    }

    Console.WriteLine();
    Console.WriteLine("========================================");
    Console.WriteLine("All operations completed successfully!");
    Console.WriteLine("========================================");
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"HTTP Error: {ex.Message}");
    Console.WriteLine($"  Status Code: {ex.StatusCode}");
    Console.WriteLine("  Make sure the API is running on " + apiBaseUrl);
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"  Stack: {ex.StackTrace}");
}
