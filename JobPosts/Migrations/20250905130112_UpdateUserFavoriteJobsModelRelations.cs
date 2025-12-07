using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPosts.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUserFavoriteJobsModelRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserFavoriteJobs_JobPosts",
                table: "UserFavoriteJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_UserFavoriteJobs_Users",
                table: "UserFavoriteJobs");

            migrationBuilder.DropTable(
                name: "Careerjet");

            migrationBuilder.AlterColumn<long>(
                name: "JobId",
                table: "JobPosts",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.CreateIndex(
                name: "IX_UserFavoriteJobs_UserId_JobPostId",
                table: "UserFavoriteJobs",
                columns: new[] { "UserId", "JobPostId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserFavoriteJobs_JobPosts",
                table: "UserFavoriteJobs",
                column: "JobPostId",
                principalTable: "JobPosts",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserFavoriteJobs_JobPosts",
                table: "UserFavoriteJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_UserFavoriteJobs_Users",
                table: "UserFavoriteJobs");

            migrationBuilder.DropIndex(
                name: "IX_UserFavoriteJobs_UserId_JobPostId",
                table: "UserFavoriteJobs");

            migrationBuilder.AlterColumn<long>(
                name: "JobId",
                table: "JobPosts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Careerjet",
                columns: table => new
                {
                    UrlHash = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: true),
                    CountryId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProcessDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Salary = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SalaryMax = table.Column<double>(type: "float", nullable: true),
                    SalaryMin = table.Column<double>(type: "float", nullable: true),
                    Scrape = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TitleNormalized = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Careerjet", x => x.UrlHash);
                    table.ForeignKey(
                        name: "FK_Careerjet_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Careerjet_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Careerjet_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Careerjet_CompanyId",
                table: "Careerjet",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Careerjet_CountryId",
                table: "Careerjet",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Careerjet_LocationId",
                table: "Careerjet",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserFavoriteJobs_JobPosts",
                table: "UserFavoriteJobs",
                column: "JobPostId",
                principalTable: "JobPosts",
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
