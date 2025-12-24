namespace Atc.CodeGeneration.CSharp.Helpers;

/// <summary>
/// Helper for getting descriptions from enumerations used by the code generation library.
/// </summary>
internal static class EnumDescriptionHelper
{
    /// <summary>
    /// Gets the description from the enumeration's DescriptionAttribute.
    /// </summary>
    /// <param name="enumeration">The enumeration value.</param>
    /// <returns>The description from the DescriptionAttribute, or the enum name if not found.</returns>
    public static string GetDescription(Enum enumeration)
    {
        if (enumeration is null)
        {
            return string.Empty;
        }

        var enumType = enumeration.GetType();
        var enumName = enumeration.ToString();
        var memberInfo = enumType.GetMember(enumName);

        if (memberInfo.Length <= 0)
        {
            return enumName;
        }

        var attributes = memberInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
        return attributes.Length > 0
            ? ((DescriptionAttribute)attributes[0]).Description
            : enumName;
    }
}