using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PromoRadar.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardenSecurityAndTrackingPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AlertTrigger",
                table: "UserTrackedProducts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "DailySummaryEnabled",
                table: "UserTrackedProducts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EmailAlertsEnabled",
                table: "UserTrackedProducts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaximumPrice",
                table: "UserTrackedProducts",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PushNotificationsEnabled",
                table: "UserTrackedProducts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedCategory",
                table: "Products",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "Products",
                type: "character varying(180)",
                maxLength: 180,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "Products"
                SET "NormalizedName" = UPPER(TRIM("Name")),
                    "NormalizedCategory" = UPPER(TRIM("Category"))
                WHERE "NormalizedName" = '' OR "NormalizedCategory" = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Products_NormalizedName_NormalizedCategory",
                table: "Products",
                columns: new[] { "NormalizedName", "NormalizedCategory" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_NormalizedName_NormalizedCategory",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AlertTrigger",
                table: "UserTrackedProducts");

            migrationBuilder.DropColumn(
                name: "DailySummaryEnabled",
                table: "UserTrackedProducts");

            migrationBuilder.DropColumn(
                name: "EmailAlertsEnabled",
                table: "UserTrackedProducts");

            migrationBuilder.DropColumn(
                name: "MaximumPrice",
                table: "UserTrackedProducts");

            migrationBuilder.DropColumn(
                name: "PushNotificationsEnabled",
                table: "UserTrackedProducts");

            migrationBuilder.DropColumn(
                name: "NormalizedCategory",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "Products");
        }
    }
}
