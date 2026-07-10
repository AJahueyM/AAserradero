using FluentValidation;

namespace AntiguoAserradero.Application.Reservations;

public sealed class CreateReservationRequestValidator : AbstractValidator<CreateReservationRequest>
{
    public CreateReservationRequestValidator()
    {
        RuleFor(request => request.EntryDate).LessThan(request => request.ExitDate);
        RuleFor(request => request.Fare).GreaterThanOrEqualTo(0m);
    }
}
