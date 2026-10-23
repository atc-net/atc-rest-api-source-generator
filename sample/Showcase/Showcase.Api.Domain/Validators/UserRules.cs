namespace Showcase.Api.Domain.Validators;

/// <summary>
/// The domain's rules for a user - what the OpenAPI specification cannot say - shared by the create and the update
/// validators. The specification's own constraints (required, lengths, format: email, the phone pattern, ...) are the
/// generated DataAnnotations on the models; these run next to them, and both answer in the same 400.
/// </summary>
public static partial class UserRules
{
    /// <summary>
    /// The company's e-mail domain: an administrator's address must be in it.
    /// </summary>
    public const string CompanyDomain = "example.com";

    public const string AdultMessage = "A user must be 18 or older.";

    public const string CompanyEmailMessage = "An administrator must have an @" + CompanyDomain + " e-mail address.";

    /// <summary>
    /// The time the duplicate-name check takes, standing for a database round trip.
    /// </summary>
    public static readonly TimeSpan LookupDelay = TimeSpan.FromMilliseconds(500);

    public static bool StartsWithUppercase(string? value)
        => value is not null && FirstCharacterUpperCase().IsMatch(value);

    public static bool IsAdult(DateOnly dateOfBirth)
        => dateOfBirth <= DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-18);

    public static bool IsCompanyEmail(string? email)
        => email is not null && email.EndsWith("@" + CompanyDomain, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a name may be looked up at all: the expensive check runs only for a name that passed the cheap rules (the
    /// specification's length, an uppercase first letter). FluentValidation runs even when DataAnnotations already
    /// failed, so without this guard an invalid request would wait for the lookup too.
    /// </summary>
    public static bool IsWorthLookingUp(
        string? firstName,
        string? lastName)
        => firstName is { Length: >= 2 and <= 50 } &&
           lastName is { Length: >= 2 and <= 50 } &&
           StartsWithUppercase(firstName) &&
           StartsWithUppercase(lastName) &&
           !string.Equals(firstName, lastName, StringComparison.Ordinal);

    /// <summary>
    /// Whether no other user has this name - asynchronous, as a database lookup is.
    /// </summary>
    public static async Task<bool> IsNameFreeAsync(
        UserInMemoryRepository users,
        string firstName,
        string lastName,
        Guid? exceptUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(users);

        await Task.Delay(LookupDelay, cancellationToken);
        return !await users.ExistsWithName(firstName, lastName, exceptUserId);
    }

    public static string NameTakenMessage(
        string firstName,
        string lastName)
        => $"A user named {firstName} {lastName} already exists.";

    /// <summary>
    /// A value that starts with an uppercase letter (FluentValidation's <c>Matches</c> leaves a missing value to the
    /// specification's required).
    /// </summary>
    [GeneratedRegex("^[A-Z]", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    public static partial Regex FirstCharacterUpperCase();
}