using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPosts.Migrations
{
    /// <inheritdoc />
    public partial class RemovedUrlHashFromJobPostClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JobPost_UrlHash_Unique",
                table: "JobPosts");

            migrationBuilder.DropColumn(
                name: "UrlHash",
                table: "JobPosts");

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "JobPosts",
                type: "nvarchar(800)",
                maxLength: 800,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.CreateIndex(
                name: "IX_JobPost_Url_External_Unique",
                table: "JobPosts",
                column: "Url",
                unique: true,
                filter: "[DataSource] IS NOT NULL AND [Url] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JobPost_Url_External_Unique",
                table: "JobPosts");

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "JobPosts",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(800)",
                oldMaxLength: 800,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UrlHash",
                table: "JobPosts",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobPost_UrlHash_Unique",
                table: "JobPosts",
                column: "UrlHash",
                unique: true,
                filter: "[UrlHash] IS NOT NULL");
        }
    }
}
