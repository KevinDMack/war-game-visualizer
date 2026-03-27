using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WargameData.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Scenarios",
                columns: table => new
                {
                    ScenarioId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    ScenarioName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    BoundingBox_MinLatitude = table.Column<double>(type: "float", nullable: true),
                    BoundingBox_MinLongitude = table.Column<double>(type: "float", nullable: true),
                    BoundingBox_MaxLatitude = table.Column<double>(type: "float", nullable: true),
                    BoundingBox_MaxLongitude = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scenarios", x => x.ScenarioId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Scenarios");
        }
    }
}
