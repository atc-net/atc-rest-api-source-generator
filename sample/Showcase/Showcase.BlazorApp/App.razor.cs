namespace Showcase.BlazorApp;

public partial class App
{
    private MudThemeProvider mudThemeProvider = null!;
    private bool isDarkMode = true;

    public bool IsDarkMode
    {
        get => isDarkMode;
        set
        {
            if (isDarkMode != value)
            {
                isDarkMode = value;
                StateHasChanged();
            }
        }
    }

    public void ToggleDarkMode()
        => IsDarkMode = !IsDarkMode;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            isDarkMode = await mudThemeProvider.GetSystemDarkModeAsync();
            await InvokeAsync(StateHasChanged);
        }
    }
}