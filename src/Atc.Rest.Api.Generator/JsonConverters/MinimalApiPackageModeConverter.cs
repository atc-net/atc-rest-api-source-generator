namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// JSON converter for MinimalApiPackageMode that handles strings, booleans, and various formats.
/// </summary>
/// <remarks>
/// Supported formats:
/// - "auto" → Auto
/// - true / "true" / "enabled" → Enabled
/// - false / "false" / "disabled" → Disabled
/// </remarks>
public class MinimalApiPackageModeConverter : MarkerValueConverter<MinimalApiPackageMode>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MinimalApiPackageModeConverter"/> class.
    /// </summary>
    public MinimalApiPackageModeConverter()
        : base(
            MinimalApiPackageMode.Auto,
            Value(MinimalApiPackageMode.Auto, "auto"),
            Value(MinimalApiPackageMode.Enabled, "enabled", "true"),
            Value(MinimalApiPackageMode.Disabled, "disabled", "false"))
    {
    }

    /// <inheritdoc />
    public override bool AcceptsBooleans => true;

    /// <inheritdoc />
    protected override MinimalApiPackageMode ReadBoolean(bool value)
        => value ? MinimalApiPackageMode.Enabled : MinimalApiPackageMode.Disabled;
}