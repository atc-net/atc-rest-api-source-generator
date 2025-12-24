namespace Showcase.BlazorApp.Pages;

public partial class AccountsAsyncEnumerable : IDisposable
{
    [Inject]
    private GatewayService Gateway { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    private readonly List<Account> accounts = [];
    private bool isLoading;
    private CancellationTokenSource? cts;

    private async Task LoadAsyncEnumerableAsync()
    {
        accounts.Clear();
        isLoading = true;
        cts = new CancellationTokenSource();

        try
        {
            await foreach (var account in Gateway.ListAsyncEnumerableAccountsAsync(cts.Token))
            {
                accounts.Add(account);
                StateHasChanged();

                // Small delay to visualize streaming
                await Task.Delay(50, cts.Token);
            }

            Snackbar.Add($"Completed! Loaded {accounts.Count} accounts", Severity.Success);
        }
        catch (OperationCanceledException)
        {
            Snackbar.Add($"Cancelled. Loaded {accounts.Count} accounts before cancellation", Severity.Warning);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
        finally
        {
            isLoading = false;
            cts?.Dispose();
            cts = null;
        }
    }

    private void CancelStreaming()
    {
        cts?.Cancel();
    }

    private void ClearAccounts()
    {
        accounts.Clear();
    }

    public void Dispose()
    {
        cts?.Cancel();
        cts?.Dispose();
    }
}