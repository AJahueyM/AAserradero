using FluentValidation;

namespace AntiguoAserradero.Application.Movements;

public sealed class UpsertMovementRequestValidator : AbstractValidator<UpsertMovementRequest>
{
    public UpsertMovementRequestValidator()
    {
        RuleFor(request => request.ConceptCode).NotEmpty();
        RuleFor(request => request.Charge).GreaterThanOrEqualTo(0m);
        RuleFor(request => request.Payment).GreaterThanOrEqualTo(0m);
        RuleFor(request => request)
            .Must(request => request.Charge > 0m || request.Payment > 0m)
            .WithMessage("A movement must have a charge or payment amount.");
    }
}
