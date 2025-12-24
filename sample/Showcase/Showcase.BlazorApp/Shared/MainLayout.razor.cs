namespace Showcase.BlazorApp.Shared;

public partial class MainLayout
{
    [CascadingParameter]
    private App? appInstance { get; set; }

    private bool drawerOpen = true;

    private void ToggleDrawer()
    {
        drawerOpen = !drawerOpen;
    }

    private void ToggleDarkMode()
    {
        appInstance?.ToggleDarkMode();
    }
}