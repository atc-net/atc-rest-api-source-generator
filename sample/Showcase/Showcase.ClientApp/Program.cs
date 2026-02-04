#pragma warning disable CA1031, CA1303, SA1518, CA2000

// Redirect Console output to also write to VS Debug Output window
Console.SetOut(new DualWriter(Console.Out));

Console.WriteLine("Showcase Client Demo - CRUD Operations");
Console.WriteLine("=======================================");
Console.WriteLine();

// Get API endpoint from Aspire environment variables or use default
// Aspire sets services__api__http__0 when using .WithReference(api.GetEndpoint("http"))
var apiBaseUrl = Environment.GetEnvironmentVariable("services__api__http__0")
    ?? Environment.GetEnvironmentVariable("services__api__https__0")
    ?? "http://localhost:15046";

Console.WriteLine($"Connecting to API at: {apiBaseUrl}");
Console.WriteLine();

// Demo JWT token (validation is disabled in API's Program.cs for demo purposes)
// This is a valid JWT structure but with fake claims - works because validation is disabled
const string demoToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJkZW1vLXVzZXIiLCJuYW1lIjoiRGVtbyBVc2VyIiwiaWF0IjoxNzE2MjM5MDIyfQ.demo-signature";

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

    // 5. DELETE - Delete the account (returns 204 No Content)
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

    // ========================================
    // ADDITIONAL OPERATIONS
    // ========================================

    // 7. Paginated list
    Console.WriteLine("7. PAGINATED - Listing paginated accounts (pageSize: 5, pageIndex: 0)...");
    var paginatedResult = await client
        .ListPaginatedAccountsAsync(
            new ListPaginatedAccountsParameters(QueryString: null, Continuation: null, PageSize: 5, PageIndex: 0),
            CancellationToken.None)
        .ConfigureAwait(false);

    if (paginatedResult != null)
    {
        Console.WriteLine($"   Page Size: {paginatedResult.PageSize}");
        Console.WriteLine($"   Page Index: {paginatedResult.PageIndex}");
        Console.WriteLine($"   Count: {paginatedResult.Count}");
        Console.WriteLine($"   Total Count: {paginatedResult.TotalCount}");
        Console.WriteLine($"   Results: {paginatedResult.Results?.Count ?? 0} accounts");
    }

    Console.WriteLine();

    // 8. Async-enumerable (streaming)
    Console.WriteLine("8. STREAMING - Listing async-enumerable accounts...");
    Console.WriteLine("   Streaming accounts as they arrive:");
    var streamedCount = 0;
    await foreach (var account in client
                       .ListAsyncEnumerableAccountsAsync(CancellationToken.None)
                       .ConfigureAwait(false))
    {
        streamedCount++;
        if (streamedCount <= 3)
        {
            Console.WriteLine($"   - {account.Name} (ID: {account.Id})");
        }
    }

    Console.WriteLine(streamedCount > 3
        ? $"   ... and {streamedCount - 3} more (total: {streamedCount})"
        : $"   Total: {streamedCount} accounts");

    Console.WriteLine();

    // ========================================
    // FILE OPERATIONS DEMONSTRATION
    // ========================================
    Console.WriteLine("========================================");
    Console.WriteLine("FILE OPERATIONS DEMONSTRATION");
    Console.WriteLine("========================================");
    Console.WriteLine();
    Console.WriteLine("The API has pre-loaded sample files:");
    Console.WriteLine("  ID 1: sample-readme.txt (text/plain)");
    Console.WriteLine("  ID 2: config-example.json (application/json)");
    Console.WriteLine("  ID 3: data-sample.xml (application/xml)");
    Console.WriteLine("  ID 4: sample-image.png (image/png - red pixel)");
    Console.WriteLine("  ID 5: blue-pixel.png (image/png - blue pixel)");
    Console.WriteLine();

    var filesClient = new FilesClient(httpClient);

    // 9. GET FILE BY ID (existing) - /files/{id}
    Console.WriteLine("9. GET FILE BY ID - Getting pre-loaded file with ID '1' (sample-readme.txt)...");
    await filesClient
        .GetFileByIdAsync(
            new GetFileByIdParameters(Id: "1"),
            CancellationToken.None)
        .ConfigureAwait(false);

    Console.WriteLine("   File retrieved successfully (200 OK)");

    Console.WriteLine();

    // 10. GET FILE BY ID (non-existent) - /files/{id}
    Console.WriteLine("10. GET FILE BY ID - Getting non-existent file with ID '999'...");
    try
    {
        await filesClient
            .GetFileByIdAsync(
                new GetFileByIdParameters(Id: "999"),
                CancellationToken.None)
            .ConfigureAwait(false);

        Console.WriteLine("   File retrieved successfully (200 OK)");
    }
    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        Console.WriteLine("   File not found (404 - expected for non-existent ID)");
    }

    Console.WriteLine();

    // 11. UPLOAD SINGLE FILE - /files/form-data/singleFile
    Console.WriteLine("11. UPLOAD SINGLE FILE - Uploading a text file as octet-stream...");
    var singleFileContent = """
        This is a test document uploaded via the API client.
        It demonstrates single file upload using application/octet-stream.

        Timestamp: {DateTime.UtcNow:O}
        """u8.ToArray();

    using (var singleFileStream = new MemoryStream(singleFileContent))
    {
        await filesClient
            .UploadSingleFileAsFormDataAsync(
                new UploadSingleFileAsFormDataParameters(File: singleFileStream, FileName: "client-upload.txt"),
                CancellationToken.None)
            .ConfigureAwait(false);

        Console.WriteLine($"   Uploaded single file successfully ({singleFileContent.Length} bytes)");
        Console.WriteLine("   - FileName: client-upload.txt");
    }

    Console.WriteLine();

    // 12. UPLOAD MULTIPLE FILES - /files/form-data/multiFile
    Console.WriteLine("12. UPLOAD MULTI FILES - Uploading 3 files as multipart form data...");
    var file1Content = "Document 1: Project requirements and specifications."u8.ToArray();
    var file2Content = "Document 2: Technical design and architecture notes."u8.ToArray();
    var file3Content = """{ "version": "1.0", "status": "uploaded" }"""u8.ToArray();

    using var multiFileStream1 = new MemoryStream(file1Content);
    using var multiFileStream2 = new MemoryStream(file2Content);
    using var multiFileStream3 = new MemoryStream(file3Content);

    await filesClient
        .UploadMultiFilesAsFormDataAsync(
            new UploadMultiFilesAsFormDataParameters(File: [multiFileStream1, multiFileStream2, multiFileStream3]),
            CancellationToken.None)
        .ConfigureAwait(false);

    Console.WriteLine("   Uploaded 3 files successfully:");
    Console.WriteLine($"   - requirements.txt: {file1Content.Length} bytes");
    Console.WriteLine($"   - design.txt: {file2Content.Length} bytes");
    Console.WriteLine($"   - metadata.json: {file3Content.Length} bytes");

    Console.WriteLine();

    // 13. UPLOAD SINGLE OBJECT WITH FILE - /files/form-data/singleObject
    Console.WriteLine("13. UPLOAD OBJECT WITH FILE - Uploading file with metadata...");
    var objectFileContent = "Confidential report data - for authorized personnel only."u8.ToArray();
    using (var objectFileStream = new MemoryStream(objectFileContent))
    {
        var formDataRequest = new FileAsFormDataRequest(
            ItemName: "Q4 Financial Report",
            File: objectFileStream,
            Items: ["finance", "quarterly", "confidential"]);

        await filesClient
            .UploadSingleObjectWithFileAsFormDataAsync(
                new UploadSingleObjectWithFileAsFormDataParameters(Request: formDataRequest),
                CancellationToken.None)
            .ConfigureAwait(false);

        Console.WriteLine("   Uploaded object with file successfully:");
        Console.WriteLine($"   - ItemName: {formDataRequest.ItemName}");
        Console.WriteLine($"   - Tags: [{string.Join(", ", formDataRequest.Items)}]");
        Console.WriteLine($"   - File size: {objectFileContent.Length} bytes");
    }

    Console.WriteLine();

    // 14. UPLOAD SINGLE OBJECT WITH MULTIPLE FILES - /files/form-data/singleObjectMultiFile
    Console.WriteLine("14. UPLOAD OBJECT WITH MULTI FILES - Uploading batch of attachments...");
    var attachment1Content = "Attachment 1: Supporting documentation for the main report."u8.ToArray();
    var attachment2Content = "Attachment 2: Additional charts and graphs in text format."u8.ToArray();

    using var attachmentStream1 = new MemoryStream(attachment1Content);
    using var attachmentStream2 = new MemoryStream(attachment2Content);

    var filesFormDataRequest = new FilesAsFormDataRequest(
        Files: [attachmentStream1, attachmentStream2]);

    await filesClient
        .UploadSingleObjectWithFilesAsFormDataAsync(
            new UploadSingleObjectWithFilesAsFormDataParameters(Request: filesFormDataRequest),
            CancellationToken.None)
        .ConfigureAwait(false);

    Console.WriteLine("   Uploaded batch of attachments successfully:");
    Console.WriteLine($"   - Total files: {filesFormDataRequest.Files.Count}");
    Console.WriteLine($"   - Attachment 1: {attachment1Content.Length} bytes");
    Console.WriteLine($"   - Attachment 2: {attachment2Content.Length} bytes");

    Console.WriteLine();
    Console.WriteLine("========================================");
    Console.WriteLine("All operations completed successfully!");
    Console.WriteLine("========================================");
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"HTTP Error: {ex.Message}");
    Console.WriteLine($"  Status Code: {ex.StatusCode}");
    Console.WriteLine("  Make sure the API is running (via Aspire or standalone)");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"  Stack: {ex.StackTrace}");
}
