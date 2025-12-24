namespace Showcase.Api.Domain.Validators;

/// <summary>
/// FluentValidation validator for CreateUserRequest.
/// </summary>
/// <remarks>
/// This validator works alongside DataAnnotations validation.
/// ValidationFilter&lt;T&gt; from Atc.Rest.MinimalApi automatically discovers and executes
/// validators for [FromBody] properties in Parameters classes.
/// </remarks>
public sealed partial class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .Matches(EnsureFirstCharacterUpperCase())
            .WithMessage(x => $"{nameof(x.FirstName)} must start with an uppercase letter.");

        RuleFor(x => x.LastName)
            .Matches(EnsureFirstCharacterUpperCase())
            .WithMessage(x => $"{nameof(x.LastName)} must start with an uppercase letter.")
            .NotEqual(x => x.FirstName)
            .WithMessage("FirstName and LastName must not be the same.");

        RuleFor(x => x.Address)
            .NotNull()
            .WithMessage("Address is required.");

        When(x => x.Address is not null, () =>
        {
            RuleFor(x => x.Address.City)
                .Matches(EnsureFirstCharacterUpperCase())
                .WithMessage("Address.City must start with an uppercase letter.");

            RuleFor(x => x.Address.Country)
                .Matches(EnsureFirstCharacterUpperCase())
                .WithMessage("Address.Country must start with an uppercase letter.");
        });
    }

    [GeneratedRegex("^[A-Z]", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EnsureFirstCharacterUpperCase();
}