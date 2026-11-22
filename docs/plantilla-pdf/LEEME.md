# Boceto de la plantilla del PDF

Boceto de la representación impresa en PDF de un CFDI 4.0, hecho para fijar el diseño antes de programarla (`PLAN.md`, fase 6). El PDF real ya existe (`backend/src/Comprobante.Pdf/`) y sigue este boceto, con las diferencias que se anotan más abajo. Partió de la estructura de un CFDI de ejemplo que pasó el dueño del proyecto (una hoja carta con logo, folio fiscal, emisor y receptor, conceptos, totales con letra, resumen de impuestos, datos del timbre, QR y sellos). Ese ejemplo **no está en el repositorio**: es la factura de un tercero, con su marca y con los datos de un cliente. La plantilla es propia, con el logo de Comprobante.

| Archivo | Qué es |
|---|---|
| `logo.svg` | El logo de Comprobante: la insignia «CF» del favicon y el nombre, con las letras convertidas a trazos para que no dependan de ninguna fuente. Es el logo que lleva el PDF cuando el usuario no sube otro |
| `boceto-sin-timbre.png` y `.html` | Un CFDI generado en la app: sin UUID ni código QR, con la franja «Comprobante de prueba» y la marca de agua «Sin validez fiscal» |
| `boceto-con-timbre.png` y `.html` | Un XML ya timbrado (con el complemento `TimbreFiscalDigital`): UUID, datos del SAT, sello del SAT y el QR de verificación. El UUID, el certificado y el sello del SAT están inventados |

Los datos son del comprobante de referencia `ingreso-retenciones` (`backend/tests/Comprobante.UnitTests/Referencia/`): emisor y receptor de prueba del SAT, tres conceptos, IVA a tasa 16 %, tasa 0 y exento, y retenciones de ISR e IVA. El QR del boceto es real: se leyó con un decodificador y devuelve la dirección del verificador del SAT con `id`, `re`, `rr`, `tt` y `fe`.

## Medidas y reglas

- Hoja carta, márgenes de 12 mm (15 mm arriba, bajo la franja de «prueba»).
- Logo en una caja de 60 × 20 mm, ajustado sin deformarlo. Si el usuario sube el suyo (PNG o JPEG) ocupa esa misma caja.
- Colores de la identidad de la app: tinta `#1b1733`, líneas `#d9d4e8`, morado `#512bd4` para el título, los encabezados de sección y el total, y el tinte `#ede7ff` en los encabezados de tabla (imprime bien y gasta poca tinta). El total lleva la sombra sólida de la tarjeta «registro» del sitio.
- Cadenas largas (sellos y cadena original) en letra monoespaciada de 5 pt que se corta donde caiga.
- El QR del boceto lleva una zona de silencio de 4 módulos y mide 31 mm. El del PDF real mide 30 mm (medidos en el PDF) y deja 3 mm de zona de silencio por lado.
- Con timbre no hay marca de agua ni franja. Sin timbre, el recuadro del QR dice «Sin timbre» y los sellos son la cadena original del comprobante y el sello del emisor.
- Pie: leyenda «Este documento es una representación impresa de un CFDI», «Generado con Comprobante» y «Página X de Y».

## Tipografía

El boceto usa Inter y DejaVu Sans Mono porque eran las que había a la mano. El PDF real usa **Atkinson Hyperlegible Next** y su versión monoespaciada (la del sitio y la app, licencia OFL), incrustadas en el ensamblado y con las fuentes del sistema apagadas, así que sale igual en cualquier equipo; los archivos y sus licencias están en [`backend/src/Comprobante.Pdf/Fuentes/`](../../backend/src/Comprobante.Pdf/Fuentes/LEEME.md).

## Lo que cambió en el PDF real

- Los rótulos, los títulos de sección y los encabezados de las tablas van en minúsculas, no en mayúsculas sostenidas: se leen mejor y algunos lectores de pantalla deletrean las palabras en mayúsculas. El título del comprobante sí va en mayúsculas.
- Los impuestos de cada concepto van uno por renglón, y las columnas de cifras miden lo que sus importes necesitan; con importes enormes la letra se achica (hasta 5.5 pt) antes de dejar a la descripción en una tira.
- Con timbre, bajo el QR hay un enlace al verificador del SAT, para quien no pueda escanearlo.
- Desde la segunda hoja, un encabezado compacto dice de qué comprobante es (con su UUID si está timbrado), y el encabezado de la tabla de conceptos se repite.
- Va etiquetado para lectores de pantalla, con idioma `es-MX`, título y asunto. Lo decorativo (marca de agua, franja de prueba, encabezado compacto y pie) queda fuera de esa lectura.
- Un concepto que se estima de más de 150 mm de alto se deja partir entre dos hojas; los demás nunca se cortan.

## Cómo verlo o cambiarlo

Abre un `.html` en el navegador. Para sacar un PDF de este boceto: Imprimir → Guardar como PDF, tamaño carta, sin márgenes y con gráficos de fondo. Los `.png` son capturas de esos mismos archivos.

Para ver el PDF real, la prueba `MuestrasDePdf` escribe los cinco comprobantes de referencia con y sin timbre, con logo, sin sellos y de 70 conceptos. Solo corre si se le dice dónde guardarlos (en PowerShell, `$env:MUESTRAS_PDF = 'C:\ruta'`; en bash, `MUESTRAS_PDF=/ruta`):

```bash
MUESTRAS_PDF=/tmp/muestras dotnet test backend/tests/Comprobante.UnitTests --filter MuestrasDePdf
```

Las hojas del README salen de ahí; cómo se pasan a imagen está en el [LEEME de las capturas](../capturas/LEEME.md).
