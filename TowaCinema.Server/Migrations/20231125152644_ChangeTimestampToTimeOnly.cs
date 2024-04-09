using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TowaCinema.Server.Migrations
{
    /// <inheritdoc />
    public partial class ChangeTimestampToTimeOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<TimeOnly>(
                name: "GameTimestamp",
                table: "StreamGame",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "GameTimestamp",
                table: "StreamGame",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(TimeOnly),
                oldType: "TEXT");
        }
    }
}
