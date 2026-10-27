namespace Atc.Rest.Api.Generator.Cli.Helpers;

/// <summary>
/// A command option whose value names a member of an enum: by its name, in any case. Not by a number, which
/// <see cref="Enum.TryParse{TEnum}(string?, bool, out TEnum)"/> also takes - <c>--hooks 7</c> is no hooks style.
/// </summary>
public static class EnumOptionHelper
{
    /// <summary>
    /// The member a value names.
    /// </summary>
    /// <typeparam name="TEnum">The option's enum.</typeparam>
    /// <param name="value">The value given for the option.</param>
    /// <param name="result">The member, when the value names one.</param>
    /// <returns>Whether the value names a member.</returns>
    public static bool TryParse<TEnum>(
        string? value,
        out TEnum result)
        where TEnum : struct, Enum
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var name = Array.Find(Enum.GetNames<TEnum>(), n => string.Equals(n, value.Trim(), StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            return false;
        }

        result = Enum.Parse<TEnum>(name);
        return true;
    }

    /// <summary>
    /// The names an option takes, for a message: <c>None, ReactQuery, Swr</c>.
    /// </summary>
    /// <typeparam name="TEnum">The option's enum.</typeparam>
    /// <returns>The names, comma-separated.</returns>
    public static string ValidValues<TEnum>()
        where TEnum : struct, Enum
        => string.Join(", ", Enum.GetNames<TEnum>());
}