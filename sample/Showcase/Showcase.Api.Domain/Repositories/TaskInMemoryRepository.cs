#pragma warning disable CA5394, SCS0005 // Random is used only for sample data generation

namespace Showcase.Api.Domain.Repositories;

public sealed class TaskInMemoryRepository
{
    private static readonly string[] TaskPrefixes =
    [
        "Review", "Update", "Create", "Fix", "Implement", "Design", "Test", "Deploy",
        "Refactor", "Optimize", "Document", "Analyze", "Configure", "Migrate", "Debug",
        "Validate", "Monitor", "Setup", "Integrate", "Research"
    ];

    private static readonly string[] TaskSuffixes =
    [
        "database schema", "API endpoints", "user authentication", "CI/CD pipeline",
        "unit tests", "documentation", "performance metrics", "security audit",
        "code review", "deployment scripts", "error handling", "logging system",
        "caching layer", "search functionality", "notification service", "backup system"
    ];

    private static readonly string[] Tags = ["high-priority", "low-priority", "in-progress", "blocked"];

    private readonly List<Showcase.Generated.Tasks.Models.Task> tasks = [];
    private long nextId = 1;

    public TaskInMemoryRepository()
    {
        var random = new Random(42); // Fixed seed for reproducibility

        for (var i = 0; i < 50; i++)
        {
            var prefix = TaskPrefixes[random.Next(TaskPrefixes.Length)];
            var suffix = TaskSuffixes[random.Next(TaskSuffixes.Length)];
            var tag = Tags[random.Next(Tags.Length)];
            var name = $"{prefix} {suffix}";

            tasks.Add(new Showcase.Generated.Tasks.Models.Task(nextId++, name, tag));
        }
    }

    public async Task<Showcase.Generated.Tasks.Models.Task[]> GetAll(
        int? limit = null)
    {
        await Task
            .Delay(1)
            .ConfigureAwait(false);

        var query = tasks.AsEnumerable();

        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        return query.ToArray();
    }

    public async Task<Showcase.Generated.Tasks.Models.Task?> GetById(string id)
    {
        await Task
            .Delay(1)
            .ConfigureAwait(false);

        if (long.TryParse(id, out var taskId))
        {
            return tasks.FirstOrDefault(t => t.Id == taskId);
        }
        else
        {
            return null;
        }
    }

    public async Task<Showcase.Generated.Tasks.Models.Task> Add(
        string name,
        string? tag = null)
    {
        await Task
            .Delay(1)
            .ConfigureAwait(false);

        var newTask = new Showcase.Generated.Tasks.Models.Task(nextId++, name, tag ?? string.Empty);
        tasks.Add(newTask);

        return newTask;
    }

    public async Task<Showcase.Generated.Tasks.Models.Task> Create(
        long id,
        string name,
        string? tag = null)
    {
        await Task
            .Delay(1)
            .ConfigureAwait(false);

        // Check if task with this ID already exists
        var existing = tasks.FirstOrDefault(t => t.Id == id);
        if (existing != null)
        {
            return existing;
        }

        var newTask = new Showcase.Generated.Tasks.Models.Task(id, name, tag ?? string.Empty);
        tasks.Add(newTask);

        // Update nextId if necessary
        if (id >= nextId)
        {
            nextId = id + 1;
        }

        return newTask;
    }

    public async Task<Showcase.Generated.Tasks.Models.Task?> Update(
        string id,
        string name,
        string? tag = null)
    {
        await Task
            .Delay(1)
            .ConfigureAwait(false);

        if (!long.TryParse(id, out var taskId))
        {
            return null;
        }

        var existingIndex = tasks.FindIndex(t => t.Id == taskId);
        if (existingIndex < 0)
        {
            return null;
        }

        var updatedTask = new Showcase.Generated.Tasks.Models.Task(taskId, name, tag ?? string.Empty);
        tasks[existingIndex] = updatedTask;

        return updatedTask;
    }

    public async Task<Showcase.Generated.Tasks.Models.Task?> Delete(string id)
    {
        await Task
            .Delay(1)
            .ConfigureAwait(false);

        if (!long.TryParse(id, out var taskId))
        {
            return null;
        }

        var existingIndex = tasks.FindIndex(t => t.Id == taskId);
        if (existingIndex < 0)
        {
            return null;
        }

        var deletedTask = tasks[existingIndex];
        tasks.RemoveAt(existingIndex);

        return deletedTask;
    }
}