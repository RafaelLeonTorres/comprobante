# Boceto de la plantilla del PDF

Diseño de la representación impresa en PDF de un CFDI 4.0, hecho para aprobarlo antes de programarla (`PLAN.md`, fase 6). Parte de la estructura de un CFDI de ejemplo que pasó el dueño del proyecto (una hoja carta con logo, folio fiscal, emisor y receptor, conceptos, totales con letra, resumen de impuestos, datos del timbre, QR y sellos). Ese ejemplo **no está en el repositorio**: es la factura de un tercero, con su marca y con los datos de un cliente. La plantilla es propia, con el logo de Comprobante.

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
- El QR lleva una zona de silencio de 4 módulos y mide 31 mm.
- Con timbre no hay marca de agua ni franja. Sin timbre, el recuadro del QR dice «Sin timbre» y los sellos son la cadena original del comprobante y el sello del emisor.
- Pie: leyenda «Este documento es una representación impresa de un CFDI», «Generado con Comprobante» y «Página X de Y».

## Tipografía

El boceto usa Inter y DejaVu Sans Mono porque eran las que había a la mano. El PDF final usará **Atkinson Hyperlegible** (la del sitio y la app, licencia OFL) y su versión monoespaciada, incrustadas en el ensamblado; si no se consiguen los TTF, Lato, que ya trae QuestPDF.

## Cómo verlo o cambiarlo

Abre un `.html` en el navegador. Para sacar un PDF: Imprimir → Guardar como PDF, tamaño carta, sin márgenes y con gráficos de fondo. Los `.png` son capturas de esos mismos archivos.
