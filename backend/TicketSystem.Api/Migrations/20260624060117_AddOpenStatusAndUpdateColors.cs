using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketSystem.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOpenStatusAndUpdateColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // add Open status (ID 5)
            migrationBuilder.Sql(@"
                SET IDENTITY_INSERT TicketStatuses ON;
                IF NOT EXISTS (SELECT 1 FROM TicketStatuses WHERE Id = 5)
                    INSERT INTO TicketStatuses (Id, Name, DisplayName, Color, IsActive, SortOrder, CreatedAt)
                    VALUES (5, 'open', 'Open', '#0D6EFD', 1, 0, GETUTCDATE());
                SET IDENTITY_INSERT TicketStatuses OFF;
            ");

            // update existing status colors
            migrationBuilder.Sql("UPDATE TicketStatuses SET Color = '#FFC107', SortOrder = 1 WHERE Id = 1");
            migrationBuilder.Sql("UPDATE TicketStatuses SET Color = '#FD7E14', SortOrder = 2 WHERE Id = 2");
            migrationBuilder.Sql("UPDATE TicketStatuses SET Color = '#198754', SortOrder = 3 WHERE Id = 3");
            migrationBuilder.Sql("UPDATE TicketStatuses SET Color = '#6C757D', SortOrder = 4 WHERE Id = 4");

            // change default StatusId on Tickets from 1 (in_progress) to 5 (open)
            migrationBuilder.AlterColumn<int>(
                name: "StatusId",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 5,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "StatusId",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 5);

            // undo color changes and remove open status
            migrationBuilder.Sql("UPDATE TicketStatuses SET Color = '#2563eb', SortOrder = 1 WHERE Id = 1");
            migrationBuilder.Sql("UPDATE TicketStatuses SET Color = '#f59e0b', SortOrder = 2 WHERE Id = 2");
            migrationBuilder.Sql("UPDATE TicketStatuses SET Color = '#10b981', SortOrder = 3 WHERE Id = 3");
            migrationBuilder.Sql("UPDATE TicketStatuses SET Color = '#6b7280', SortOrder = 4 WHERE Id = 4");
            migrationBuilder.Sql("DELETE FROM TicketStatuses WHERE Id = 5");
        }
    }
}
