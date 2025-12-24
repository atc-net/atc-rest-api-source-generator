namespace Showcase.BlazorApp.Pages;

public partial class Accounts
{
    [Inject]
    private GatewayService Gateway { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    [Inject]
    private IDialogService DialogService { get; set; } = null!;

    private Account[] accounts = [];
    private bool isLoading;
    private int? limit;
    private string newAccountName = string.Empty;
    private string? newAccountTag;

    private async Task LoadAccountsAsync()
    {
        isLoading = true;
        try
        {
            accounts = await Gateway.ListAccountsAsync(limit) ?? [];
            Snackbar.Add($"Loaded {accounts.Length} accounts", Severity.Success);
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

    private async Task CreateAccountAsync()
    {
        if (string.IsNullOrWhiteSpace(newAccountName))
        {
            Snackbar.Add("Name is required", Severity.Warning);
            return;
        }

        isLoading = true;
        try
        {
            var account = new Account(0, newAccountName, newAccountTag ?? string.Empty);
            var created = await Gateway.CreateAccountAsync(account);
            if (created != null)
            {
                Snackbar.Add($"Created account: {created.Name} (ID: {created.Id})", Severity.Success);
                newAccountName = string.Empty;
                newAccountTag = null;
                await LoadAccountsAsync();
            }
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

    private async Task DeleteAccountAsync(string id)
    {
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.ContentText, "Are you sure you want to delete this account? This action cannot be undone." },
            { x => x.ButtonText, "Delete" },
            { x => x.Color, Color.Error },
        };

        var dialog = await DialogService.ShowAsync<ConfirmDialog>("Delete Account", parameters);
        var result = await dialog.Result;

        if (result is null || result.Canceled)
        {
            return;
        }

        isLoading = true;
        try
        {
            await Gateway.DeleteAccountByIdAsync(id);
            Snackbar.Add($"Deleted account {id}", Severity.Success);
            await LoadAccountsAsync();
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
}