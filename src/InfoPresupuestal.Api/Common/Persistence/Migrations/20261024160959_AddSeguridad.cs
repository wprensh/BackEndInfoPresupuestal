using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfoPresupuestal.Api.Common.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeguridad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "seguridad");

            migrationBuilder.CreateTable(
                name: "empresa",
                schema: "seguridad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    razon_social = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    telefono = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_empresa", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rol",
                schema: "seguridad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    nombre = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    es_sistema = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rol", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rol_permiso",
                schema: "seguridad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    modulo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ver = table.Column<bool>(type: "boolean", nullable: false),
                    insertar = table.Column<bool>(type: "boolean", nullable: false),
                    editar = table.Column<bool>(type: "boolean", nullable: false),
                    eliminar = table.Column<bool>(type: "boolean", nullable: false),
                    rol_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rol_permiso", x => x.id);
                    table.ForeignKey(
                        name: "FK_rol_permiso_rol_rol_id",
                        column: x => x.rol_id,
                        principalSchema: "seguridad",
                        principalTable: "rol",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuario",
                schema: "seguridad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    rol_id = table.Column<Guid>(type: "uuid", nullable: false),
                    es_super_admin = table.Column<bool>(type: "boolean", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    ultimo_acceso = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario", x => x.id);
                    table.ForeignKey(
                        name: "FK_usuario_rol_rol_id",
                        column: x => x.rol_id,
                        principalSchema: "seguridad",
                        principalTable: "rol",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_empresa_tenant_id",
                schema: "seguridad",
                table: "empresa",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rol_tenant_id",
                schema: "seguridad",
                table: "rol",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_rol_tenant_id_nombre",
                schema: "seguridad",
                table: "rol",
                columns: new[] { "tenant_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rol_permiso_rol_id_modulo",
                schema: "seguridad",
                table: "rol_permiso",
                columns: new[] { "rol_id", "modulo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuario_rol_id",
                schema: "seguridad",
                table: "usuario",
                column: "rol_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_tenant_id",
                schema: "seguridad",
                table: "usuario",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_tenant_id_email",
                schema: "seguridad",
                table: "usuario",
                columns: new[] { "tenant_id", "email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "empresa",
                schema: "seguridad");

            migrationBuilder.DropTable(
                name: "rol_permiso",
                schema: "seguridad");

            migrationBuilder.DropTable(
                name: "usuario",
                schema: "seguridad");

            migrationBuilder.DropTable(
                name: "rol",
                schema: "seguridad");
        }
    }
}
