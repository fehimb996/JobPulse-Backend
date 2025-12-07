using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPosts.Migrations
{
    /// <inheritdoc />
    public partial class AddCareerjetPropertiesToJobPostClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DataSource",
                table: "JobPosts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Salary",
                table: "JobPosts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UrlHash",
                table: "JobPosts",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobPost_DataSource",
                table: "JobPosts",
                column: "DataSource");

            migrationBuilder.CreateIndex(
                name: "IX_JobPost_DataSource_CountryId",
                table: "JobPosts",
                columns: new[] { "DataSource", "CountryId" });

            migrationBuilder.CreateIndex(
                name: "IX_JobPost_UrlHash_Unique",
                table: "JobPosts",
                column: "UrlHash",
                unique: true,
                filter: "[UrlHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JobPost_DataSource",
                table: "JobPosts");

            migrationBuilder.DropIndex(
                name: "IX_JobPost_DataSource_CountryId",
                table: "JobPosts");

            migrationBuilder.DropIndex(
                name: "IX_JobPost_UrlHash_Unique",
                table: "JobPosts");

            migrationBuilder.DropColumn(
                name: "DataSource",
                table: "JobPosts");

            migrationBuilder.DropColumn(
                name: "Salary",
                table: "JobPosts");

            migrationBuilder.DropColumn(
                name: "UrlHash",
                table: "JobPosts");
        }
    }
}
