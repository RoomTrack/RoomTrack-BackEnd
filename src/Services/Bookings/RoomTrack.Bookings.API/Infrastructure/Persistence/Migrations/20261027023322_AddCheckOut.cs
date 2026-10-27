using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomTrack.Bookings.API.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckOut : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "checked_out_at",
                table: "bookings",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "checked_out_by_user_id",
                table: "bookings",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "checked_out_at",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "checked_out_by_user_id",
                table: "bookings");
        }
    }
}
