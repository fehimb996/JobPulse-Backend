using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPosts.Migrations
{
    /// <inheritdoc />
    public partial class AddCompositeHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Careerjet_Companies_CompanyId",
                table: "Careerjet");

            migrationBuilder.DropForeignKey(
                name: "FK_Careerjet_Countries_CountryId",
                table: "Careerjet");

            migrationBuilder.DropForeignKey(
                name: "FK_Careerjet_Locations_LocationId",
                table: "Careerjet");

            migrationBuilder.DropIndex(
                name: "IX_Careerjet_TitleNormalized",
                table: "Careerjet");

            migrationBuilder.AddColumn<string>(
                name: "CompositeHash",
                table: "JobPosts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobPosts_CompositeHash_DataSource_CountryId",
                table: "JobPosts",
                columns: new[] { "CompositeHash", "DataSource", "CountryId" });

            migrationBuilder.CreateIndex(
                name: "IX_JobPosts_DataSource_CountryId_Created",
                table: "JobPosts",
                columns: new[] { "DataSource", "CountryId", "Created" });

            migrationBuilder.CreateIndex(
                name: "UQ_JobPosts_CompositeHash_DataSource",
                table: "JobPosts",
                columns: new[] { "CompositeHash", "DataSource" },
                unique: true,
                filter: "[CompositeHash] IS NOT NULL AND [DataSource] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Careerjet_Companies_CompanyId",
                table: "Careerjet",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Careerjet_Countries_CountryId",
                table: "Careerjet",
                column: "CountryId",
                principalTable: "Countries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Careerjet_Locations_LocationId",
                table: "Careerjet",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Careerjet_Companies_CompanyId",
                table: "Careerjet");

            migrationBuilder.DropForeignKey(
                name: "FK_Careerjet_Countries_CountryId",
                table: "Careerjet");

            migrationBuilder.DropForeignKey(
                name: "FK_Careerjet_Locations_LocationId",
                table: "Careerjet");

            migrationBuilder.DropIndex(
                name: "IX_JobPosts_CompositeHash_DataSource_CountryId",
                table: "JobPosts");

            migrationBuilder.DropIndex(
                name: "IX_JobPosts_DataSource_CountryId_Created",
                table: "JobPosts");

            migrationBuilder.DropIndex(
                name: "UQ_JobPosts_CompositeHash_DataSource",
                table: "JobPosts");

            migrationBuilder.DropColumn(
                name: "CompositeHash",
                table: "JobPosts");

            migrationBuilder.CreateIndex(
                name: "IX_Careerjet_TitleNormalized",
                table: "Careerjet",
                column: "TitleNormalized");

            migrationBuilder.AddForeignKey(
                name: "FK_Careerjet_Companies_CompanyId",
                table: "Careerjet",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Careerjet_Countries_CountryId",
                table: "Careerjet",
                column: "CountryId",
                principalTable: "Countries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Careerjet_Locations_LocationId",
                table: "Careerjet",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
