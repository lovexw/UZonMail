﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UZonMail.DB.Migrations.SqLite
{
    /// <inheritdoc />
    public partial class addOrgIdToSystemSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "OrganizationId",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "UserId",
                table: "SystemSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "SystemSettings");
        }
    }
}
