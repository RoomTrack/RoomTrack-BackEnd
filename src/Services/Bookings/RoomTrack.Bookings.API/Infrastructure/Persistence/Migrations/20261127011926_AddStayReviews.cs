using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomTrack.Bookings.API.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStayReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stay_reviews",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    booking_id = table.Column<int>(type: "int", nullable: false),
                    hotel_id = table.Column<int>(type: "int", nullable: false),
                    room_id = table.Column<int>(type: "int", nullable: false),
                    guest_user_id = table.Column<int>(type: "int", nullable: false),
                    overall_rating = table.Column<int>(type: "int", nullable: false),
                    front_desk_rating = table.Column<int>(type: "int", nullable: true),
                    housekeeping_rating = table.Column<int>(type: "int", nullable: true),
                    maintenance_rating = table.Column<int>(type: "int", nullable: true),
                    comment = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    submitted_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_stay_reviews", x => x.id);
                    table.ForeignKey(
                        name: "f_k_stay_reviews_bookings__booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "i_x_stay_reviews__booking_id",
                table: "stay_reviews",
                column: "booking_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_stay_reviews__hotel_id__submitted_at",
                table: "stay_reviews",
                columns: new[] { "hotel_id", "submitted_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stay_reviews");
        }
    }
}
