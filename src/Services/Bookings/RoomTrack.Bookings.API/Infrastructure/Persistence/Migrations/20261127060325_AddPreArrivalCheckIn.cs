using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomTrack.Bookings.API.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPreArrivalCheckIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "completed_at",
                table: "digital_check_ins",
                type: "datetime(6)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetime(6)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "access_code_valid_until",
                table: "digital_check_ins",
                type: "datetime(6)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetime(6)");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "submitted_at",
                table: "digital_check_ins",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            // Every existing check-in was approved at once: it was sent when it was completed.
            migrationBuilder.Sql("UPDATE digital_check_ins SET submitted_at = completed_at;");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "pre_arrival_check_in_at",
                table: "bookings",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "submitted_at",
                table: "digital_check_ins");

            migrationBuilder.DropColumn(
                name: "pre_arrival_check_in_at",
                table: "bookings");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "completed_at",
                table: "digital_check_ins",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetime(6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "access_code_valid_until",
                table: "digital_check_ins",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetime(6)",
                oldNullable: true);
        }
    }
}
