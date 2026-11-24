using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Campanhas.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CriacaoDeCampanhasEDoacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "campanhas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    data_inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_fim = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    meta_financeira = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valor_arrecadado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_campanhas", x => x.id);
                    table.CheckConstraint("ck_campanhas_arrecadado_nao_negativo", "valor_arrecadado >= 0");
                    table.CheckConstraint("ck_campanhas_meta_positiva", "meta_financeira > 0");
                    table.CheckConstraint("ck_campanhas_periodo", "data_fim > data_inicio");
                });

            migrationBuilder.CreateTable(
                name: "doacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    campanha_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    motivo_rejeicao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_doacoes", x => x.id);
                    table.CheckConstraint("ck_doacoes_valor_positivo", "valor > 0");
                    table.ForeignKey(
                        name: "fk_doacoes_campanhas_campanha_id",
                        column: x => x.campanha_id,
                        principalTable: "campanhas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_campanhas_status",
                table: "campanhas",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_doacoes_campanha_id",
                table: "doacoes",
                column: "campanha_id");

            migrationBuilder.CreateIndex(
                name: "ix_doacoes_doador_id",
                table: "doacoes",
                column: "doador_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "doacoes");

            migrationBuilder.DropTable(
                name: "campanhas");
        }
    }
}
