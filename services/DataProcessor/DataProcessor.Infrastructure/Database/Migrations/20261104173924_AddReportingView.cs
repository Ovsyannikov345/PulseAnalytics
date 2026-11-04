using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataProcessor.Infrastructure.Database.Migrations
{
    /// <summary>
    /// Adds the read contract for other services: the <c>reporting.meter_readings</c> view.
    /// It hides write-side details (deduplication key, jsonb payload format) and flattens the payload
    /// into typed nullable columns, so the table can change as long as the view keeps its shape.
    /// </summary>
    public partial class AddReportingView : Migration
    {
        private const string ReportingSchema = "reporting";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(ReportingSchema);

            migrationBuilder.Sql(
                """
                CREATE VIEW reporting.meter_readings AS
                SELECT
                    id,
                    type,
                    location,
                    received_at,
                    (payload ->> 'motionDetected')::boolean AS motion_detected,
                    (payload ->> 'energy')::double precision AS energy,
                    (payload ->> 'co2')::integer AS co2,
                    (payload ->> 'pm25')::integer AS pm25,
                    (payload ->> 'humidity')::integer AS humidity
                FROM public.meter_readings;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW reporting.meter_readings;");

            migrationBuilder.DropSchema(ReportingSchema);
        }
    }
}
