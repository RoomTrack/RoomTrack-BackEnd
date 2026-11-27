using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomTrack.Accommodations.API.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "report_schedules",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    hotel_id = table.Column<int>(type: "int", nullable: false),
                    frequency = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    recipient_list = table.Column<string>(type: "varchar(2550)", maxLength: 2550, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    include_productivity = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    include_service_times = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    next_run_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    last_sent_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    created_by_user_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_report_schedules", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "i_x_report_schedules__hotel_id",
                table: "report_schedules",
                column: "hotel_id");

            migrationBuilder.CreateIndex(
                name: "i_x_report_schedules__is_active__next_run_at",
                table: "report_schedules",
                columns: new[] { "is_active", "next_run_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "report_schedules");
        }
    }
}
