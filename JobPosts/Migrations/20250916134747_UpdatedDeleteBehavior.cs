using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPosts.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedDeleteBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Countries_CountryId",
                table: "Companies");

            migrationBuilder.DropForeignKey(
                name: "FK_JobPostLanguages_Languages_LanguageId",
                table: "JobPostLanguages");

            migrationBuilder.DropForeignKey(
                name: "FK_JobPosts_Countries_CountryId",
                table: "JobPosts");

            migrationBuilder.DropForeignKey(
                name: "FK_JobPostSkills_Skills_SkillId",
                table: "JobPostSkills");

            migrationBuilder.DropForeignKey(
                name: "FK_Locations_Countries_CountryId",
                table: "Locations");

            migrationBuilder.DropForeignKey(
                name: "FK_UserFavoriteJobs_Users",
                table: "UserFavoriteJobs");

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Countries_CountryId",
                table: "Companies",
                column: "CountryId",
                principalTable: "Countries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_JobPostLanguages_Languages_LanguageId",
                table: "JobPostLanguages",
                column: "LanguageId",
                principalTable: "Languages",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_JobPosts_Countries_CountryId",
                table: "JobPosts",
                column: "CountryId",
                principalTable: "Countries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_JobPostSkills_Skills_SkillId",
                table: "JobPostSkills",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_Countries_CountryId",
                table: "Locations",
                column: "CountryId",
                principalTable: "Countries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserFavoriteJobs_Users",
                table: "UserFavoriteJobs",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Countries_CountryId",
                table: "Companies");

            migrationBuilder.DropForeignKey(
                name: "FK_JobPostLanguages_Languages_LanguageId",
                table: "JobPostLanguages");

            migrationBuilder.DropForeignKey(
                name: "FK_JobPosts_Countries_CountryId",
                table: "JobPosts");

            migrationBuilder.DropForeignKey(
                name: "FK_JobPostSkills_Skills_SkillId",
                table: "JobPostSkills");

            migrationBuilder.DropForeignKey(
                name: "FK_Locations_Countries_CountryId",
                table: "Locations");

            migrationBuilder.DropForeignKey(
                name: "FK_UserFavoriteJobs_Users",
                table: "UserFavoriteJobs");

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Countries_CountryId",
                table: "Companies",
                column: "CountryId",
                principalTable: "Countries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JobPostLanguages_Languages_LanguageId",
                table: "JobPostLanguages",
                column: "LanguageId",
                principalTable: "Languages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JobPosts_Countries_CountryId",
                table: "JobPosts",
                column: "CountryId",
                principalTable: "Countries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JobPostSkills_Skills_SkillId",
                table: "JobPostSkills",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_Countries_CountryId",
                table: "Locations",
                column: "CountryId",
                principalTable: "Countries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserFavoriteJobs_Users",
                table: "UserFavoriteJobs",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
