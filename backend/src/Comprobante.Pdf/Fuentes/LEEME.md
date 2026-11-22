# Fuentes del PDF

El PDF se dibuja con **Atkinson Hyperlegible Next** (texto) y **Atkinson Hyperlegible Mono** (cifras de los sellos, folios y
claves), del [Braille Institute](https://www.brailleinstitute.org/freefont/). Se escogieron porque sus letras están diseñadas
para distinguirse unas de otras (la `l` minúscula, la `I` mayúscula y el `1`; el `0` con diagonal), y eso ayuda a
quien lee una cadena original o un folio fiscal a ojo.

| Archivo | Uso |
|---|---|
| `AtkinsonHyperlegibleNext-Regular.ttf` | texto normal |
| `AtkinsonHyperlegibleNext-SemiBold.ttf` | rótulos y encabezados de tabla |
| `AtkinsonHyperlegibleNext-Bold.ttf` | valores destacados |
| `AtkinsonHyperlegibleNext-ExtraBold.ttf` | el título y el total |
| `AtkinsonHyperlegibleMono-Regular.ttf` | folios, claves, sellos y cadena original |
| `AtkinsonHyperlegibleMono-Bold.ttf` | el folio fiscal (UUID) |

Licencia: **SIL Open Font License 1.1** (los textos están junto a las fuentes: `OFL-Atkinson-Hyperlegible-Next.txt` y
`OFL-Atkinson-Hyperlegible-Mono.txt`). Se pueden usar, copiar y redistribuir, también dentro de un producto, siempre que se
conserve el aviso de licencia y no se vendan por separado.

## Cómo se usan

Van **incrustadas como recursos del ensamblado** (`fuentes/<archivo>`, ver `Comprobante.Pdf.csproj`) y `MotorPdf` las
registra al arrancar. QuestPDF está configurado con `UseSystemFonts = false`, así que el PDF sale igual en Windows, Linux y
dentro del contenedor, sin depender de las fuentes que haya instaladas. Cada PDF lleva solo los glifos que usa (subconjunto).

Atkinson cubre latín, griego y cirílico. Un carácter que no tiene (un emoji, un ideograma, el árabe) sale como un
cuadrito en lugar de detener la generación.

## Cambiar o agregar una fuente

1. Copia el `.ttf` aquí y agrega su licencia.
2. Declara el archivo en `Comprobante.Pdf.csproj` (los `.ttf` de esta carpeta ya entran como `EmbeddedResource`).
3. Regístrala en `MotorPdf` y usa el nombre de la familia tal como lo declara el archivo.
