namespace Showcase.BlazorApp.Pages;

public partial class UserForm
{
    [Parameter]
    public string? UserId { get; set; }

    [Inject]
    private GatewayService Gateway { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    private UserFormModel model = new();
    private bool isLoading;

    private bool IsEdit => !string.IsNullOrEmpty(UserId);

    protected override async Task OnInitializedAsync()
    {
        if (IsEdit)
        {
            await LoadUserAsync();
        }
    }

    private async Task LoadUserAsync()
    {
        isLoading = true;
        try
        {
            var user = await Gateway.GetUserByIdAsync(UserId!);
            if (user is null)
            {
                Snackbar.Add("User not found", Severity.Error);
                Navigation.NavigateTo("/users");
                return;
            }

            model = new UserFormModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.Phone,
                Website = user.Website?.ToString(),
                DateOfBirth = user.DateOfBirth.DateTime,
                Bio = user.Bio,
                AvatarUrl = user.AvatarUrl?.ToString(),
                IsActive = user.IsActive,
                Role = user.Role,
                Street = user.Address?.Street ?? string.Empty,
                City = user.Address?.City ?? string.Empty,
                State = user.Address?.State,
                PostalCode = user.Address?.PostalCode ?? string.Empty,
                Country = user.Address?.Country ?? string.Empty,
                CountryCode = user.Address?.CountryCode,
                Latitude = user.Address?.Latitude ?? 0,
                Longitude = user.Address?.Longitude ?? 0,
            };
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

    private async Task OnValidSubmit()
    {
        isLoading = true;
        try
        {
            var address = new AddressRequest(
                Street: model.Street,
                City: model.City,
                State: model.State,
                PostalCode: model.PostalCode,
                Country: model.Country,
                CountryCode: model.CountryCode,
                Latitude: model.Latitude,
                Longitude: model.Longitude);

            if (IsEdit)
            {
                var request = new UpdateUserRequest(
                    FirstName: model.FirstName,
                    LastName: model.LastName,
                    Email: model.Email,
                    Phone: model.Phone,
                    Website: string.IsNullOrEmpty(model.Website) ? null : new Uri(model.Website),
                    DateOfBirth: new DateTimeOffset(model.DateOfBirth ?? DateTime.Today),
                    Bio: model.Bio,
                    AvatarUrl: string.IsNullOrEmpty(model.AvatarUrl) ? null : new Uri(model.AvatarUrl),
                    IsActive: model.IsActive,
                    Role: model.Role,
                    Address: address);

                var (updated, error) = await Gateway.UpdateUserByIdAsync(UserId!, request);
                if (updated is not null)
                {
                    Snackbar.Add("User updated successfully", Severity.Success);
                    Navigation.NavigateTo($"/users/{UserId}");
                }
                else
                {
                    Snackbar.Add($"Failed to update user: {error}", Severity.Error);
                }
            }
            else
            {
                var request = new CreateUserRequest(
                    FirstName: model.FirstName,
                    LastName: model.LastName,
                    Email: model.Email,
                    Phone: model.Phone,
                    Website: string.IsNullOrEmpty(model.Website) ? null : new Uri(model.Website),
                    DateOfBirth: new DateTimeOffset(model.DateOfBirth ?? DateTime.Today),
                    Bio: model.Bio,
                    AvatarUrl: string.IsNullOrEmpty(model.AvatarUrl) ? null : new Uri(model.AvatarUrl),
                    IsActive: model.IsActive,
                    Role: model.Role,
                    Address: address);

                var (created, error) = await Gateway.CreateUserAsync(request);
                if (created is not null)
                {
                    Snackbar.Add($"User created successfully (ID: {created.Id})", Severity.Success);
                    Navigation.NavigateTo($"/users/{created.Id}");
                }
                else
                {
                    Snackbar.Add($"Failed to create user: {error}", Severity.Error);
                }
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