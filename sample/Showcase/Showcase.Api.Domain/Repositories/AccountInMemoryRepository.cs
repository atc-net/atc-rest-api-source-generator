#pragma warning disable CA5394, SCS0005 // Random is used only for sample data generation

namespace Showcase.Api.Domain.Repositories;

public sealed class AccountInMemoryRepository
{
    private static readonly string[] CompanyPrefixes =
    [
        "Acme", "Global", "Tech", "Digital", "Creative", "Innovation", "Premier", "Elite",
        "Alpha", "Beta", "Omega", "Delta", "Prime", "First", "Next", "Future", "Smart",
        "Bright", "Swift", "Peak", "Summit", "Apex", "Core", "Pro", "Max", "Ultra"
    ];

    private static readonly string[] CompanySuffixes =
    [
        "Corporation", "Industries", "Solutions", "Labs", "Systems", "Group", "Partners",
        "Ventures", "Holdings", "Enterprises", "Technologies", "Services", "Consulting",
        "Agency", "Network", "Media", "Dynamics", "Works", "Hub", "Studio"
    ];

    private static readonly string[] Tags = ["enterprise", "startup", "small-business", "mid-market"];

    private readonly List<Account> accounts = [];
    private long nextId = 1;

    public AccountInMemoryRepository()
    {
        var random = new Random(42); // Fixed seed for reproducibility

        for (var i = 0; i < 120; i++)
        {
            var prefix = CompanyPrefixes[random.Next(CompanyPrefixes.Length)];
            var suffix = CompanySuffixes[random.Next(CompanySuffixes.Length)];
            var tag = Tags[random.Next(Tags.Length)];
            var name = $"{prefix} {suffix} {i + 1}";

            accounts.Add(new Account(nextId++, name, tag));
        }
    }

    public async Task<Account[]> GetAll(int? limit = null)
    {
        await Task
            .Delay(1)
            .ConfigureAwait(false);

        var query = accounts.AsEnumerable();

        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        return query.ToArray();
    }

    public async Task<Account?> GetById(string id)
    {
        await Task
            .Delay(1)
            .ConfigureAwait(false);

        if (long.TryParse(id, out var accountId))
        {
            return accounts.FirstOrDefault(a => a.Id == accountId);
        }
        else
        {
            return null;
        }
    }

    public async Task<Account> Create(
        long id,
        string name,
        string? tag = null)
    {
        await Task
            .Delay(1)
            .ConfigureAwait(false);

        // Check if account with this ID already exists
        var existing = accounts.FirstOrDefault(a => a.Id == id);
        if (existing != null)
        {
            return existing;
        }

        var newAccount = new Account(id, name, tag ?? string.Empty);
        accounts.Add(newAccount);

        // Update nextId if necessary
        if (id >= nextId)
        {
            nextId = id + 1;
        }

        return newAccount;
    }

    public async Task<Account?> Update(
        string id,
        string name,
        string? tag = null)
    {
        await Task
            .Delay(1)
            .ConfigureAwait(false);

        if (!long.TryParse(id, out var accountId))
        {
            return null;
        }

        var existingIndex = accounts.FindIndex(a => a.Id == accountId);
        if (existingIndex < 0)
        {
            return null;
        }

        var updatedAccount = new Account(accountId, name, tag ?? string.Empty);
        accounts[existingIndex] = updatedAccount;

        return updatedAccount;
    }

    public async Task<Account?> Delete(string id)
    {
        await Task
            .Delay(1)
            .ConfigureAwait(false);

        if (!long.TryParse(id, out var accountId))
        {
            return null;
        }

        var existingIndex = accounts.FindIndex(a => a.Id == accountId);
        if (existingIndex < 0)
        {
            return null;
        }

        var deletedAccount = accounts[existingIndex];
        accounts.RemoveAt(existingIndex);

        return deletedAccount;
    }

    public async Task<(Account[] Items, int TotalCount)> GetPaginated(
        int pageSize,
        int pageIndex,
        string? queryString = null)
    {
        await Task
            .Delay(1)
            .ConfigureAwait(false);

        var query = accounts.AsEnumerable();

        // Apply search filter if provided
        if (!string.IsNullOrWhiteSpace(queryString))
        {
            query = query.Where(a =>
                a.Name.Contains(queryString, StringComparison.OrdinalIgnoreCase) ||
                (a.Tag?.Contains(queryString, StringComparison.OrdinalIgnoreCase) ?? false));
        }

#pragma warning disable AsyncFixer02 // ToListAsync - not applicable for in-memory collections
        var filteredList = query.ToList();
#pragma warning restore AsyncFixer02
        var totalCount = filteredList.Count;

        // Apply pagination
        var items = filteredList
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToArray();

        return (items, totalCount);
    }

    /// <summary>
    /// Streams accounts asynchronously with simulated delay per item.
    /// Useful for demonstrating IAsyncEnumerable streaming responses.
    /// </summary>
    public async IAsyncEnumerable<Account> GetAllStreaming(
        int? pageSize = null,
        int pageIndex = 0,
        string? queryString = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = accounts.AsEnumerable();

        // Apply search filter if provided
        if (!string.IsNullOrWhiteSpace(queryString))
        {
            query = query.Where(a =>
                a.Name.Contains(queryString, StringComparison.OrdinalIgnoreCase) ||
                (a.Tag?.Contains(queryString, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // Apply pagination
        if (pageIndex > 0)
        {
            query = query.Skip(pageIndex * (pageSize ?? 10));
        }

        if (pageSize.HasValue)
        {
            query = query.Take(pageSize.Value);
        }

        // Stream each account with a small delay to simulate streaming
        foreach (var account in query)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Simulate async I/O delay for each item (e.g., database cursor)
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);

            yield return account;
        }
    }
}