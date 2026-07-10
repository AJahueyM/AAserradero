using FluentValidation;

namespace AntiguoAserradero.Application.Users;

public sealed class CreateStaffUserRequestValidator : AbstractValidator<CreateStaffUserRequest>
{
    public CreateStaffUserRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);
        RuleFor(request => request.DisplayName)
            .NotEmpty()
            .MaximumLength(200);
        RuleFor(request => request.InitialPassword)
            .NotEmpty()
            .MinimumLength(12)
            .MaximumLength(256);
    }
}

public sealed class UpdateStaffUserRequestValidator : AbstractValidator<UpdateStaffUserRequest>
{
    public UpdateStaffUserRequestValidator()
    {
        RuleFor(request => request.DisplayName)
            .NotEmpty()
            .MaximumLength(200)
            .When(request => request.DisplayName is not null);
        RuleFor(request => request)
            .Must(request => request.DisplayName is not null || request.IsActive.HasValue)
            .WithName(nameof(UpdateStaffUserRequest))
            .WithMessage("At least one staff user field must be provided.");
    }
}

public sealed class StaffUserCapabilityRequestValidator : AbstractValidator<StaffUserCapabilityRequest>
{
    public StaffUserCapabilityRequestValidator()
    {
        RuleFor(request => request.Capability)
            .NotEmpty()
            .Must(capability => capability is not null && StaffUserCapabilities.Allowed.Contains(capability))
            .WithMessage("Capability is not valid for staff user administration.");
    }
}

public sealed class ResetStaffUserPasswordRequestValidator : AbstractValidator<ResetStaffUserPasswordRequest>
{
    public ResetStaffUserPasswordRequestValidator()
    {
        RuleFor(request => request.NewPassword)
            .NotEmpty()
            .MinimumLength(12)
            .MaximumLength(256);
    }
}
