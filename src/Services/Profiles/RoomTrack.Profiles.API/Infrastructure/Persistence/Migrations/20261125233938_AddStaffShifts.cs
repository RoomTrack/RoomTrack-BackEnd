using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomTrack.Profiles.API.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffShifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shift_swap_requests",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    hotel_id = table.Column<int>(type: "int", nullable: false),
                    requester_user_id = table.Column<int>(type: "int", nullable: false),
                    requester_shift_id = table.Column<int>(type: "int", nullable: false),
                    colleague_user_id = table.Column<int>(type: "int", nullable: false),
                    colleague_shift_id = table.Column<int>(type: "int", nullable: false),
                    reason = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    requested_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    decided_by_user_id = table.Column<int>(type: "int", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_shift_swap_requests", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "work_shifts",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    hotel_id = table.Column<int>(type: "int", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    period = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    area = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    minimum_staff = table.Column<int>(type: "int", nullable: false),
                    created_by_user_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_work_shifts", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "work_shift_assignees",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    work_shift_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    display_name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_work_shift_assignees", x => x.id);
                    table.ForeignKey(
                        name: "f_k_work_shift_assignees_work_shifts__work_shift_id",
                        column: x => x.work_shift_id,
                        principalTable: "work_shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "i_x_shift_swap_requests__hotel_id__status",
                table: "shift_swap_requests",
                columns: new[] { "hotel_id", "status" });

            migrationBuilder.CreateIndex(
                name: "i_x_work_shift_assignees__user_id",
                table: "work_shift_assignees",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "i_x_work_shift_assignees__work_shift_id__user_id",
                table: "work_shift_assignees",
                columns: new[] { "work_shift_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_work_shifts__hotel_id__date__period__area",
                table: "work_shifts",
                columns: new[] { "hotel_id", "date", "period", "area" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shift_swap_requests");

            migrationBuilder.DropTable(
                name: "work_shift_assignees");

            migrationBuilder.DropTable(
                name: "work_shifts");
        }
    }
}
