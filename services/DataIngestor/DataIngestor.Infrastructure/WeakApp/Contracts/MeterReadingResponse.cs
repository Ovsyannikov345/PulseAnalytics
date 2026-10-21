using System.Text.Json.Serialization;

namespace DataIngestor.Infrastructure.WeakApp.Contracts;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MotionMeterReadingResponse), "motion")]
[JsonDerivedType(typeof(EnergyMeterReadingResponse), "energy")]
[JsonDerivedType(typeof(AirQualityMeterReadingResponse), "air_quality")]
internal abstract record MeterReadingResponse
{
    public required string Name { get; init; }
}
