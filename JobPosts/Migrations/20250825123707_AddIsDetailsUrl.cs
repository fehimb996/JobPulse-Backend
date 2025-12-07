using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPosts.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDetailsUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDetailsUrl",
                table: "JobPosts",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDetailsUrl",
                table: "JobPosts");
        }
    }
}
