using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomTrack.Accommodations.API.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChecklistsPreventiveMaintenanceAndShiftHandovers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_recurring",
                table: "incidents",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "incidents",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Corrective")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "preventive_plan_id",
                table: "incidents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "recurrence_count",
                table: "incidents",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "action",
                table: "incident_histories",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "cleaning_checklist_templates",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    hotel_id = table.Column<int>(type: "int", nullable: false),
                    room_type_id = table.Column<int>(type: "int", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    updated_by_user_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_cleaning_checklist_templates", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "cleaning_task_checklist_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    cleaning_task_id = table.Column<int>(type: "int", nullable: false),
                    position = table.Column<int>(type: "int", nullable: false),
                    label = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_required = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    is_done = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    done_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    done_by_user_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_cleaning_task_checklist_items", x => x.id);
                    table.ForeignKey(
                        name: "f_k_cleaning_task_checklist_items_cleaning_tasks__cleaning_task_id",
                        column: x => x.cleaning_task_id,
                        principalTable: "cleaning_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "preventive_maintenance_plans",
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
                    frequency_days = table.Column<int>(type: "int", nullable: false),
                    next_due_on = table.Column<DateOnly>(type: "date", nullable: false),
                    reminder_days_before = table.Column<int>(type: "int", nullable: false),
                    reminder_sent_for_due_on = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_by_user_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    last_generated_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    last_incident_id = table.Column<int>(type: "int", nullable: true),
                    generated_count = table.Column<int>(type: "int", nullable: false),
                    deactivated_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_preventive_maintenance_plans", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "shift_handovers",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    hotel_id = table.Column<int>(type: "int", nullable: false),
                    area = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    author_user_id = table.Column<int>(type: "int", nullable: false),
                    author_email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_shift_handovers", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "cleaning_checklist_template_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    cleaning_checklist_template_id = table.Column<int>(type: "int", nullable: false),
                    position = table.Column<int>(type: "int", nullable: false),
                    label = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_required = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_cleaning_checklist_template_items", x => x.id);
                    table.ForeignKey(
                        name: "f_k_cleaning_checklist_template_items_cleaning_checklist_templat~",
                        column: x => x.cleaning_checklist_template_id,
                        principalTable: "cleaning_checklist_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "shift_handover_acknowledgements",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    shift_handover_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_shift_handover_acknowledgements", x => x.id);
                    table.ForeignKey(
                        name: "f_k_shift_handover_acknowledgements_shift_handovers__shift_handove~",
                        column: x => x.shift_handover_id,
                        principalTable: "shift_handovers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "shift_handover_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    shift_handover_id = table.Column<int>(type: "int", nullable: false),
                    kind = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reference_id = table.Column<int>(type: "int", nullable: false),
                    room_id = table.Column<int>(type: "int", nullable: false),
                    summary = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    priority = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    assignee_user_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_shift_handover_items", x => x.id);
                    table.ForeignKey(
                        name: "f_k_shift_handover_items_shift_handovers__shift_handover_id",
                        column: x => x.shift_handover_id,
                        principalTable: "shift_handovers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "i_x_incidents__preventive_plan_id",
                table: "incidents",
                column: "preventive_plan_id");

            migrationBuilder.CreateIndex(
                name: "i_x_incidents__room_id__reported_at",
                table: "incidents",
                columns: new[] { "room_id", "reported_at" });

            migrationBuilder.CreateIndex(
                name: "i_x_cleaning_checklist_template_items__cleaning_checklist_template_id",
                table: "cleaning_checklist_template_items",
                column: "cleaning_checklist_template_id");

            migrationBuilder.CreateIndex(
                name: "i_x_cleaning_checklist_templates__hotel_id__room_type_id",
                table: "cleaning_checklist_templates",
                columns: new[] { "hotel_id", "room_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_cleaning_task_checklist_items__cleaning_task_id__position",
                table: "cleaning_task_checklist_items",
                columns: new[] { "cleaning_task_id", "position" });

            migrationBuilder.CreateIndex(
                name: "i_x_preventive_maintenance_plans__hotel_id__is_active",
                table: "preventive_maintenance_plans",
                columns: new[] { "hotel_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "i_x_preventive_maintenance_plans__is_active__next_due_on",
                table: "preventive_maintenance_plans",
                columns: new[] { "is_active", "next_due_on" });

            migrationBuilder.CreateIndex(
                name: "i_x_preventive_maintenance_plans__room_id",
                table: "preventive_maintenance_plans",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "i_x_shift_handover_acknowledgements__shift_handover_id__user_id",
                table: "shift_handover_acknowledgements",
                columns: new[] { "shift_handover_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_shift_handover_items__shift_handover_id",
                table: "shift_handover_items",
                column: "shift_handover_id");

            migrationBuilder.CreateIndex(
                name: "i_x_shift_handovers__hotel_id__area__created_at",
                table: "shift_handovers",
                columns: new[] { "hotel_id", "area", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cleaning_checklist_template_items");

            migrationBuilder.DropTable(
                name: "cleaning_task_checklist_items");

            migrationBuilder.DropTable(
                name: "preventive_maintenance_plans");

            migrationBuilder.DropTable(
                name: "shift_handover_acknowledgements");

            migrationBuilder.DropTable(
                name: "shift_handover_items");

            migrationBuilder.DropTable(
                name: "cleaning_checklist_templates");

            migrationBuilder.DropTable(
                name: "shift_handovers");

            migrationBuilder.DropIndex(
                name: "i_x_incidents__preventive_plan_id",
                table: "incidents");

            migrationBuilder.DropIndex(
                name: "i_x_incidents__room_id__reported_at",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "is_recurring",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "preventive_plan_id",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "recurrence_count",
                table: "incidents");

            migrationBuilder.AlterColumn<string>(
                name: "action",
                table: "incident_histories",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(30)",
                oldMaxLength: 30)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
