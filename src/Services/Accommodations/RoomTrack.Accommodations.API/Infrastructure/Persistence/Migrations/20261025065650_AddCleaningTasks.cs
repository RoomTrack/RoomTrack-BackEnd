using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomTrack.Accommodations.API.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCleaningTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cleaning_tasks",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    hotel_id = table.Column<int>(type: "int", nullable: false),
                    room_id = table.Column<int>(type: "int", nullable: false),
                    priority = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_by_user_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    assignee_user_id = table.Column<int>(type: "int", nullable: true),
                    assigned_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    assigned_automatically = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    completed_by_user_id = table.Column<int>(type: "int", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    cancellation_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_cleaning_tasks", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "hotel_housekeeping_settings",
                columns: table => new
                {
                    hotel_id = table.Column<int>(type: "int", nullable: false),
                    auto_assign = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    updated_by_user_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_hotel_housekeeping_settings", x => x.hotel_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "cleaning_task_histories",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    cleaning_task_id = table.Column<int>(type: "int", nullable: false),
                    action = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    detail = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    incident_id = table.Column<int>(type: "int", nullable: true),
                    actor_user_id = table.Column<int>(type: "int", nullable: true),
                    actor_email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    occurred_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_cleaning_task_histories", x => x.id);
                    table.ForeignKey(
                        name: "f_k_cleaning_task_histories_cleaning_tasks__cleaning_task_id",
                        column: x => x.cleaning_task_id,
                        principalTable: "cleaning_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "i_x_cleaning_task_histories__cleaning_task_id__occurred_at",
                table: "cleaning_task_histories",
                columns: new[] { "cleaning_task_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "i_x_cleaning_tasks__assignee_user_id__status",
                table: "cleaning_tasks",
                columns: new[] { "assignee_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "i_x_cleaning_tasks__hotel_id__status",
                table: "cleaning_tasks",
                columns: new[] { "hotel_id", "status" });

            migrationBuilder.CreateIndex(
                name: "i_x_cleaning_tasks__room_id__status",
                table: "cleaning_tasks",
                columns: new[] { "room_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cleaning_task_histories");

            migrationBuilder.DropTable(
                name: "hotel_housekeeping_settings");

            migrationBuilder.DropTable(
                name: "cleaning_tasks");
        }
    }
}
