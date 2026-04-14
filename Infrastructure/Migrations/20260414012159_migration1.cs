using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class migration1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScriptExecutionTimeoutMinutes = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Destinos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    TipoDeDestino = table.Column<string>(type: "TEXT", nullable: false),
                    Credenciales = table.Column<string>(type: "TEXT", nullable: false),
                    IdCarpeta = table.Column<string>(type: "TEXT", nullable: false),
                    AccessKeyId = table.Column<string>(type: "TEXT", nullable: false),
                    SecretAccessKey = table.Column<string>(type: "TEXT", nullable: false),
                    BucketName = table.Column<string>(type: "TEXT", nullable: false),
                    S3Region = table.Column<string>(type: "TEXT", nullable: false),
                    GoogleServiceAccountEmail = table.Column<string>(type: "TEXT", nullable: false),
                    GooglePrivateKey = table.Column<string>(type: "TEXT", nullable: false),
                    AzureBlobContainerName = table.Column<string>(type: "TEXT", nullable: false),
                    AzureBlobConnectionString = table.Column<string>(type: "TEXT", nullable: false),
                    CarpetaDestino = table.Column<string>(type: "TEXT", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Destinos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoryBackupExecutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TrabajoId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    Trigger = table.Column<int>(type: "INTEGER", nullable: false),
                    ArchivosCopiados = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoryBackupExecutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogAccionesUsuario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FechaAccion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ValorAnterior = table.Column<string>(type: "TEXT", nullable: false),
                    ValorNuevo = table.Column<string>(type: "TEXT", nullable: false),
                    Accion = table.Column<string>(type: "TEXT", nullable: false),
                    TablaAfectada = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogAccionesUsuario", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Origenes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    Ruta = table.Column<string>(type: "TEXT", nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", nullable: false),
                    TamanoMaximo = table.Column<string>(type: "TEXT", nullable: false),
                    FiltrosExclusiones = table.Column<string>(type: "TEXT", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Origenes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScriptConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    ScriptPath = table.Column<string>(type: "TEXT", nullable: false),
                    Arguments = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Tipo = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScriptConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    RequirePassword = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrabajosOrigenDestinos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrigenId = table.Column<int>(type: "INTEGER", nullable: false),
                    DestinoId = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrabajosOrigenDestinos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrabajosOrigenDestinos_Destinos_DestinoId",
                        column: x => x.DestinoId,
                        principalTable: "Destinos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrabajosOrigenDestinos_Origenes_OrigenId",
                        column: x => x.OrigenId,
                        principalTable: "Origenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrabajosScripts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScriptPreId = table.Column<int>(type: "INTEGER", nullable: true),
                    PreDetenerEnFallo = table.Column<bool>(type: "INTEGER", nullable: false),
                    ScriptPostId = table.Column<int>(type: "INTEGER", nullable: true),
                    PostDetenerEnFallo = table.Column<bool>(type: "INTEGER", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrabajosScripts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrabajosScripts_ScriptConfigurations_ScriptPostId",
                        column: x => x.ScriptPostId,
                        principalTable: "ScriptConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrabajosScripts_ScriptConfigurations_ScriptPreId",
                        column: x => x.ScriptPreId,
                        principalTable: "ScriptConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Trabajos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", nullable: false),
                    TrabajosOrigenDestinoId = table.Column<int>(type: "INTEGER", nullable: false),
                    TrabajosScriptsId = table.Column<int>(type: "INTEGER", nullable: false),
                    CronExpression = table.Column<string>(type: "TEXT", nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    Procesando = table.Column<bool>(type: "INTEGER", nullable: false),
                    EstatusPrevio = table.Column<string>(type: "TEXT", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trabajos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trabajos_TrabajosOrigenDestinos_TrabajosOrigenDestinoId",
                        column: x => x.TrabajosOrigenDestinoId,
                        principalTable: "TrabajosOrigenDestinos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trabajos_TrabajosScripts_TrabajosScriptsId",
                        column: x => x.TrabajosScriptsId,
                        principalTable: "TrabajosScripts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Destinos_Nombre",
                table: "Destinos",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Origenes_Nombre",
                table: "Origenes",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trabajos_TrabajosOrigenDestinoId",
                table: "Trabajos",
                column: "TrabajosOrigenDestinoId");

            migrationBuilder.CreateIndex(
                name: "IX_Trabajos_TrabajosScriptsId",
                table: "Trabajos",
                column: "TrabajosScriptsId");

            migrationBuilder.CreateIndex(
                name: "IX_TrabajosOrigenDestinos_DestinoId",
                table: "TrabajosOrigenDestinos",
                column: "DestinoId");

            migrationBuilder.CreateIndex(
                name: "IX_TrabajosOrigenDestinos_OrigenId_DestinoId",
                table: "TrabajosOrigenDestinos",
                columns: new[] { "OrigenId", "DestinoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrabajosScripts_ScriptPostId",
                table: "TrabajosScripts",
                column: "ScriptPostId");

            migrationBuilder.CreateIndex(
                name: "IX_TrabajosScripts_ScriptPreId",
                table: "TrabajosScripts",
                column: "ScriptPreId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationSettings");

            migrationBuilder.DropTable(
                name: "HistoryBackupExecutions");

            migrationBuilder.DropTable(
                name: "LogAccionesUsuario");

            migrationBuilder.DropTable(
                name: "Trabajos");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "TrabajosOrigenDestinos");

            migrationBuilder.DropTable(
                name: "TrabajosScripts");

            migrationBuilder.DropTable(
                name: "Destinos");

            migrationBuilder.DropTable(
                name: "Origenes");

            migrationBuilder.DropTable(
                name: "ScriptConfigurations");
        }
    }
}
