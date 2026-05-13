using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusPulse.Migrations
{
    /// <inheritdoc />
    public partial class SyncInvestigatorEmailAllowlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvestigatorEmails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestigatorEmails", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvestigatorEmails_NormalizedEmail",
                table: "InvestigatorEmails",
                column: "NormalizedEmail",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvestigatorEmails");
        }
    }
}
