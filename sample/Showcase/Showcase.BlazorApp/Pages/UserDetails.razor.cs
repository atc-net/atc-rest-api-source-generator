namespace Showcase.BlazorApp.Pages;

public partial class UserDetails
{
    [Parameter]
    public string UserId { get; set; } = string.Empty;

    [Inject]
    private GatewayService Gateway { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private IDialogService DialogService { get; set; } = null!;

    private User? user;
    private bool isLoading = true;
    private bool isUploading;

    private List<BreadcrumbItem> breadcrumbs = [];

    protected override async Task OnInitializedAsync()
    {
        breadcrumbs =
        [
            new BreadcrumbItem("Users", "/users"),
            new BreadcrumbItem("Details", null, disabled: true),
        ];

        await LoadUserAsync();
    }

    private async Task LoadUserAsync()
    {
        isLoading = true;
        try
        {
            user = await Gateway.GetUserByIdAsync(UserId);
            if (user is not null)
            {
                breadcrumbs =
                [
                    new BreadcrumbItem("Users", "/users"),
                    new BreadcrumbItem($"{user.FirstName} {user.LastName}", null, disabled: true),
                ];
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error loading user: {ex.Message}", Severity.Error);
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task DeleteUserAsync()
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

        try
        {
            await Gateway.DeleteUserByIdAsync(UserId);
            Snackbar.Add("User deleted successfully", Severity.Success);
            Navigation.NavigateTo("/users");
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error deleting user: {ex.Message}", Severity.Error);
        }
    }

    private async Task OnProfileImageSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file is null)
        {
            return;
        }

        // Validate file type
        var allowedTypes = new[] { "image/jpeg", "image/png" };
        if (!allowedTypes.Contains(file.ContentType))
        {
            Snackbar.Add("Only JPEG and PNG images are allowed", Severity.Warning);
            return;
        }

        // Validate file size (max 5MB)
        const long maxSize = 5 * 1024 * 1024;
        if (file.Size > maxSize)
        {
            Snackbar.Add("File size must be less than 5MB", Severity.Warning);
            return;
        }

        isUploading = true;
        try
        {
            // For now, we'll convert to base64 and update avatar URL
            // In a real app, you'd upload to a file service
            using var stream = file.OpenReadStream(maxSize);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            var base64 = Convert.ToBase64String(memoryStream.ToArray());
            var dataUrl = $"data:{file.ContentType};base64,{base64}";

            // Update user with new avatar
            if (user is not null)
            {
                var request = new UpdateUserRequest(
                    FirstName: user.FirstName,
                    LastName: user.LastName,
                    Email: user.Email,
                    Phone: user.Phone,
                    Website: user.Website,
                    DateOfBirth: user.DateOfBirth,
                    Bio: user.Bio,
                    AvatarUrl: new Uri(dataUrl),
                    IsActive: user.IsActive,
                    Role: user.Role,
                    Address: new AddressRequest(
                        Street: user.Address?.Street ?? string.Empty,
                        City: user.Address?.City ?? string.Empty,
                        State: user.Address?.State,
                        PostalCode: user.Address?.PostalCode ?? string.Empty,
                        Country: user.Address?.Country ?? string.Empty,
                        CountryCode: user.Address?.CountryCode,
                        Latitude: user.Address?.Latitude,
                        Longitude: user.Address?.Longitude));

                var (updated, error) = await Gateway.UpdateUserByIdAsync(UserId, request);
                if (updated is not null)
                {
                    user = updated;
                    Snackbar.Add("Profile picture updated successfully", Severity.Success);
                }
                else
                {
                    Snackbar.Add($"Failed to update profile picture: {error}", Severity.Error);
                }
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error uploading image: {ex.Message}", Severity.Error);
        }
        finally
        {
            isUploading = false;
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

    private static string GetMapUrl(
        double lat,
        double lng)
        => $"https://www.openstreetmap.org/export/embed.html?bbox={lng - 0.01},{lat - 0.01},{lng + 0.01},{lat + 0.01}&layer=mapnik&marker={lat},{lng}";

    private static string GetGoogleMapsLink(
        double lat,
        double lng)
        => $"https://www.google.com/maps/search/?api=1&query={lat.ToString(CultureInfo.InvariantCulture)},{lng.ToString(CultureInfo.InvariantCulture)}";

    private static string FormatDateTime(DateTimeOffset dateTime)
        => dateTime.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
}