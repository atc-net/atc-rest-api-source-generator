namespace Showcase.BlazorApp.Pages;

public partial class Tasks
{
    [Inject]
    private GatewayService Gateway { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    [Inject]
    private IDialogService DialogService { get; set; } = null!;

    private Showcase.Generated.Tasks.Models.Task[] tasks = [];
    private bool isLoading;
    private int? limit;
    private string newTaskName = string.Empty;
    private string? newTaskTag;

    private async Task LoadTasksAsync()
    {
        isLoading = true;
        try
        {
            tasks = await Gateway
                .ListTasksAsync(limit)
                .ConfigureAwait(false) ?? [];

            Snackbar.Add($"Loaded {tasks.Length} tasks", Severity.Success);
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

    private async Task CreateTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(newTaskName))
        {
            Snackbar.Add("Name is required", Severity.Warning);
            return;
        }

        isLoading = true;
        try
        {
            var task = new Showcase.Generated.Tasks.Models.Task(0, newTaskName, newTaskTag ?? string.Empty);
            var created = await Gateway
                .CreateTaskAsync(task)
                .ConfigureAwait(false);

            if (created != null)
            {
                Snackbar.Add($"Created task: {created.Name} (ID: {created.Id})", Severity.Success);
                newTaskName = string.Empty;
                newTaskTag = null;
                await LoadTasksAsync().ConfigureAwait(false);
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

    private async Task DeleteTaskAsync(string id)
    {
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.ContentText, "Are you sure you want to delete this task? This action cannot be undone." },
            { x => x.ButtonText, "Delete" },
            { x => x.Color, Color.Error },
        };

        var dialog = await DialogService
            .ShowAsync<ConfirmDialog>("Delete Task", parameters)
            .ConfigureAwait(false);

        var result = await dialog.Result.ConfigureAwait(false);

        if (result is null || result.Canceled)
        {
            return;
        }

        isLoading = true;
        try
        {
            await Gateway
                .DeleteTaskByIdAsync(id)
                .ConfigureAwait(false);

            Snackbar.Add($"Deleted task {id}", Severity.Success);
            await LoadTasksAsync().ConfigureAwait(false);
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