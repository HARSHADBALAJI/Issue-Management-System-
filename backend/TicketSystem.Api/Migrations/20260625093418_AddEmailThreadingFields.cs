using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketSystem.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailThreadingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InReplyTo",
                table: "TicketMessages",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "References",
                table: "TicketMessages",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InReplyTo",
                table: "EmailMessages",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "References",
                table: "EmailMessages",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InReplyTo",
                table: "TicketMessages");

            migrationBuilder.DropColumn(
                name: "References",
                table: "TicketMessages");

            migrationBuilder.DropColumn(
                name: "InReplyTo",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "References",
                table: "EmailMessages");
        }
    }
}
