using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaycareAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityCommentsAndActivityPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ActivityId",
                table: "Photos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActivityComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ActivityId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParentCommentId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityComments_ActivityComments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalTable: "ActivityComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityComments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityComments_DailyActivities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "DailyActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_ActivityId",
                table: "Photos",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityComments_ActivityId_CreatedAt",
                table: "ActivityComments",
                columns: new[] { "ActivityId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityComments_ParentCommentId",
                table: "ActivityComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityComments_UserId",
                table: "ActivityComments",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Photos_DailyActivities_ActivityId",
                table: "Photos",
                column: "ActivityId",
                principalTable: "DailyActivities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Photos_DailyActivities_ActivityId",
                table: "Photos");

            migrationBuilder.DropTable(
                name: "ActivityComments");

            migrationBuilder.DropIndex(
                name: "IX_Photos_ActivityId",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "ActivityId",
                table: "Photos");
        }
    }
}
