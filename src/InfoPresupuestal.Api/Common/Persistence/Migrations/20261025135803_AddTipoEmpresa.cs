using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfoPresupuestal.Api.Common.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTipoEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tipo",
                schema: "seguridad",
                table: "empresa",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "CONSTRUCTORA");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tipo",
                schema: "seguridad",
                table: "empresa");
        }
    }
}
