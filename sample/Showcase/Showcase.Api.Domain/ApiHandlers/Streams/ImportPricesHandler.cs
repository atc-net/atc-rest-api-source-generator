namespace Showcase.Api.Domain.ApiHandlers.Streams;

/// <summary>
/// Handler business logic for the ImportPrices operation: prices sent as JSON Lines.
/// The body is a stream of records: the handler reads one price at a time with await foreach, each one validated by
/// the generated endpoint as it arrives - a price that is not valid ends the loop with a 400 that names it.
/// </summary>
public sealed class ImportPricesHandler : IImportPricesHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Api.Domain.ApiHandlers.Streams.ImportPrices");

    /// <summary>
    /// Executes the ImportPrices operation.
    /// </summary>
    public async Task<ImportPricesResult> ExecuteAsync(
        ImportPricesParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        using var activity = ActivitySource.StartActivity("ImportPrices");

        var count = 0;
        var symbols = new List<string>();
        var lowest = double.MaxValue;
        var highest = double.MinValue;
        await foreach (var tick in parameters.Request.WithCancellation(cancellationToken))
        {
            count++;
            if (!symbols.Contains(tick.Symbol, StringComparer.Ordinal))
            {
                symbols.Add(tick.Symbol);
            }

            lowest = Math.Min(lowest, tick.Price);
            highest = Math.Max(highest, tick.Price);
        }

        return ImportPricesResult.Ok(new PriceImportSummary(
            count,
            symbols,
            count == 0 ? 0 : lowest,
            count == 0 ? 0 : highest));
    }
}