namespace Showcase.BlazorApp.Pages;

public partial class Users
{
    private readonly string[] roles = ["Admin", "Manager", "Employee", "Guest"];

    [Inject]
    private GatewayService Gateway { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private IDialogService DialogService { get; set; } = null!;

    private User[] users = [];
    private string[] countries = [];
    private bool isLoading;
    private string? searchQuery;
    private string? selectedCountry;
    private string? selectedRole;
    private bool? selectedActiveStatus;

    protected override async Task OnInitializedAsync()
    {
        countries = ["Denmark", "Sweden", "Finland", "Germany", "England", "United States"];
        await LoadUsersAsync();
    }

    private async Task LoadUsersAsync()
    {
        isLoading = true;
        try
        {
            users = await Gateway.ListUsersAsync(
                search: searchQuery,
                country: selectedCountry,
                role: selectedRole,
                isActive: selectedActiveStatus) ?? [];

            Snackbar.Add($"Found {users.Length} users", Severity.Success);
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

    private async Task OnSearchKeyUp(KeyboardEventArgs args)
    {
        if (args.Key == "Enter")
        {
            await LoadUsersAsync();
        }
    }

    private async Task ClearFilters()
    {
        searchQuery = null;
        selectedCountry = null;
        selectedRole = null;
        selectedActiveStatus = null;
        await LoadUsersAsync();
    }

    private async Task DeleteUserAsync(string id)
    {
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.ContentText, "Are you sure you want to delete this user? This action cannot be undone." },
            { x => x.ButtonText, "Delete" },
            { x => x.Color, Color.Error },
        };

        var dialog = await DialogService.ShowAsync<ConfirmDialog>("Delete User", parameters);
        var result = await dialog.Result;

        if (result is null || result.Canceled)
        {
            return;
        }

        isLoading = true;
        try
        {
            await Gateway.DeleteUserByIdAsync(id);
            Snackbar.Add("User deleted successfully", Severity.Success);
            await LoadUsersAsync();
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

    private void OnRowClick(TableRowClickEventArgs<User> args)
    {
        if (args.Item is not null)
        {
            Navigation.NavigateTo($"/users/{args.Item.Id}");
        }
    }

    private static Color GetRoleColor(UserRole role)
        => role switch
        {
            UserRole.Admin => Color.Error,
            UserRole.Manager => Color.Warning,
            UserRole.Employee => Color.Info,
            UserRole.Guest => Color.Default,
            _ => Color.Default,
        };
}