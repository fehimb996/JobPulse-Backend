using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPosts.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedUserFavoriteJobPostDeleteBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserFavoriteJobs_JobPosts",
                table: "UserFavoriteJobs");

            migrationBuilder.AddForeignKey(
                name: "FK_UserFavoriteJobs_JobPosts",
                table: "UserFavoriteJobs",
                column: "JobPostId",
                principalTable: "JobPosts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserFavoriteJobs_JobPosts",
                table: "UserFavoriteJobs");

            migrationBuilder.AddForeignKey(
                name: "FK_UserFavoriteJobs_JobPosts",
                table: "UserFavoriteJobs",
                column: "JobPostId",
                principalTable: "JobPosts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
