using DataProcessor.Application.Interfaces.Database;
using DataProcessor.Domain.Constants.Enums;
using DataProcessor.Domain.Entities;
using DataProcessor.Domain.Entities.MeterReadings;
using DataProcessor.Domain.Entities.MeterReadings.Payloads;
using DataProcessor.Infrastructure.Database;
using DataProcessor.Infrastructure.Database.ValueGenerators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Shouldly;
using Xunit;

namespace DataProcessor.UnitTests.Infrastructure.Database;

// No database is needed: the model is built and keys are generated on Add, before anything is sent to PostgreSQL.
public sealed class ApplicationDbContextTests : IAsyncDisposable
{
    private readonly ApplicationDbContext _sut = new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .Options);

    public async ValueTask DisposeAsync()
    {
        await _sut.DisposeAsync();
    }

    // Npgsql generates UUID v7 keys by default too, so the test checks that our generator is the one chosen.
    [Fact]
    public void Model_AnyEntity_GeneratesIdWithGuidVersion7ValueGenerator()
    {
        // Arrange
        var generatorSelector = _sut.GetService<IValueGeneratorSelector>();
        var entityTypes = _sut.Model.GetEntityTypes().ToList();

        // Assert
        entityTypes.ShouldNotBeEmpty();
        entityTypes.ShouldAllBe(entityType => typeof(BaseEntity).IsAssignableFrom(entityType.ClrType));

        foreach (var entityType in entityTypes)
        {
            var idProperty = entityType.FindProperty(nameof(BaseEntity.Id)).ShouldNotBeNull();

            generatorSelector.TrySelect(idProperty, entityType, out var generator).ShouldBeTrue();
            generator.ShouldBeOfType<GuidVersion7ValueGenerator>();
        }
    }

    [Fact]
    public void Add_NewEntity_AssignsUuidVersion7Id()
    {
        // Arrange
        var reading = new MeterReading
        {
            DeduplicationKey = Guid.CreateVersion7(),
            Type = MeterType.Energy,
            Location = MeterLocation.Office,
            ReceivedAt = DateTimeOffset.UtcNow,
            Payload = new EnergyPayload { Energy = 845.62 }
        };

        // Act
        ((IApplicationDbContext)_sut).Add(reading);

        // Assert
        reading.Id.ShouldNotBe(Guid.Empty);
        reading.Id.Version.ShouldBe(7);
    }
}
