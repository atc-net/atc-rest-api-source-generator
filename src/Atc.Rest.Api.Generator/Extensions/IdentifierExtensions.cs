namespace Atc.Rest.Api.Generator.Extensions;

/// <summary>
/// The rules every generated name follows: it starts as an identifier, and two names in one scope are numbered
/// (<c>name</c>, <c>name2</c>, <c>name3</c>, ...) - none is dropped, as each stands for something on the wire.
/// </summary>
[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "C# 14 extension blocks.")]
[SuppressMessage("Naming", "CA1708:Identifiers should differ by more than case", Justification = "C# 14 extension blocks.")]
public static class IdentifierExtensions
{
    extension(ISet<string> taken)
    {
        /// <summary>
        /// Takes the first of <paramref name="name"/>, <c>name2</c>, <c>name3</c>, ... that is not taken yet.
        /// </summary>
        /// <param name="name">The name wanted.</param>
        /// <returns>The name taken.</returns>
        public string Claim(string name)
        {
            var unique = name;
            var number = 1;
            while (!taken.Add(unique))
            {
                number++;
                unique = name + number.ToString(CultureInfo.InvariantCulture);
            }

            return unique;
        }
    }

    extension(string name)
    {
        /// <summary>
        /// The name with <paramref name="prefix"/> in front when it does not start as an identifier does (a letter or
        /// <c>_</c>): <c>2fa</c> becomes <c>Header2fa</c> with the prefix <c>Header</c>.
        /// </summary>
        /// <param name="prefix">The prefix of a name that starts with a digit or is empty.</param>
        /// <returns>The name, starting as an identifier.</returns>
        public string WithIdentifierStart(string prefix)
            => name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_')
                ? name
                : prefix + name;
    }
}