namespace Showcase.Api.Domain.Validators;

/// <summary>
/// FluentValidation validator for the whole updateUserById request - the generated parameters record, the route's
/// <c>userId</c> and the body together: a renamed user may not take another user's name, but may keep its own.
/// </summary>
/// <remarks>
/// ValidationFilter&lt;UpdateUserByIdParameters&gt; runs this validator for the record and
/// <see cref="UpdateUserRequestValidator"/> for its body. An error on <c>Request.LastName</c> is keyed by the body's
/// wire name, <c>lastName</c>, as the body validator's are.
/// </remarks>
public sealed class UpdateUserByIdParametersValidator : AbstractValidator<UpdateUserByIdParameters>
{
    public UpdateUserByIdParametersValidator(UserInMemoryRepository users)
    {
        RuleFor(x => x.Request.LastName)
            .MustAsync((parameters, lastName, cancellationToken) => UserRules.IsNameFreeAsync(users, parameters.Request.FirstName, lastName, parameters.UserId, cancellationToken))
            .When(x => x.Request is not null && UserRules.IsWorthLookingUp(x.Request.FirstName, x.Request.LastName))
            .WithMessage(x => UserRules.NameTakenMessage(x.Request.FirstName, x.Request.LastName));
    }
}