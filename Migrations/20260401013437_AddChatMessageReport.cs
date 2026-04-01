using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BookSharingApp.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessageReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chat_message_report",
                columns: table => new
                {
                    report_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    message_id = table.Column<int>(type: "integer", nullable: false),
                    reporter_id = table.Column<string>(type: "text", nullable: false),
                    reported_user_id = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    reported_content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_message_report", x => x.report_id);
                    table.ForeignKey(
                        name: "FK_chat_message_report_AspNetUsers_reported_user_id",
                        column: x => x.reported_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_chat_message_report_AspNetUsers_reporter_id",
                        column: x => x.reporter_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_chat_message_report_chat_message_message_id",
                        column: x => x.message_id,
                        principalTable: "chat_message",
                        principalColumn: "message_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessageReport_MessageId_ReporterId_Unique",
                table: "chat_message_report",
                columns: new[] { "message_id", "reporter_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chat_message_report_reported_user_id",
                table: "chat_message_report",
                column: "reported_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_message_report_reporter_id",
                table: "chat_message_report",
                column: "reporter_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_message_report");
        }
    }
}
