using Comprobante.Core.Representacion;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Comprobante.Pdf;

/// <summary>
/// La plantilla de la representación impresa: acomoda en hojas carta los textos que ya dejó listos
/// <see cref="ConstructorDeRepresentacion"/>. No consulta catálogos ni hace cuentas; solo dibuja.
/// </summary>
/// <remarks>
/// Los colores son los de la identidad de la app (tinta, líneas, morado y su tinte). El PDF sale etiquetado (PDF/UA) para
/// que un lector de pantalla lo recorra en orden: título, emisor y receptor, datos, conceptos, totales y sellos. Lo que es
/// solo decoración o se repite en cada hoja (marca de agua, franja de prueba, encabezado compacto y pie) va como
/// artefacto y no estorba esa lectura. Los rótulos van en minúsculas porque se leen mejor y los lectores de pantalla no
/// deletrean las mayúsculas sostenidas.
/// <para>
/// Las columnas de cifras miden lo que su contenido necesita (<see cref="AnchoEnMm"/>), así un importe de siete dígitos no
/// se parte en dos renglones, y lo que sobra se le da a la descripción. Las filas, los totales y los sellos se piden
/// enteros para que no se corten entre hojas, salvo que sean tan largos que no cabrían ni en una hoja sola.
/// </para>
/// </remarks>
internal sealed class PlantillaDeRepresentacion(RepresentacionImpresa r, OpcionesDelPdf opciones)
{
    private const Unit Mm = Unit.Millimetre;

    private const string Tinta = "#1b1733";
    private const string Rotulo = "#5e5878"; // 6.7:1 sobre blanco
    private const string Linea = "#d9d4e8";
    private const string Borde = "#9c97ad";
    private const string Morado = "#512bd4"; // 7.9:1 sobre blanco
    private const string Tinte = "#ede7ff";
    private const string MarcaDeAgua = "#14512bd4";
    private const string Vacio = "—";

    private const string Texto = MotorPdf.FamiliaTexto;
    private const string Mono = MotorPdf.FamiliaMono;

    // Hoja carta (215.9 mm) con márgenes de 12 mm.
    private const float AnchoUtil = 191.9f;
    private const float AnchoDeCifras = 66f;
    private const float Separacion = 5f;
    private const float AnchoIzquierdo = AnchoUtil - AnchoDeCifras - Separacion;

    private enum Alineacion
    {
        Izquierda,
        Centro,
        Derecha,
    }

    /// <summary>Hay algo que poner al pie: el QR (con timbre) o la cadena original y los sellos (si se pidieron).</summary>
    private bool MuestraElPie => r.EstaTimbrado || opciones.IncluirCadenaYSellos;

    public Document Armar() =>
        Document.Create(Componer)
            .WithSettings(new DocumentSettings { PDFUA_Conformance = PDFUA_Conformance.PDFUA_1 })
            .WithMetadata(new DocumentMetadata
            {
                Title = r.TituloDelDocumento,
                Author = "Comprobante",
                Creator = "Comprobante",
                Subject = r.AsuntoDelDocumento,
                Language = "es-MX",
            });

    private void Componer(IDocumentContainer documento) =>
        documento.Page(pagina =>
        {
            pagina.Size(PageSizes.Letter);
            pagina.MarginHorizontal(12, Mm);
            pagina.MarginTop(r.EstaTimbrado ? 11 : 13, Mm); // sin timbre, la franja de «prueba» ocupa los primeros 7 mm
            pagina.MarginBottom(9, Mm);
            pagina.DefaultTextStyle(x => x.FontFamily(Texto).FontSize(9).FontColor(Tinta));

            if (!r.EstaTimbrado)
            {
                pagina.Background().Element(FondoDePrueba);
            }

            pagina.Header().SkipOnce().SemanticIgnore().Element(EncabezadoCompacto);
            pagina.Content().Element(Contenido);
            pagina.Footer().SemanticIgnore().Element(Pie);
        });

    // ------------------------------------------------------------------------------------------------------------
    // Lo que se repite en cada hoja
    // ------------------------------------------------------------------------------------------------------------

    private static void FondoDePrueba(IContainer fondo) =>
        fondo.Column(columna =>
        {
            columna.Item().Height(7, Mm).Background(Tinte).AlignCenter().AlignMiddle()
                .Text("COMPROBANTE DE PRUEBA · NO TIMBRADO · SIN VALIDEZ FISCAL")
                .FontFamily(Texto).FontSize(7.5f).Bold().LetterSpacing(0.08f).FontColor(Morado);

            columna.Item().Extend().SemanticIgnore().AlignCenter().AlignMiddle().Rotate(-30)
                .Text("SIN VALIDEZ FISCAL").FontFamily(Texto).FontSize(44).Bold().FontColor(MarcaDeAgua);
        });

    /// <summary>Desde la segunda hoja, una línea que dice de qué comprobante es.</summary>
    private void EncabezadoCompacto(IContainer c) =>
        c.PaddingBottom(3, Mm).BorderBottom(0.5f).BorderColor(Linea).PaddingBottom(1.5f, Mm).Row(fila =>
        {
            fila.RelativeItem().Text(texto =>
            {
                texto.Span(r.Titulo).Bold().FontColor(Morado);
                if (r.SerieFolio is { } serieFolio)
                {
                    texto.Span("  " + serieFolio).SemiBold();
                }
            });

            fila.AutoItem().Text(r.FolioFiscal ?? "Sin timbrar").FontFamily(Mono).FontSize(7.5f).FontColor(Rotulo);
        });

    private static void Pie(IContainer c) =>
        c.PaddingTop(1.5f, Mm).Row(fila =>
        {
            fila.RelativeItem().Text("Generado con Comprobante").FontSize(7).FontColor(Rotulo);

            fila.RelativeItem(2).AlignCenter().Text("Este documento es una representación impresa de un CFDI")
                .FontSize(7.5f).SemiBold();

            fila.RelativeItem().AlignRight().Text(texto =>
            {
                texto.DefaultTextStyle(x => x.FontSize(7).FontColor(Rotulo));
                texto.Span("Página ");
                texto.CurrentPageNumber();
                texto.Span(" de ");
                texto.TotalPages();
            });
        });

    // ------------------------------------------------------------------------------------------------------------
    // El comprobante
    // ------------------------------------------------------------------------------------------------------------

    private void Contenido(IContainer c) =>
        c.Column(columna =>
        {
            columna.Spacing(3.5f, Mm);

            columna.Item().Element(Cabecera);

            columna.Item().Row(fila =>
            {
                fila.Spacing(5, Mm);
                fila.RelativeItem().Element(x => Tarjeta(x, r.Emisor));
                fila.RelativeItem().Element(x => Tarjeta(x, r.Receptor));
            });

            columna.Item().Element(DatosDelComprobante);

            if (r.Relacionados.Count > 0)
            {
                columna.Item().Element(CfdiRelacionados);
            }

            if (r.Conceptos.Count > 0)
            {
                columna.Item().Element(Conceptos);
            }

            // Los totales y los sellos se piden enteros: que una hoja no termine a la mitad de ellos. Si son tan largos que no
            // cabrían en una hoja sola (un comprobante con cientos de impuestos o conceptos), se dejan partir.
            var totales = columna.Item();
            (r.Traslados.Count + r.Retenciones.Count <= 20 ? totales.ShowEntire() : totales.EnsureSpace(Puntos(70)))
                .Element(Totales);

            if (MuestraElPie)
            {
                var sellos = columna.Item();
                (LargoDeLosSellos() <= 5000 ? sellos.ShowEntire() : sellos.EnsureSpace(Puntos(45))).Element(Sellos);
            }
        });

    private static float Puntos(float milimetros) => milimetros * 72f / 25.4f;

    private int LargoDeLosSellos() =>
        r.Sellos.Cadena.Length + (r.Sellos.SelloDelEmisor?.Length ?? 0) + (r.Sellos.SelloDelSat?.Length ?? 0);

    private void Cabecera(IContainer c) =>
        c.Row(fila =>
        {
            fila.ConstantItem(60, Mm).Height(20, Mm).Element(Logo);

            fila.RelativeItem().PaddingHorizontal(5, Mm).AlignMiddle().Column(titulo =>
            {
                titulo.Item().SemanticHeading1().Text(r.Titulo)
                    .FontSize(r.Titulo.Length > 8 ? 20 : 26).ExtraBold().FontColor(Morado);
                titulo.Item().Text("Comprobante fiscal digital por Internet").FontSize(8).FontColor(Rotulo);
            });

            fila.ConstantItem(AnchoDeCifras, Mm).Column(cajas =>
            {
                cajas.Spacing(2, Mm);

                cajas.Item().Element(Caja).Column(caja =>
                {
                    caja.Item().Text("Folio fiscal (UUID)").FontSize(7.5f).SemiBold().FontColor(Rotulo);
                    if (r.FolioFiscal is { } uuid)
                    {
                        // 36 caracteres en monoespaciada: a 7 pt miden 56 mm y la caja deja 59.
                        caja.Item().Text(uuid).FontFamily(Mono).FontSize(7).Bold();
                    }
                    else
                    {
                        caja.Item().Text("Sin timbrar").FontSize(11).Bold().FontColor(Morado);
                    }
                });

                cajas.Item().Element(Caja).Row(fila2 =>
                {
                    fila2.RelativeItem().Column(serie =>
                    {
                        serie.Item().Text("Serie-folio").FontSize(7.5f).SemiBold().FontColor(Rotulo);
                        serie.Item().Text(r.SerieFolio ?? Vacio).FontSize(11).Bold();
                    });

                    fila2.AutoItem().Column(version =>
                    {
                        version.Item().AlignRight().Text("Versión").FontSize(7.5f).SemiBold().FontColor(Rotulo);
                        version.Item().AlignRight().Text(r.Version).FontSize(11).Bold();
                    });
                });
            });
        });

    private static IContainer Caja(IContainer c) =>
        c.Border(0.9f).BorderColor(Borde).CornerRadius(2, Mm).PaddingVertical(1.4f, Mm).PaddingHorizontal(3, Mm);

    /// <summary>El logo propio del usuario (PNG o JPEG) o, si no subió ninguno, el de Comprobante; en una caja de 60 × 20 mm.</summary>
    private void Logo(IContainer c)
    {
        var caja = c.AlignMiddle().AlignLeft();
        if (opciones.Logo is { Length: > 0 } propio)
        {
            caja.SemanticImage("Logotipo del emisor").Image(propio).FitArea();
        }
        else
        {
            caja.SemanticImage("Logotipo de Comprobante")
                .Svg(SvgImage.FromText(MotorPdf.LeerTexto("recursos/logo.svg"))).FitArea();
        }
    }

    private static void Tarjeta(IContainer c, ParteImpresa parte) =>
        c.Border(0.75f).BorderColor(Linea).CornerRadius(1.5f, Mm).Column(columna =>
        {
            columna.Item().Background(Tinte).CornerRadiusTopLeft(1.5f, Mm).CornerRadiusTopRight(1.5f, Mm)
                .PaddingVertical(1.4f, Mm).PaddingHorizontal(3, Mm)
                .SemanticHeading2().Text(parte.Titulo).FontSize(8.5f).Bold().FontColor(Morado);

            columna.Item().PaddingVertical(1.3f, Mm).PaddingHorizontal(3, Mm).Column(datos =>
            {
                datos.Spacing(1, Mm);
                foreach (var dato in parte.Datos)
                {
                    datos.Item().Row(fila =>
                    {
                        fila.ConstantItem(28, Mm).Text(dato.Etiqueta).FontSize(7.5f).SemiBold().FontColor(Rotulo);
                        fila.RelativeItem().Element(x => Valor(x, dato));
                    });
                }
            });
        });

    private static void Valor(IContainer c, DatoImpreso dato)
    {
        var texto = c.Text(dato.Valor);
        switch (dato.Enfasis)
        {
            case Enfasis.Nombre:
                texto.FontSize(10).Bold();
                break;
            case Enfasis.Codigo:
                texto.FontFamily(Mono).FontSize(9).Bold();
                break;
            default:
                texto.FontSize(9);
                break;
        }
    }

    /// <summary>Los datos del comprobante en una cuadrícula de cuatro columnas, cada uno con su línea al pie.</summary>
    private void DatosDelComprobante(IContainer c) =>
        c.Column(columna =>
        {
            foreach (var grupo in r.Datos.Chunk(4))
            {
                columna.Item().Row(fila =>
                {
                    fila.Spacing(4, Mm);
                    for (var i = 0; i < 4; i++)
                    {
                        var hueco = fila.RelativeItem();
                        if (i >= grupo.Length)
                        {
                            continue;
                        }

                        var dato = grupo[i];
                        hueco.BorderBottom(0.5f).BorderColor(Linea).PaddingVertical(1, Mm).Column(celda =>
                        {
                            celda.Item().Text(dato.Etiqueta).FontSize(7.5f).SemiBold().FontColor(Rotulo);
                            celda.Item().Element(x => Valor(x, dato));
                        });
                    }
                });
            }
        });

    private void CfdiRelacionados(IContainer c) =>
        c.Column(columna =>
        {
            columna.Spacing(1.2f, Mm);
            columna.Item().SemanticHeading2().Text("CFDI relacionados").FontSize(8.5f).Bold().FontColor(Morado);
            foreach (var relacion in r.Relacionados)
            {
                columna.Item().Row(fila =>
                {
                    fila.Spacing(4, Mm);
                    fila.ConstantItem(72, Mm).Text(relacion.Tipo).FontSize(9).SemiBold();
                    fila.RelativeItem().Column(uuids =>
                    {
                        foreach (var uuid in relacion.Uuids)
                        {
                            uuids.Item().Text(uuid).FontFamily(Mono).FontSize(8.5f).Bold();
                        }
                    });
                });
            }
        });

    // ------------------------------------------------------------------------------------------------------------
    // Conceptos
    // ------------------------------------------------------------------------------------------------------------

    private void Conceptos(IContainer c) =>
        c.SemanticTable().Table(tabla =>
        {
            // Las columnas de cifras miden lo que sus importes necesitan; lo que sobra es de la descripción.
            var (cantidad, valorUnitario, descuento, importe, puntos) = MedirCifras();
            var anchoDeDescripcion = AnchoUtil - AnchoFijoDeConceptos - cantidad - valorUnitario - descuento - importe;

            tabla.ColumnsDefinition(columnas =>
            {
                columnas.ConstantColumn(19, Mm); // clave de producto o servicio (8 dígitos)
                columnas.ConstantColumn(23, Mm); // unidad
                columnas.ConstantColumn(cantidad, Mm);
                columnas.RelativeColumn(); // descripción
                columnas.ConstantColumn(valorUnitario, Mm);
                columnas.ConstantColumn(descuento, Mm);
                columnas.ConstantColumn(11, Mm); // objeto de impuesto
                columnas.ConstantColumn(importe, Mm);
            });

            tabla.Header(encabezado =>
            {
                CeldaDeEncabezado(encabezado, "Clave prod. serv.");
                CeldaDeEncabezado(encabezado, "Unidad");
                CeldaDeEncabezado(encabezado, "Cantidad", Alineacion.Derecha);
                CeldaDeEncabezado(encabezado, "Descripción");
                CeldaDeEncabezado(encabezado, "Valor unitario", Alineacion.Derecha);
                CeldaDeEncabezado(encabezado, "Descuento", Alineacion.Derecha);
                CeldaDeEncabezado(encabezado, "Obj. imp.", Alineacion.Centro);
                CeldaDeEncabezado(encabezado, "Importe", Alineacion.Derecha);
            });

            foreach (var concepto in r.Conceptos)
            {
                var entera = !EsDemasiadoLargo(concepto, anchoDeDescripcion);
                IContainer Celda() => CeldaDeConcepto(tabla.Cell(), entera);

                Celda().Text(concepto.ClaveProdServ).FontFamily(Mono).FontSize(8);
                Celda().Text(concepto.Unidad).FontSize(8.5f);
                Celda().AlignRight().Text(concepto.Cantidad).FontSize(puntos);
                Celda().Column(descripcion =>
                {
                    descripcion.Item().Text(texto =>
                    {
                        texto.DefaultTextStyle(x => x.FontSize(8.5f));
                        if (concepto.NoIdentificacion is { } numero)
                        {
                            texto.Span(numero).Bold();
                            texto.Span(" · ");
                        }

                        texto.Span(concepto.Descripcion);
                    });

                    foreach (var impuesto in concepto.Impuestos)
                    {
                        descripcion.Item().Text(impuesto).FontSize(7).FontColor(Rotulo);
                    }
                });
                Celda().AlignRight().Text(concepto.ValorUnitario).FontSize(puntos);
                Celda().AlignRight().Text(concepto.Descuento ?? Vacio).FontSize(puntos);
                Celda().AlignCenter().Text(concepto.ObjetoImp).FontSize(8.5f);
                Celda().AlignRight().Text(concepto.Importe).FontSize(puntos);
            }
        });

    private const float AnchoMinimoDeDescripcion = 45f;
    private const float AnchoFijoDeConceptos = 19f + 23f + 11f; // clave de producto o servicio, unidad y objeto de impuesto

    /// <summary>
    /// Mide las cuatro columnas de cifras de la tabla de conceptos. Con importes enormes dejarían a la descripción en una tira
    /// donde cabe una letra por renglón; antes de llegar a eso las cifras se achican (hasta 5.5 puntos).
    /// </summary>
    private (float Cantidad, float ValorUnitario, float Descuento, float Importe, float Puntos) MedirCifras()
    {
        (float, float, float, float, float) Medir(float puntos) =>
        (
            AnchoDeColumna("Cantidad", r.Conceptos.Select(k => k.Cantidad), puntos, 60),
            AnchoDeColumna("Valor unitario", r.Conceptos.Select(k => k.ValorUnitario), puntos, 60),
            AnchoDeColumna("Descuento", r.Conceptos.Select(k => k.Descuento ?? Vacio), puntos, 60),
            AnchoDeColumna("Importe", r.Conceptos.Select(k => k.Importe), puntos, 60),
            puntos
        );

        var medidas = Medir(8.5f);
        foreach (var puntos in new[] { 8.5f, 7.5f, 6.5f, 5.5f })
        {
            medidas = Medir(puntos);
            if (AnchoFijoDeConceptos + medidas.Item1 + medidas.Item2 + medidas.Item3 + medidas.Item4 + AnchoMinimoDeDescripcion <= AnchoUtil)
            {
                break;
            }
        }

        return medidas;
    }

    /// <summary>
    /// Un renglón que no cabe en una hoja sola (una descripción de mil caracteres con decenas de impuestos) se deja partir:
    /// pedirle a QuestPDF que lo mantenga entero haría que fallara. Se estima la altura con una letra promedio de 0.58 em,
    /// más ancha que la de casi cualquier texto, y se deja partir desde los 150 mm, bastante antes de llenar una hoja.
    /// </summary>
    private static bool EsDemasiadoLargo(ConceptoImpreso concepto, float anchoDeDescripcion)
    {
        var util = Math.Max(anchoDeDescripcion - 3f, 15f);

        static int Renglones(int caracteres, float puntos, float util) =>
            Math.Max(1, (int)Math.Ceiling(caracteres * (puntos * 0.58f * 25.4f / 72f) / util));

        var alto = Renglones(concepto.Descripcion.Length + (concepto.NoIdentificacion?.Length ?? 0) + 3, 8.5f, util) * 3.7f;
        alto += concepto.Impuestos.Sum(impuesto => Renglones(impuesto.Length, 7f, util) * 3.1f);
        return alto > 150f;
    }

    private static IContainer CeldaDeConcepto(IContainer c, bool entera) =>
        (entera ? c.ShowEntire() : c).BorderBottom(0.5f).BorderColor(Linea).PaddingVertical(1.3f, Mm).PaddingHorizontal(1.5f, Mm);

    private static void CeldaDeEncabezado(TableCellDescriptor encabezado, string texto, Alineacion alineacion = Alineacion.Izquierda)
    {
        var celda = encabezado.Cell().SemanticHorizontalHeader().Background(Tinte).BorderTop(0.8f).BorderColor(Tinta)
            .PaddingVertical(1.4f, Mm).PaddingHorizontal(1.5f, Mm);
        Alinear(celda, alineacion).AlignBottom().Text(texto).FontSize(7.5f).SemiBold();
    }

    private static IContainer Alinear(IContainer c, Alineacion alineacion) =>
        alineacion switch
        {
            Alineacion.Derecha => c.AlignRight(),
            Alineacion.Centro => c.AlignCenter(),
            _ => c.AlignLeft(),
        };

    /// <summary>
    /// Cuánto mide una columna de cifras: lo que ocupa su valor más largo o, si es más, la palabra más larga del encabezado
    /// (que se puede partir en dos renglones, pero no a la mitad de una palabra), más el aire de los lados.
    /// </summary>
    private static float AnchoDeColumna(string encabezado, IEnumerable<string> valores, float puntos = 8.5f, float maximo = 34f)
    {
        var palabra = encabezado.Split(' ').Max(p => AnchoEnMm(p, 7.5f)) * 1.06f; // el encabezado va en seminegrita
        var contenido = valores.Max(v => AnchoEnMm(v, puntos));
        return Math.Clamp(Math.Max(palabra, contenido) + 3.4f, 12, maximo);
    }

    /// <summary>
    /// Cuánto mide un texto, calculado por lo alto con las anchuras de Atkinson Hyperlegible Next: un dígito ocupa 0.62 em, la
    /// coma, el punto y el espacio 0.28, una mayúscula 0.68 y cualquier otra letra 0.56.
    /// </summary>
    private static float AnchoEnMm(string? texto, float puntos)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return 0;
        }

        var em = 0f;
        foreach (var caracter in texto)
        {
            em += caracter switch
            {
                >= '0' and <= '9' => 0.62f,
                ',' or '.' or ' ' or ':' or ';' or '|' or '!' or '\'' or 'i' or 'l' or 'I' => 0.28f,
                '$' => 0.6f,
                _ => char.IsUpper(caracter) ? 0.68f : 0.56f,
            };
        }

        return em * puntos * 25.4f / 72f;
    }

    // ------------------------------------------------------------------------------------------------------------
    // Totales
    // ------------------------------------------------------------------------------------------------------------

    private void Totales(IContainer c) =>
        c.Row(fila =>
        {
            fila.Spacing(Separacion, Mm);

            fila.RelativeItem().Column(izquierda =>
            {
                izquierda.Spacing(3, Mm);
                if (r.Totales.ImporteConLetra is { } letra)
                {
                    izquierda.Item().Element(x => ImporteConLetra(x, letra));
                }

                if (r.Traslados.Count > 0 || r.Retenciones.Count > 0)
                {
                    izquierda.Item().Element(ResumenDeImpuestos);
                }
            });

            fila.ConstantItem(AnchoDeCifras, Mm).Element(Cifras);
        });

    private static void ImporteConLetra(IContainer c, string letra) =>
        c.Border(0.75f).BorderColor(Linea).CornerRadius(1.5f, Mm).PaddingVertical(1.6f, Mm).PaddingHorizontal(3, Mm).Column(columna =>
        {
            columna.Item().Text("Importe con letra").FontSize(7.5f).SemiBold().FontColor(Rotulo);
            columna.Item().Text(letra).FontSize(9.5f).Bold();
        });

    /// <summary>
    /// Los impuestos trasladados y retenidos del comprobante. Van lado a lado si caben (con las medidas que sus importes
    /// piden) y, si no, uno debajo del otro, para no partir los importes grandes en dos renglones.
    /// </summary>
    private void ResumenDeImpuestos(IContainer c)
    {
        var traslados = r.Traslados.Count == 0
            ? null
            : new TablaDeImpuestos(
                "Impuestos trasladados",
                ["Base", "Impuesto", "Factor", "Tasa o cuota", "Importe"],
                [Alineacion.Derecha, Alineacion.Izquierda, Alineacion.Izquierda, Alineacion.Derecha, Alineacion.Derecha],
                [.. r.Traslados.Select(t => new[] { t.Base ?? Vacio, t.Impuesto, t.Factor ?? Vacio, t.TasaOCuota ?? Vacio, t.Importe ?? Vacio })]);

        var retenciones = r.Retenciones.Count == 0
            ? null
            : new TablaDeImpuestos(
                "Impuestos retenidos",
                ["Impuesto", "Importe"],
                [Alineacion.Izquierda, Alineacion.Derecha],
                [.. r.Retenciones.Select(t => new[] { t.Impuesto, t.Importe ?? Vacio })]);

        if (traslados is not null && retenciones is not null)
        {
            if (traslados.Ancho + retenciones.Ancho + 4 <= AnchoIzquierdo)
            {
                c.Row(fila =>
                {
                    fila.Spacing(4, Mm);
                    fila.RelativeItem(traslados.Ancho).Element(x => Dibujar(x, traslados));
                    fila.RelativeItem(retenciones.Ancho).Element(x => Dibujar(x, retenciones));
                });
            }
            else
            {
                c.Column(columna =>
                {
                    columna.Spacing(3, Mm);
                    columna.Item().Element(x => Dibujar(x, traslados));
                    columna.Item().AlignLeft().MaxWidth(Math.Max(retenciones.Ancho, 70), Mm).Element(x => Dibujar(x, retenciones));
                });
            }

            return;
        }

        Dibujar(c, (traslados ?? retenciones)!);
    }

    private sealed class TablaDeImpuestos(string titulo, string[] encabezados, Alineacion[] alineaciones, List<string[]> filas)
    {
        public string Titulo { get; } = titulo;

        public string[] Encabezados { get; } = encabezados;

        public Alineacion[] Alineaciones { get; } = alineaciones;

        public List<string[]> Filas { get; } = filas;

        /// <summary>El ancho de cada columna, en milímetros, según lo que pide su contenido.</summary>
        public float[] Anchos { get; } = [.. encabezados.Select((encabezado, i) => AnchoDeColumna(encabezado, filas.Select(fila => fila[i])))];

        public float Ancho => Anchos.Sum();
    }

    private static void Dibujar(IContainer c, TablaDeImpuestos tabla) =>
        c.Column(columna =>
        {
            columna.Spacing(1, Mm);
            columna.Item().SemanticHeading2().Text(tabla.Titulo).FontSize(8.5f).Bold().FontColor(Morado);
            columna.Item().SemanticTable().Table(t =>
            {
                t.ColumnsDefinition(columnas =>
                {
                    foreach (var ancho in tabla.Anchos)
                    {
                        columnas.RelativeColumn(ancho);
                    }
                });

                t.Header(encabezado =>
                {
                    for (var i = 0; i < tabla.Encabezados.Length; i++)
                    {
                        CeldaDeEncabezado(encabezado, tabla.Encabezados[i], tabla.Alineaciones[i]);
                    }
                });

                foreach (var fila in tabla.Filas)
                {
                    for (var i = 0; i < fila.Length; i++)
                    {
                        var celda = t.Cell().BorderBottom(0.5f).BorderColor(Linea).PaddingVertical(1, Mm).PaddingHorizontal(1.5f, Mm);
                        Alinear(celda, tabla.Alineaciones[i]).Text(fila[i]).FontSize(8.5f);
                    }
                }
            });
        });

    /// <summary>Subtotal, descuento e impuestos en renglones, y el total en una tarjeta con la sombra sólida de la app.</summary>
    private void Cifras(IContainer c) =>
        c.Column(columna =>
        {
            Cifra(columna, "Subtotal", r.Totales.Subtotal);
            if (r.Totales.Descuento is { } descuento)
            {
                Cifra(columna, "Descuento", descuento);
            }

            if (r.Totales.TotalTrasladados is { } trasladados)
            {
                Cifra(columna, "Impuestos trasladados", trasladados);
            }

            if (r.Totales.TotalRetenidos is { } retenidos)
            {
                Cifra(columna, "Impuestos retenidos", retenidos);
            }

            // La sombra se sale 2 mm por la derecha y por abajo: se les deja ese espacio.
            columna.Item().PaddingTop(2.5f, Mm).PaddingRight(2, Mm).PaddingBottom(2, Mm).Element(Total);
        });

    private static void Cifra(ColumnDescriptor columna, string rotulo, string valor) =>
        columna.Item().BorderBottom(0.5f).BorderColor(Linea).PaddingVertical(1.2f, Mm).Row(fila =>
        {
            fila.RelativeItem().Text(rotulo).FontSize(8).SemiBold().FontColor(Rotulo);
            fila.AutoItem().Text(valor).FontSize(10).SemiBold();
        });

    private void Total(IContainer c)
    {
        // Un total de ocho dígitos o más (con la moneda, si no es peso) empequeñece para no apretar la palabra «Total».
        var tamano = r.Totales.Total.Length switch
        {
            <= 11 => 17f,
            <= 13 => 15f,
            <= 15 => 13f,
            _ => 11f,
        };

        c.Layers(capas =>
        {
            capas.Layer().OffsetX(2, Mm).OffsetY(2, Mm).Background(Morado).CornerRadius(3, Mm);
            capas.PrimaryLayer().Background("#ffffff").Border(1).BorderColor(Tinta).CornerRadius(3, Mm)
                .PaddingVertical(2.2f, Mm).PaddingHorizontal(4, Mm).Row(fila =>
                {
                    fila.RelativeItem().AlignMiddle().Text("Total").FontSize(9.5f).Bold();
                    fila.AutoItem().Text(r.Totales.Total).FontSize(tamano).ExtraBold();
                });
        });
    }

    // ------------------------------------------------------------------------------------------------------------
    // Al pie: el QR, la cadena original y los sellos
    // ------------------------------------------------------------------------------------------------------------

    private void Sellos(IContainer c) =>
        c.Row(fila =>
        {
            fila.Spacing(5, Mm);
            fila.ConstantItem(36, Mm).Element(CajaDelQr);
            if (opciones.IncluirCadenaYSellos)
            {
                fila.RelativeItem().Element(TextosDeLosSellos);
            }
        });

    /// <summary>
    /// Con timbre, el QR del verificador del SAT (30 mm de código y su zona de silencio) y el enlace a esa misma dirección,
    /// para quien no puede escanearlo. Sin timbre, una caja que explica por qué no hay.
    /// </summary>
    private void CajaDelQr(IContainer c)
    {
        if (r.Sellos.UrlDeVerificacion is { } url)
        {
            c.Column(columna =>
            {
                columna.Item().Border(0.5f).BorderColor(Linea).Padding(3, Mm)
                    .SemanticFigure("Código QR para verificar el comprobante en el portal del SAT")
                    .Svg(SvgImage.FromText(CodigoQr.ComoSvg(url))).FitArea();

                columna.Item().PaddingTop(1.5f, Mm).SemanticLink("Verificar este comprobante en el portal del SAT").Text(texto =>
                {
                    texto.AlignCenter();
                    texto.Hyperlink("Verifica este comprobante en el portal del SAT", url)
                        .FontSize(7).FontColor(Morado).Underline();
                });
            });
            return;
        }

        c.Height(34, Mm).Layers(capas =>
        {
            capas.Layer().SemanticIgnore().Svg(SvgImage.FromText(CajaDiscontinua)).FitArea();
            capas.PrimaryLayer().Padding(3, Mm).AlignMiddle().Column(columna =>
            {
                columna.Spacing(1.5f, Mm);
                columna.Item().Text(texto =>
                {
                    texto.AlignCenter();
                    texto.Span("Sin timbre").FontSize(9.5f).Bold().FontColor(Morado);
                });
                columna.Item().Text(texto =>
                {
                    texto.AlignCenter();
                    texto.Span("Este CFDI no se timbró: no existe en el SAT y no tiene código de verificación.")
                        .FontSize(7).FontColor(Rotulo);
                });
            });
        });
    }

    private const string CajaDiscontinua =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\">"
        + "<rect x=\"0.5\" y=\"0.5\" width=\"99\" height=\"99\" rx=\"4\" fill=\"none\" stroke=\"#9c97ad\" stroke-width=\"0.9\" stroke-dasharray=\"3.5 2.8\"/>"
        + "</svg>";

    private void TextosDeLosSellos(IContainer c) =>
        c.Column(columna =>
        {
            columna.Spacing(2.5f, Mm);
            BloqueDeTexto(columna, r.Sellos.TituloDeLaCadena, r.Sellos.Cadena);
            if (r.Sellos.SelloDelEmisor is { Length: > 0 } emisor)
            {
                BloqueDeTexto(columna, "Sello digital del emisor", emisor);
            }

            if (r.Sellos.SelloDelSat is { Length: > 0 } sat)
            {
                BloqueDeTexto(columna, "Sello digital del SAT", sat);
            }
        });

    /// <summary>Un título con su línea y debajo la cadena en letra monoespaciada chica, que se corta donde caiga.</summary>
    private static void BloqueDeTexto(ColumnDescriptor columna, string titulo, string valor) =>
        columna.Item().Column(bloque =>
        {
            bloque.Item().BorderBottom(0.5f).BorderColor(Tinta).PaddingBottom(0.8f, Mm)
                .SemanticHeading2().Text(titulo).FontSize(8).Bold();
            bloque.Item().PaddingTop(1, Mm).Text(valor).FontFamily(Mono).FontSize(5.5f);
        });
}
