using AntiguoAserradero.Application.Notifications;
using AntiguoAserradero.Domain.Constants;
using AntiguoAserradero.Domain.Entities;
using AntiguoAserradero.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AntiguoAserradero.Application.UnitTests;

public sealed class NotificationsFeatureTests
{
    [Fact]
    public async Task ConfirmationIncludesLedgerBalanceAndSanitizesGuestContent()
    {
        await using var dbContext = CreateDbContext();
        SeedNotificationData(dbContext);
        await dbContext.SaveChangesAsync();
        var service = new NotificationService(dbContext, new FakeEmailSender(0));

        var content = await service.RenderReservationConfirmationAsync(1, new ReservationConfirmationRequest(true, false));

        Assert.Contains("Hospedaje", content.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("$800.00", content.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Saldo pendiente", content.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", content.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>alert(1)</script>", content.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Banco 123", content.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendUsesAbstractEmailSenderAndRetriesFailures()
    {
        await using var dbContext = CreateDbContext();
        SeedNotificationData(dbContext);
        await dbContext.SaveChangesAsync();
        var sender = new FakeEmailSender(2);
        var service = new NotificationService(dbContext, sender);

        var response = await service.SendReservationConfirmationAsync(1, new SendReservationConfirmationRequest(false, true));

        Assert.True(response.Sent);
        Assert.Equal(3, response.Attempts);
        Assert.Equal(3, sender.Attempts);
    }

    private static AntiguoAserraderoDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AntiguoAserraderoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AntiguoAserraderoDbContext(options);
    }

    private static void SeedNotificationData(AntiguoAserraderoDbContext dbContext)
    {
        var area = new Area { Id = 1, Name = "Reserva" };
        var room = new Room { Id = 1, AreaId = 1, Area = area, Name = "Pino", Capacity = 4, UnitCount = 1 };
        var client = new Client { Id = 1, Name = "<script>alert(1)</script>", Email = "guest@example.com", Cellphone = "555" };
        var status = new ReservationStatus { Id = 1, Code = ReservationStatusCodes.Partial, Label = "Parcial" };
        var concept = new Concept { Id = 1, Code = "LODGE", Name = "Hospedaje" };
        var reservation = new Reservation
        {
            Id = 1,
            ClientId = 1,
            Client = client,
            RoomId = 1,
            Room = room,
            EntryDate = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
            ExitDate = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc),
            CheckInTime = new TimeOnly(15, 0),
            CheckOutTime = new TimeOnly(12, 0),
            Adults = 2,
            Notes = "<b>nota</b>",
            StatusId = 1,
            Status = status,
        };

        dbContext.AddRange(area, room, client, status, concept, reservation, new ConfigValue { Id = 1, Key = ConfigKeys.PaymentInstructions, Value = "Banco 123" });
        dbContext.Movements.AddRange(
            new Movement { Id = 1, ReservationId = 1, Reservation = reservation, ConceptId = 1, Concept = concept, Charge = 1000m, Date = new DateTime(2026, 7, 9, 0, 0, 0, DateTimeKind.Utc) },
            new Movement { Id = 2, ReservationId = 1, Reservation = reservation, ConceptId = 1, Concept = concept, Payment = 200m, Date = new DateTime(2026, 7, 9, 0, 0, 0, DateTimeKind.Utc) });
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        private readonly int _failuresBeforeSuccess;

        public FakeEmailSender(int failuresBeforeSuccess)
        {
            _failuresBeforeSuccess = failuresBeforeSuccess;
        }

        public int Attempts { get; private set; }

        public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Attempts++;
            return Task.FromResult(Attempts <= _failuresBeforeSuccess
                ? new EmailSendResult(false, string.Empty, "transient")
                : new EmailSendResult(true, "fake-message-id"));
        }
    }
}
