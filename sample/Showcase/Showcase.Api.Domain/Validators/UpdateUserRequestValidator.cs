namespace Showcase.Api.Domain.Validators;

/// <summary>
/// FluentValidation validator for UpdateUserRequest.
/// </summary>
/// <remarks>
/// This validator is automatically discovered by ValidationFilter&lt;UpdateUserByIdParameters&gt;
/// because UpdateUserByIdParameters has a [FromBody] property of type UpdateUserRequest.
/// </remarks>
public sealed partial class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
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