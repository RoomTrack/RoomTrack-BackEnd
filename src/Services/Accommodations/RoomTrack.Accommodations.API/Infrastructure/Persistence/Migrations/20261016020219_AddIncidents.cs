using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomTrack.Accommodations.API.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_blocked_by_incident",
                table: "rooms",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "incidents",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    hotel_id = table.Column<int>(type: "int", nullable: false),
                    room_id = table.Column<int>(type: "int", nullable: false),
                    title = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    priority = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    blocks_room = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    reported_by_user_id = table.Column<int>(type: "int", nullable: false),
                    reported_by_email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reported_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    assignee_user_id = table.Column<int>(type: "int", nullable: true),
                    assigned_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    resolved_by_user_id = table.Column<int>(type: "int", nullable: true),
                    resolution_notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    escalated_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_incidents", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "incident_histories",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    incident_id = table.Column<int>(type: "int", nullable: false),
                    action = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    detail = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    actor_user_id = table.Column<int>(type: "int", nullable: true),
                    actor_email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    occurred_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_incident_histories", x => x.id);
                    table.ForeignKey(
                        name: "f_k_incident_histories_incidents__incident_id",
                        column: x => x.incident_id,
                        principalTable: "incidents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "i_x_incident_histories__incident_id__occurred_at",
                table: "incident_histories",
                columns: new[] { "incident_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "i_x_incidents__assignee_user_id__status",
                table: "incidents",
                columns: new[] { "assignee_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "i_x_incidents__hotel_id__status",
                table: "incidents",
                columns: new[] { "hotel_id", "status" });

            migrationBuilder.CreateIndex(
                name: "i_x_incidents__room_id__status",
                table: "incidents",
                columns: new[] { "room_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "incident_histories");

            migrationBuilder.DropTable(
                name: "incidents");

            migrationBuilder.DropColumn(
                name: "is_blocked_by_incident",
                table: "rooms");
        }
    }
}
