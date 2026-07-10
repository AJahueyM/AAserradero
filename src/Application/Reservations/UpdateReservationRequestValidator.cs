using FluentValidation;

namespace AntiguoAserradero.Application.Reservations;

public sealed class UpdateReservationRequestValidator : AbstractValidator<UpdateReservationRequest>
{
    public UpdateReservationRequestValidator()
    {
        RuleFor(request => request.RoomId).GreaterThan(0);
        RuleFor(request => request.ClientId).GreaterThan(0);
        RuleFor(request => request.PromotorId).GreaterThan(0);
        RuleFor(request => request.EntryDate).LessThan(request => request.ExitDate);
        RuleFor(request => request.Adults).GreaterThanOrEqualTo(0);
        RuleFor(request => request.Children).GreaterThanOrEqualTo(0);
        RuleFor(request => request.Infants).GreaterThanOrEqualTo(0);
        RuleFor(request => request.Pets).GreaterThanOrEqualTo(0);
        RuleFor(request => request.Fare).GreaterThanOrEqualTo(0m).When(request => request.Fare.HasValue);
    }
}
