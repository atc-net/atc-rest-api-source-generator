namespace Atc.Rest.Api.Generator.Cli.Tests.Helpers;

/// <summary>
/// Ensures PathHelper tests run serially since they mutate process CWD.
/// </summary>
[CollectionDefinition("PathHelperSerial", DisableParallelization = true)]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Required xUnit collection-definition naming convention.")]
public sealed class PathHelperSerialCollection;