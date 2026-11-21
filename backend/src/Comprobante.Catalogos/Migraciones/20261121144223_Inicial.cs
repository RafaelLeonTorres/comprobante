using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Comprobante.Catalogos.Migraciones;

/// <inheritdoc />
public partial class _20261121144223_Inicial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

        migrationBuilder.CreateTable(
            name: "clave_prod_serv",
            columns: table => new
            {
                clave = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                incluir_iva = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                incluir_ieps = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                complemento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                inicio_vigencia = table.Column<DateOnly>(type: "date", nullable: false),
                fin_vigencia = table.Column<DateOnly>(type: "date", nullable: true),
                estimulo_franja_fronteriza = table.Column<bool>(type: "boolean", nullable: true),
                palabras_similares = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_clave_prod_serv", x => x.clave);
            });

        migrationBuilder.CreateTable(
            name: "clave_unidad",
            columns: table => new
            {
                clave = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                inicio_vigencia = table.Column<DateOnly>(type: "date", nullable: false),
                fin_vigencia = table.Column<DateOnly>(type: "date", nullable: true),
                simbolo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_clave_unidad", x => x.clave);
            });

        migrationBuilder.CreateTable(
            name: "codigo_postal",
            columns: table => new
            {
                clave = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                estado = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                municipio = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                localidad = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                estimulo_franja_fronteriza = table.Column<bool>(type: "boolean", nullable: true),
                inicio_vigencia = table.Column<DateOnly>(type: "date", nullable: false),
                fin_vigencia = table.Column<DateOnly>(type: "date", nullable: true),
                huso_horario = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                mes_inicio_verano = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                dia_inicio_verano = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                hora_inicio_verano = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                diferencia_verano = table.Column<int>(type: "integer", nullable: true),
                mes_inicio_invierno = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                dia_inicio_invierno = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                hora_inicio_invierno = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                diferencia_invierno = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_codigo_postal", x => x.clave);
            });

        migrationBuilder.CreateTable(
            name: "estado",
            columns: table => new
            {
                pais = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                clave = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                inicio_vigencia = table.Column<DateOnly>(type: "date", nullable: false),
                fin_vigencia = table.Column<DateOnly>(type: "date", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_estado", x => new { x.pais, x.clave });
            });

        migrationBuilder.CreateTable(
            name: "exportacion",
            columns: table => new
            {
                clave = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                inicio_vigencia = table.Column<DateOnly>(type: "date", nullable: false),
                fin_vigencia = table.Column<DateOnly>(type: "date", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_exportacion", x => x.clave);
            });

        migrationBuilder.CreateTable(
            name: "forma_pago",
            columns: table => new
            {
                clave = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                bancarizado = table.Column<bool>(type: "boolean", nullable: true),
                inicio_vigencia = table.Column<DateOnly>(type: "date", nullable: false),
                fin_vigencia = table.Column<DateOnly>(type: "date", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_forma_pago", x => x.clave);
            });

        migrationBuilder.CreateTable(
            name: "impuesto",
            columns: table => new
            {
                clave = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                descripcion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                retencion = table.Column<bool>(type: "boolean", nullable: false),
                traslado = table.Column<bool>(type: "boolean", nullable: false),
                local_o_federal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                inicio_vigencia = table.Column<DateOnly>(type: "date", nullable: false),
                fin_vigencia = table.Column<DateOnly>(type: "date", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_impuesto", x => x.clave);
            });

        migrationBuilder.CreateTable(
            name: "mes",
            columns: table => new
            {
                clave = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                inicio_vigencia = table.Column<DateOnly>(type: "date", nullable: false),
                fin_vigencia = table.Column<DateOnly>(type: "date", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_mes", x => x.clave);
            });

        migrationBuilder.CreateTable(
            name: "metodo_pago",
            columns: table => new
            {
                clave = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                inicio_vigencia = table.Column<DateOnly>(type: "date", nullable: false),
                fin_vigencia = table.Column<DateOnly>(type: "date", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_metodo_pago", x => x.clave);
            });

        migrationBuilder.CreateTable(
            name: "moneda",
            columns: table => new
            {
                clave = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                decimales = table.Column<int>(type: "integer", nullable: false),
                porcentaje_variacion = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                inicio_vigencia = table.Column<DateOnly>(type: "date", nullable: false),
                fin_vigencia = table.Column<DateOnly>(type: "date", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_moneda", x => x.clave);
            });

        migrationBuilder.CreateTable(
            name: "objeto_imp",
            columns: table => new
            {
                clave = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                inicio_vigencia = table.Column<DateOnly>(type: "date", nullable: false),
                fin_vigencia = table.Column<DateOnly>(type: "date", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_objeto_imp", x => x.clave);
            });

        migrationBuilder.CreateTable(
            name: "pais",
            columns: table => new
            {
                clave = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                formato_codigo_postal = table.Column<string>(type: "text", nullable: true),
                formato_registro_identidad_tributaria = table.Column<string>(type: "text", nullable: true),
                validacion_registro_identidad_tributaria = table.Column<string>(type: "text", nullable: true),
                agrupaciones = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_pais", x => x.clave);
            });

        migrationBuilder.CreateTable(
            name: "periodicidad",
            columns: table => new
            {
                clave = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                inicio_vigencia = table.Column<DateOnly>(type: "date", nullable: false),
                fin_vigencia = table.Column<DateOnly>(type: "date", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_periodicidad", x => x.clave);
            });

        migrationBuilder.CreateTable(
            name: "regimen_fiscal",
            columns: table => new
            {
                clave = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                fisica = table.Column<bool>(type: "boolean", nullable: false),
                moral = table.Column<bool>(type: "boolean", nullable: false),
                inicio_vigencia = table.Column<DateOnly>(type: "date", nullable: false),
                fin_vigencia = table.Column<DateOnly>(type: "date", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_regimen_fiscal", x => x.clave);
            });

        migrationBuilder.CreateTable(
            name: "tasa_o_cuota",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                rango_o_fijo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                valor_minimo = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                valor_maximo = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                impuesto = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                factor = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                traslado = table.Column<bool>(type: "boolean", nullable: false),
                retencion = table.Column<bool>(type: "boolean", nullable: false),
                inicio_vigencia = table.Column<DateOnly>(type: "date", nullable: false),
                fin_vigencia = table.Column<DateOnly>(type: "date", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_tasa_o_cuota", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "tipo_de_comprobante",
            columns: table => new
            {
                clave = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                valor_maximo = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: true),
                inicio_vigencia = table.Column<DateOnly>(type: "date", nullable: false),
                fin_vigencia = table.Column<DateOnly>(type: "date", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_tipo_de_comprobante", x => x.clave);
            });

        migrationBuilder.CreateTable(
            name: "tipo_factor",
            columns: table => new
            {
                clave = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                inicio_vigencia = table.Column<DateOnly>(type: "date", nullable: false),
                fin_vigencia = table.Column<DateOnly>(type: "date", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_tipo_factor", x => x.clave);
            });

        migrationBuilder.CreateTable(
            name: "tipo_relacion",
            columns: table => new
            {
                clave = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                inicio_vigencia = table.Column<DateOnly>(type: "date", nullable: false),
                fin_vigencia = table.Column<DateOnly>(type: "date", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_tipo_relacion", x => x.clave);
            });

        migrationBuilder.CreateTable(
            name: "uso_cfdi",
            columns: table => new
            {
                clave = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                fisica = table.Column<bool>(type: "boolean", nullable: false),
                moral = table.Column<bool>(type: "boolean", nullable: false),
                inicio_vigencia = table.Column<DateOnly>(type: "date", nullable: false),
                fin_vigencia = table.Column<DateOnly>(type: "date", nullable: true),
                regimenes_receptor = table.Column<string[]>(type: "text[]", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_uso_cfdi", x => x.clave);
            });

        migrationBuilder.CreateTable(
            name: "version_catalogos",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                archivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                publicacion = table.Column<DateOnly>(type: "date", nullable: true),
                sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                cargado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_version_catalogos", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_clave_prod_serv_descripcion",
            table: "clave_prod_serv",
            column: "descripcion")
            .Annotation("Npgsql:IndexMethod", "gin")
            .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

        migrationBuilder.CreateIndex(
            name: "ix_clave_unidad_nombre",
            table: "clave_unidad",
            column: "nombre")
            .Annotation("Npgsql:IndexMethod", "gin")
            .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

        migrationBuilder.CreateIndex(
            name: "ix_tasa_o_cuota_impuesto_factor",
            table: "tasa_o_cuota",
            columns: new[] { "impuesto", "factor" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "clave_prod_serv");

        migrationBuilder.DropTable(
            name: "clave_unidad");

        migrationBuilder.DropTable(
            name: "codigo_postal");

        migrationBuilder.DropTable(
            name: "estado");

        migrationBuilder.DropTable(
            name: "exportacion");

        migrationBuilder.DropTable(
            name: "forma_pago");

        migrationBuilder.DropTable(
            name: "impuesto");

        migrationBuilder.DropTable(
            name: "mes");

        migrationBuilder.DropTable(
            name: "metodo_pago");

        migrationBuilder.DropTable(
            name: "moneda");

        migrationBuilder.DropTable(
            name: "objeto_imp");

        migrationBuilder.DropTable(
            name: "pais");

        migrationBuilder.DropTable(
            name: "periodicidad");

        migrationBuilder.DropTable(
            name: "regimen_fiscal");

        migrationBuilder.DropTable(
            name: "tasa_o_cuota");

        migrationBuilder.DropTable(
            name: "tipo_de_comprobante");

        migrationBuilder.DropTable(
            name: "tipo_factor");

        migrationBuilder.DropTable(
            name: "tipo_relacion");

        migrationBuilder.DropTable(
            name: "uso_cfdi");

        migrationBuilder.DropTable(
            name: "version_catalogos");

        migrationBuilder.AlterDatabase()
            .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
    }
}
