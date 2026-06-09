using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace jira.Migrations
{
    /// <inheritdoc />
    public partial class AddOAuthFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UZYTKOWNICY",
                columns: table => new
                {
                    id_uzytkownika = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    nazwa_uzytkownika = table.Column<string>(type: "text", nullable: false),
                    avatar_url = table.Column<string>(type: "text", nullable: true),
                    github_id = table.Column<string>(type: "text", nullable: true),
                    google_id = table.Column<string>(type: "text", nullable: true),
                    github_refresh_token_encrypted = table.Column<string>(type: "text", nullable: true),
                    google_refresh_token_encrypted = table.Column<string>(type: "text", nullable: true),
                    data_rejestracji = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UZYTKOWNICY", x => x.id_uzytkownika);
                });

            migrationBuilder.CreateTable(
                name: "TABLICE",
                columns: table => new
                {
                    id_tablicy = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nazwa_tablicy = table.Column<string>(type: "text", nullable: false),
                    opis_tablicy = table.Column<string>(type: "text", nullable: true),
                    id_uzytkownika_owner = table.Column<int>(type: "integer", nullable: false),
                    data_stworzenia = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    kolor_tablicy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TABLICE", x => x.id_tablicy);
                    table.ForeignKey(
                        name: "FK_TABLICE_UZYTKOWNICY_id_uzytkownika_owner",
                        column: x => x.id_uzytkownika_owner,
                        principalTable: "UZYTKOWNICY",
                        principalColumn: "id_uzytkownika",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TABLICE_UZYTKOWNICY",
                columns: table => new
                {
                    id_uzytkownika = table.Column<int>(type: "integer", nullable: false),
                    id_tablicy = table.Column<int>(type: "integer", nullable: false),
                    rola = table.Column<string>(type: "text", nullable: false),
                    data_dolaczenia = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TABLICE_UZYTKOWNICY", x => new { x.id_uzytkownika, x.id_tablicy });
                    table.ForeignKey(
                        name: "FK_TABLICE_UZYTKOWNICY_TABLICE_id_tablicy",
                        column: x => x.id_tablicy,
                        principalTable: "TABLICE",
                        principalColumn: "id_tablicy",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TABLICE_UZYTKOWNICY_UZYTKOWNICY_id_uzytkownika",
                        column: x => x.id_uzytkownika,
                        principalTable: "UZYTKOWNICY",
                        principalColumn: "id_uzytkownika",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ZADANIA",
                columns: table => new
                {
                    id_zadania = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_tablicy = table.Column<int>(type: "integer", nullable: false),
                    tytul_zadania = table.Column<string>(type: "text", nullable: false),
                    opis_zadania = table.Column<string>(type: "text", nullable: true),
                    data_stworzenia = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    id_uzytkownika_przypisanego = table.Column<int>(type: "integer", nullable: true),
                    id_uzytkownika_tworcy_zadania = table.Column<int>(type: "integer", nullable: false),
                    priorytet = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    data_zakonczenia = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    kolumna_tablicy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZADANIA", x => x.id_zadania);
                    table.ForeignKey(
                        name: "FK_ZADANIA_TABLICE_id_tablicy",
                        column: x => x.id_tablicy,
                        principalTable: "TABLICE",
                        principalColumn: "id_tablicy",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ZADANIA_UZYTKOWNICY_id_uzytkownika_przypisanego",
                        column: x => x.id_uzytkownika_przypisanego,
                        principalTable: "UZYTKOWNICY",
                        principalColumn: "id_uzytkownika",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ZADANIA_UZYTKOWNICY_id_uzytkownika_tworcy_zadania",
                        column: x => x.id_uzytkownika_tworcy_zadania,
                        principalTable: "UZYTKOWNICY",
                        principalColumn: "id_uzytkownika",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KOMENTARZE",
                columns: table => new
                {
                    id_komentarza = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_zadania = table.Column<int>(type: "integer", nullable: false),
                    tresc_komentarza = table.Column<string>(type: "text", nullable: false),
                    id_uzytkownika = table.Column<int>(type: "integer", nullable: false),
                    data_utworzenia = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    data_edycji = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KOMENTARZE", x => x.id_komentarza);
                    table.ForeignKey(
                        name: "FK_KOMENTARZE_UZYTKOWNICY_id_uzytkownika",
                        column: x => x.id_uzytkownika,
                        principalTable: "UZYTKOWNICY",
                        principalColumn: "id_uzytkownika",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KOMENTARZE_ZADANIA_id_zadania",
                        column: x => x.id_zadania,
                        principalTable: "ZADANIA",
                        principalColumn: "id_zadania",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KOMENTARZE_id_uzytkownika",
                table: "KOMENTARZE",
                column: "id_uzytkownika");

            migrationBuilder.CreateIndex(
                name: "IX_KOMENTARZE_id_zadania",
                table: "KOMENTARZE",
                column: "id_zadania");

            migrationBuilder.CreateIndex(
                name: "IX_TABLICE_id_uzytkownika_owner",
                table: "TABLICE",
                column: "id_uzytkownika_owner");

            migrationBuilder.CreateIndex(
                name: "IX_TABLICE_UZYTKOWNICY_id_tablicy",
                table: "TABLICE_UZYTKOWNICY",
                column: "id_tablicy");

            migrationBuilder.CreateIndex(
                name: "IX_UZYTKOWNICY_email",
                table: "UZYTKOWNICY",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZADANIA_id_tablicy",
                table: "ZADANIA",
                column: "id_tablicy");

            migrationBuilder.CreateIndex(
                name: "IX_ZADANIA_id_uzytkownika_przypisanego",
                table: "ZADANIA",
                column: "id_uzytkownika_przypisanego");

            migrationBuilder.CreateIndex(
                name: "IX_ZADANIA_id_uzytkownika_tworcy_zadania",
                table: "ZADANIA",
                column: "id_uzytkownika_tworcy_zadania");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KOMENTARZE");

            migrationBuilder.DropTable(
                name: "TABLICE_UZYTKOWNICY");

            migrationBuilder.DropTable(
                name: "ZADANIA");

            migrationBuilder.DropTable(
                name: "TABLICE");

            migrationBuilder.DropTable(
                name: "UZYTKOWNICY");
        }
    }
}
