using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPosts.Migrations
{
    /// <inheritdoc />
    public partial class CareerjetClassUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Company",
                table: "Careerjet");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Careerjet");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Careerjet");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Careerjet",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Salary",
                table: "Careerjet",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Careerjet",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CountryId",
                table: "Careerjet",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LocationId",
                table: "Careerjet",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SalaryMax",
                table: "Careerjet",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SalaryMin",
                table: "Careerjet",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleNormalized",
                table: "Careerjet",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

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
                onDelete: ReferentialAction.SetNull);
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
                name: "IX_Careerjet_CompanyId",
                table: "Careerjet");

            migrationBuilder.DropIndex(
                name: "IX_Careerjet_CountryId",
                table: "Careerjet");

            migrationBuilder.DropIndex(
                name: "IX_Careerjet_LocationId",
                table: "Careerjet");

            migrationBuilder.DropIndex(
                name: "IX_Careerjet_TitleNormalized",
                table: "Careerjet");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Careerjet");

            migrationBuilder.DropColumn(
                name: "CountryId",
                table: "Careerjet");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "Careerjet");

            migrationBuilder.DropColumn(
                name: "SalaryMax",
                table: "Careerjet");

            migrationBuilder.DropColumn(
                name: "SalaryMin",
                table: "Careerjet");

            migrationBuilder.DropColumn(
                name: "TitleNormalized",
                table: "Careerjet");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Careerjet",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "Salary",
                table: "Careerjet",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Company",
                table: "Careerjet",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Careerjet",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Careerjet",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
