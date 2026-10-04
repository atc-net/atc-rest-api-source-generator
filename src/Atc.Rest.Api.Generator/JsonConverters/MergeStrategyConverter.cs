namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// JSON converter for <see cref="MergeStrategy"/> (PascalCase or kebab-case) in <c>multiPartConfiguration</c>; an
/// unknown value is <see cref="MergeStrategy.ErrorOnDuplicate"/>.
/// </summary>
public class MergeStrategyConverter : MarkerValueConverter<MergeStrategy>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MergeStrategyConverter"/> class.
    /// </summary>
    public MergeStrategyConverter()
        : base(
            MergeStrategy.ErrorOnDuplicate,
            Value(MergeStrategy.ErrorOnDuplicate, "ErrorOnDuplicate", "error-on-duplicate"),
            Value(MergeStrategy.MergeIfIdentical, "MergeIfIdentical", "merge-if-identical"),
            Value(MergeStrategy.AppendUnique, "AppendUnique", "append-unique"),
            Value(MergeStrategy.FirstWins, "FirstWins", "first-wins"),
            Value(MergeStrategy.LastWins, "LastWins", "last-wins"))
    {
    }
}