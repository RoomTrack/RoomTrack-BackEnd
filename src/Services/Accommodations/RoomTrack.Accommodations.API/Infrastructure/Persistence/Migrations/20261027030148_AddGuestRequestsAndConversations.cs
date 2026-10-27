using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomTrack.Accommodations.API.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestRequestsAndConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conversations",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    hotel_id = table.Column<int>(type: "int", nullable: false),
                    kind = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    key = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    guest_user_id = table.Column<int>(type: "int", nullable: true),
                    area = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    subject = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    subject_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    last_message_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    message_count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_conversations", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "guest_requests",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    hotel_id = table.Column<int>(type: "int", nullable: false),
                    room_id = table.Column<int>(type: "int", nullable: false),
                    booking_id = table.Column<int>(type: "int", nullable: true),
                    guest_user_id = table.Column<int>(type: "int", nullable: true),
                    title = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    area = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_by_user_id = table.Column<int>(type: "int", nullable: false),
                    created_by_guest = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    estimated_attention_by = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    assignee_user_id = table.Column<int>(type: "int", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    closing_note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_guest_requests", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "conversation_messages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    conversation_id = table.Column<int>(type: "int", nullable: false),
                    sender_user_id = table.Column<int>(type: "int", nullable: false),
                    sender_name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sender_is_guest = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    body = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sent_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_conversation_messages", x => x.id);
                    table.ForeignKey(
                        name: "f_k_conversation_messages_conversations__conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "guest_request_histories",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    guest_request_id = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("p_k_guest_request_histories", x => x.id);
                    table.ForeignKey(
                        name: "f_k_guest_request_histories_guest_requests__guest_request_id",
                        column: x => x.guest_request_id,
                        principalTable: "guest_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "i_x_conversation_messages__conversation_id__id",
                table: "conversation_messages",
                columns: new[] { "conversation_id", "id" });

            migrationBuilder.CreateIndex(
                name: "i_x_conversations__guest_user_id",
                table: "conversations",
                column: "guest_user_id");

            migrationBuilder.CreateIndex(
                name: "i_x_conversations__hotel_id__kind__key",
                table: "conversations",
                columns: new[] { "hotel_id", "kind", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_conversations__kind__key",
                table: "conversations",
                columns: new[] { "kind", "key" });

            migrationBuilder.CreateIndex(
                name: "i_x_guest_request_histories__guest_request_id__occurred_at",
                table: "guest_request_histories",
                columns: new[] { "guest_request_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "i_x_guest_requests__guest_user_id",
                table: "guest_requests",
                column: "guest_user_id");

            migrationBuilder.CreateIndex(
                name: "i_x_guest_requests__hotel_id__area__status",
                table: "guest_requests",
                columns: new[] { "hotel_id", "area", "status" });

            migrationBuilder.CreateIndex(
                name: "i_x_guest_requests__hotel_id__status",
                table: "guest_requests",
                columns: new[] { "hotel_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "conversation_messages");

            migrationBuilder.DropTable(
                name: "guest_request_histories");

            migrationBuilder.DropTable(
                name: "conversations");

            migrationBuilder.DropTable(
                name: "guest_requests");
        }
    }
}
