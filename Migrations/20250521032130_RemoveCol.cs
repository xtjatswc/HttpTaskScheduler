using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HttpTaskScheduler.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TokenCommand",
                table: "TaskSchedules");

            migrationBuilder.DropColumn(
                name: "TokenRefreshInterval",
                table: "TaskSchedules");

            migrationBuilder.DropColumn(
                name: "TokenUrl",
                table: "TaskSchedules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TokenCommand",
                table: "TaskSchedules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TokenRefreshInterval",
                table: "TaskSchedules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TokenUrl",
                table: "TaskSchedules",
                type: "TEXT",
                nullable: true);
        }
    }
}
