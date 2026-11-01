using DataProcessor.Domain.Entities.MeterReadings;
using DataProcessor.Domain.Entities.MeterReadings.Payloads;
using DataProcessor.Infrastructure.Database.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataProcessor.Infrastructure.Database.Configurations;

internal sealed class MeterReadingConfiguration : IEntityTypeConfiguration<MeterReading>
{
    public void Configure(EntityTypeBuilder<MeterReading> builder)
    {
        builder.ToTable("meter_readings");

        builder.HasKey(reading => reading.Id);

        builder.Property(reading => reading.Payload)
            .HasColumnType("jsonb")
            .HasConversion(
                payload => ReadingPayloadSerializer.Serialize(payload),
                json => ReadingPayloadSerializer.Deserialize(json),
                new ValueComparer<ReadingPayload>(
                    (left, right) => Equals(left, right),
                    payload => payload.GetHashCode(),
                    payload => payload));

        builder.HasIndex(reading => reading.DeduplicationKey)
            .IsUnique();

        builder.HasIndex(reading => new { reading.Type, reading.Location, reading.ReceivedAt });

        builder.HasIndex(reading => reading.ReceivedAt);
    }
}
