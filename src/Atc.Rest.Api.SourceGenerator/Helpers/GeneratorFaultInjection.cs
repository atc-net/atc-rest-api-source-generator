namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// Lets a test make one generator step throw, so it can prove the step's catch reports the failure as its diagnostic
/// instead of failing the generator (CS8785). No known input makes those steps throw. The step is set per execution
/// context, so only the generator run of the test that sets it fails; outside tests it is never set.
/// </summary>
internal static class GeneratorFaultInjection
{
    private static readonly System.Threading.AsyncLocal<string?> FailingStep = new();

    /// <summary>
    /// Makes the step named by <paramref name="ruleId"/> (the diagnostic its catch reports) throw until the returned
    /// value is disposed.
    /// </summary>
    /// <param name="ruleId">The rule id of the step's catch, e.g. <see cref="RuleIdentifiers.ServerGenerationError"/>.</param>
    /// <returns>A value that clears the fault when disposed.</returns>
    internal static IDisposable FailAt(string ruleId)
    {
        FailingStep.Value = ruleId;
        return new Clear();
    }

    /// <summary>
    /// Throws when a test asked the step named by <paramref name="ruleId"/> to fail.
    /// </summary>
    /// <param name="ruleId">The rule id the step's catch reports.</param>
    internal static void ThrowIfRequested(string ruleId)
    {
        if (string.Equals(FailingStep.Value, ruleId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"A test made the step reported as {ruleId} fail.");
        }
    }

    private sealed class Clear : IDisposable
    {
        public void Dispose()
            => FailingStep.Value = null;
    }
}