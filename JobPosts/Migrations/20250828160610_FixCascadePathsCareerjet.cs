using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPosts.Migrations
{
    /// <inheritdoc />
    public partial class FixCascadePathsCareerjet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Careerjet_Locations_LocationId",
                table: "Careerjet");

            migrationBuilder.AddForeignKey(
                name: "FK_Careerjet_Locations_LocationId",
                table: "Careerjet",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Careerjet_Locations_LocationId",
                table: "Careerjet");

            migrationBuilder.AddForeignKey(
                name: "FK_Careerjet_Locations_LocationId",
                table: "Careerjet",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
