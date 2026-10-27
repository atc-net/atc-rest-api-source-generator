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
    private EditContext editContext = null!;
    private ValidationMessageStore serverErrors = null!;
    private bool isLoading;

    private bool IsEdit => !string.IsNullOrEmpty(UserId);

    protected override async Task OnInitializedAsync()
    {
        UseModel(model);
        if (IsEdit)
        {
            await LoadUserAsync();
        }
    }

    /// <summary>
    /// The form's edit context, with a message store for what the server says: the 400's field errors - the
    /// specification's and the domain's - shown at their fields next to the client-side checks. A field's server error
    /// goes when the field is edited.
    /// </summary>
    private void UseModel(UserFormModel formModel)
    {
        model = formModel;
        editContext = new EditContext(model);
        serverErrors = new ValidationMessageStore(editContext);
        editContext.OnFieldChanged += (_, e) =>
        {
            serverErrors.Clear(e.FieldIdentifier);
            serverErrors.Clear(new FieldIdentifier(model, string.Empty));
            editContext.NotifyValidationStateChanged();
        };
    }

    private void ShowServerErrors(
        IReadOnlyDictionary<string, string[]>? fieldErrors)
    {
        if (fieldErrors is null)
        {
            return;
        }

        foreach (var (key, messages) in fieldErrors)
        {
            serverErrors.Add(ToFormField(key), messages);
        }

        editContext.NotifyValidationStateChanged();
    }

    /// <summary>
    /// The form field of a wire key: <c>firstName</c> is FirstName, <c>address.city</c> is City (the form is flat). A key
    /// with no field goes to the form itself, and shows in the validation summary.
    /// </summary>
    private FieldIdentifier ToFormField(string wireKey)
    {
        var name = wireKey[(wireKey.LastIndexOf('.') + 1)..];
        var property = name.Length == 0
            ? null
            : typeof(UserFormModel).GetProperty(char.ToUpperInvariant(name[0]) + name[1..]);
        return new FieldIdentifier(model, property?.Name ?? string.Empty);
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

            UseModel(new UserFormModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.Phone,
                Website = user.Website?.ToString(),
                DateOfBirth = user.DateOfBirth.ToDateTime(TimeOnly.MinValue),
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
                Latitude = user.Address?.Latitude,
                Longitude = user.Address?.Longitude,
            });
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
        serverErrors.Clear();
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
                    DateOfBirth: DateOnly.FromDateTime(model.DateOfBirth ?? DateTime.Today),
                    Bio: model.Bio,
                    AvatarUrl: string.IsNullOrEmpty(model.AvatarUrl) ? null : new Uri(model.AvatarUrl),
                    IsActive: model.IsActive,
                    Role: model.Role,
                    Address: address);

                var (updated, error, fieldErrors) = await Gateway.UpdateUserByIdAsync(UserId!, request);
                if (updated is not null)
                {
                    Snackbar.Add("User updated successfully", Severity.Success);
                    Navigation.NavigateTo($"/users/{UserId}");
                }
                else
                {
                    ShowServerErrors(fieldErrors);
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
                    DateOfBirth: DateOnly.FromDateTime(model.DateOfBirth ?? DateTime.Today),
                    Bio: model.Bio,
                    AvatarUrl: string.IsNullOrEmpty(model.AvatarUrl) ? null : new Uri(model.AvatarUrl),
                    IsActive: model.IsActive,
                    Role: model.Role,
                    Address: address);

                var (created, error, fieldErrors) = await Gateway.CreateUserAsync(request);
                if (created is not null)
                {
                    Snackbar.Add($"User created successfully (ID: {created.Id})", Severity.Success);
                    Navigation.NavigateTo($"/users/{created.Id}");
                }
                else
                {
                    ShowServerErrors(fieldErrors);
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
        => role.Value switch
        {
            UserRole.Names.Admin => Color.Error,
            UserRole.Names.Manager => Color.Warning,
            UserRole.Names.Employee => Color.Info,
            UserRole.Names.Guest => Color.Default,
            _ => Color.Default,
        };
}