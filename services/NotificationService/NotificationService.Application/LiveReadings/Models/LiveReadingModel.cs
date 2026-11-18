using NotificationService.Application.LiveReadings.Enums;

namespace NotificationService.Application.LiveReadings.Models;

public abstract record LiveReadingModel
{
    public required Guid Id { get; init; }

    public abstract MeterType Type { get; }

    public required MeterLocation Location { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }
}
