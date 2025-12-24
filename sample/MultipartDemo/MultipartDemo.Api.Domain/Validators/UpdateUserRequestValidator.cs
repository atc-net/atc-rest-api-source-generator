namespace MultipartDemo.Api.Domain.Validators;

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .MaximumLength(50)
            .When(x => !string.IsNullOrEmpty(x.FirstName));

        RuleFor(x => x.LastName)
            .MaximumLength(50)
            .When(x => !string.IsNullOrEmpty(x.LastName));

        RuleFor(x => x.Email)
            .EmailAddress()
            .MaximumLength(255)
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .When(x => !string.IsNullOrEmpty(x.Phone));
    }
}