using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoomTrack.Identity.API.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "audit_entries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    action = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    outcome = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    actor_user_id = table.Column<int>(type: "int", nullable: true),
                    actor_email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    target_user_id = table.Column<int>(type: "int", nullable: true),
                    target_email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    hotel_id = table.Column<int>(type: "int", nullable: true),
                    ip_address = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    details = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_audit_entries", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "data_protection_keys",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    friendly_name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    xml = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_data_protection_keys", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "inbox_states",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    message_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    consumer_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    lock_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    row_version = table.Column<DateTime>(type: "timestamp(6)", rowVersion: true, nullable: true),
                    received = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    receive_count = table.Column<int>(type: "int", nullable: false),
                    expiration_time = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    consumed = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    delivered = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_sequence_number = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_inbox_states", x => x.id);
                    table.UniqueConstraint("a_k_inbox_states__message_id__consumer_id", x => new { x.message_id, x.consumer_id });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "outbox_states",
                columns: table => new
                {
                    outbox_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    lock_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    row_version = table.Column<DateTime>(type: "timestamp(6)", rowVersion: true, nullable: true),
                    created = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    delivered = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_sequence_number = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_outbox_states", x => x.outbox_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    password_hash = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    role = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "Active")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    hotel_id = table.Column<int>(type: "int", nullable: true),
                    chain_id = table.Column<int>(type: "int", nullable: true),
                    token_version = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    session_revocation_reason = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)")
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn),
                    first_name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email_verified = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    email_verified_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    failed_sign_in_attempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    locked_until = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    mfa_enabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    mfa_enabled_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    mfa_secret_protected = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    mfa_pending_secret_protected = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    mfa_last_used_time_step = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_users", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    sequence_number = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    enqueue_time = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    sent_time = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    headers = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    properties = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    inbox_message_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    inbox_consumer_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    outbox_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    message_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    content_type = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    message_type = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    body = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    conversation_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    correlation_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    initiator_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    request_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    source_address = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    destination_address = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    response_address = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fault_address = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    expiration_time = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_outbox_messages", x => x.sequence_number);
                    table.ForeignKey(
                        name: "f_k_outbox_messages__outbox_state__outbox_id",
                        column: x => x.outbox_id,
                        principalTable: "outbox_states",
                        principalColumn: "outbox_id");
                    table.ForeignKey(
                        name: "f_k_outbox_messages_inbox_states__inbox_message_id__inbox_consumer_id",
                        columns: x => new { x.inbox_message_id, x.inbox_consumer_id },
                        principalTable: "inbox_states",
                        principalColumns: new[] { "message_id", "consumer_id" });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "account_tokens",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    purpose = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    token_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    issued_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_account_tokens", x => x.id);
                    table.ForeignKey(
                        name: "f_k_account_tokens_users__user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "mfa_recovery_codes",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    code_hash = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_mfa_recovery_codes", x => x.id);
                    table.ForeignKey(
                        name: "f_k_mfa_recovery_codes_users__user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    family_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    token_version = table.Column<int>(type: "int", nullable: false),
                    token_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    issued_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    revocation_reason = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "f_k_refresh_tokens_users__user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "i_x_account_tokens__token_hash",
                table: "account_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_account_tokens__user_id__purpose",
                table: "account_tokens",
                columns: new[] { "user_id", "purpose" });

            migrationBuilder.CreateIndex(
                name: "i_x_audit_entries__actor_user_id",
                table: "audit_entries",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "i_x_audit_entries__hotel_id__occurred_at",
                table: "audit_entries",
                columns: new[] { "hotel_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "i_x_audit_entries__occurred_at",
                table: "audit_entries",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "i_x_audit_entries__target_user_id",
                table: "audit_entries",
                column: "target_user_id");

            migrationBuilder.CreateIndex(
                name: "i_x_inbox_states__delivered",
                table: "inbox_states",
                column: "delivered");

            migrationBuilder.CreateIndex(
                name: "i_x_mfa_recovery_codes__user_id",
                table: "mfa_recovery_codes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "i_x_outbox_messages__enqueue_time",
                table: "outbox_messages",
                column: "enqueue_time");

            migrationBuilder.CreateIndex(
                name: "i_x_outbox_messages__expiration_time",
                table: "outbox_messages",
                column: "expiration_time");

            migrationBuilder.CreateIndex(
                name: "i_x_outbox_messages__inbox_message_id__inbox_consumer_id__sequence_number",
                table: "outbox_messages",
                columns: new[] { "inbox_message_id", "inbox_consumer_id", "sequence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_outbox_messages__outbox_id__sequence_number",
                table: "outbox_messages",
                columns: new[] { "outbox_id", "sequence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_outbox_states__created",
                table: "outbox_states",
                column: "created");

            migrationBuilder.CreateIndex(
                name: "i_x_refresh_tokens__family_id",
                table: "refresh_tokens",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "i_x_refresh_tokens__token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_refresh_tokens__user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "i_x_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_users_status",
                table: "users",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_tokens");

            migrationBuilder.DropTable(
                name: "audit_entries");

            migrationBuilder.DropTable(
                name: "data_protection_keys");

            migrationBuilder.DropTable(
                name: "mfa_recovery_codes");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "outbox_states");

            migrationBuilder.DropTable(
                name: "inbox_states");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
