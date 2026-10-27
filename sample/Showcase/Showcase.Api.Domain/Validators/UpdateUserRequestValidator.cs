namespace Showcase.Api.Domain.Validators;

/// <summary>
/// FluentValidation validator for UpdateUserRequest: the rules that only need the body.
/// </summary>
/// <remarks>
/// ValidationFilter&lt;UpdateUserByIdParameters&gt; finds this validator because UpdateUserByIdParameters has a
/// [FromBody] property of type UpdateUserRequest. The duplicate-name check needs the user's id from the route as well,
/// so it is in <see cref="UpdateUserByIdParametersValidator"/>; both run.
/// </remarks>
public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .Matches(UserRules.FirstCharacterUpperCase())
            .WithMessage(x => $"{nameof(x.FirstName)} must start with an uppercase letter.");

        RuleFor(x => x.LastName)
            .Matches(UserRules.FirstCharacterUpperCase())
            .WithMessage(x => $"{nameof(x.LastName)} must start with an uppercase letter.")
            .NotEqual(x => x.FirstName)
            .WithMessage("FirstName and LastName must not be the same.");

        RuleFor(x => x.DateOfBirth)
            .Must(UserRules.IsAdult)
            .WithMessage(UserRules.AdultMessage);

        RuleFor(x => x.Email)
            .Must(UserRules.IsCompanyEmail)
            .When(x => x.Role == UserRole.Admin)
            .WithMessage(UserRules.CompanyEmailMessage);

        RuleFor(x => x.Address)
            .NotNull()
            .WithMessage("Address is required.");

        When(x => x.Address is not null, () =>
        {
            RuleFor(x => x.Address.City)
                .Matches(UserRules.FirstCharacterUpperCase())
                .WithMessage("Address.City must start with an uppercase letter.");

            RuleFor(x => x.Address.Country)
                .Matches(UserRules.FirstCharacterUpperCase())
                .WithMessage("Address.Country must start with an uppercase letter.");
        });
    }
}