namespace Showcase.Api.Domain.Validators;

/// <summary>
/// FluentValidation validator for CreateUserRequest: the domain's rules, next to the specification's DataAnnotations.
/// </summary>
/// <remarks>
/// <para>
/// ValidationFilter&lt;CreateUserParameters&gt; from Atc.Rest.MinimalApi finds this validator for the [FromBody]
/// property, runs it after the DataAnnotations and answers both in one 400 (a field with errors from both lists the
/// DataAnnotations message first).
/// </para>
/// <para>
/// <c>AddApiValidatorsFromDomain()</c> registers validators as singletons, so a validator may only depend on singletons:
/// the in-memory repository is one. A validator that needs a scoped service (a DbContext) resolves it per call instead.
/// </para>
/// </remarks>
public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator(UserInMemoryRepository users)
    {
        RuleFor(x => x.FirstName)
            .Matches(UserRules.FirstCharacterUpperCase())
            .WithMessage(x => $"{nameof(x.FirstName)} must start with an uppercase letter.");

        RuleFor(x => x.LastName)
            .Matches(UserRules.FirstCharacterUpperCase())
            .WithMessage(x => $"{nameof(x.LastName)} must start with an uppercase letter.")
            .NotEqual(x => x.FirstName)
            .WithMessage("FirstName and LastName must not be the same.");

        // Asynchronous: no other user may have this name - looked up (half a second, as a database round trip) only
        // when the name passed the cheap rules
        RuleFor(x => x.LastName)
            .MustAsync((request, lastName, cancellationToken) => UserRules.IsNameFreeAsync(users, request.FirstName, lastName, exceptUserId: null, cancellationToken))
            .When(x => UserRules.IsWorthLookingUp(x.FirstName, x.LastName))
            .WithMessage(x => UserRules.NameTakenMessage(x.FirstName, x.LastName));

        RuleFor(x => x.DateOfBirth)
            .Must(UserRules.IsAdult)
            .WithMessage(UserRules.AdultMessage);

        // A rule across two fields: the role decides what the e-mail address may be
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