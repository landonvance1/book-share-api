using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookSharingApp.Migrations
{
    /// <inheritdoc />
    public partial class AddIsResolvedToChatMessageReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_resolved",
                table: "chat_message_report",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_resolved",
                table: "chat_message_report");
        }
    }
}
