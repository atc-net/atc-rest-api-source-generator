namespace Showcase.BlazorApp.Pages;

public partial class AccountsPaginated
{
    [Inject]
    private GatewayService Gateway { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    private Account[] accounts = [];
    private PaginatedResult<Account>? result;
    private bool isLoading;
    private int? pageSize = 10;
    private int? pageIndex = 0;
    private string? queryString;
    private string? continuation;

    private async Task LoadPaginatedAsync()
    {
        isLoading = true;
        try
        {
            result = await Gateway.ListPaginatedAccountsAsync(pageSize, pageIndex, queryString, continuation);
            accounts = result?.Results ?? [];
            Snackbar.Add($"Loaded {accounts.Length} accounts (Total: {result?.TotalCount})", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task LoadNextPageAsync()
    {
        if (result?.Continuation != null)
        {
            continuation = result.Continuation;
            await LoadPaginatedAsync();
        }
    }
}