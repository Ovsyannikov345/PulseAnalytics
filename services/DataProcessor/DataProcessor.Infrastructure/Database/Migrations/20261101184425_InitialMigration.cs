using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataProcessor.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "meter_readings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    deduplication_key = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    location = table.Column<int>(type: "integer", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meter_readings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_meter_readings_deduplication_key",
                table: "meter_readings",
                column: "deduplication_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meter_readings_received_at",
                table: "meter_readings",
                column: "received_at");

            migrationBuilder.CreateIndex(
                name: "ix_meter_readings_type_location_received_at",
                table: "meter_readings",
                columns: new[] { "type", "location", "received_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meter_readings");
        }
    }
}
