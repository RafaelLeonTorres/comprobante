# Capturas del README

`inicio.png`, `inicio-oscuro.png`, `generar.png`, `resultado.png`, `dialogo-pdf.png` y `validar.png` salen de la app corriendo, no de un montaje. Las toma [`tomar-capturas.mjs`](tomar-capturas.mjs) con Edge o Chrome sin interfaz, hablándole por el protocolo DevTools. No instala nada: solo necesita Node.js 22 o más reciente (el proyecto usa el 24).

```bash
docker compose up -d --build --wait                          # la app en http://localhost:8081
node docs/capturas/tomar-capturas.mjs                        # o con otra dirección: ... http://localhost:4200
```

Si no encuentra el navegador, dile cuál con la variable `NAVEGADOR` (la ruta del ejecutable).

## Qué hace

Abre cada pantalla con un ancho de 1200 px y al doble de resolución (las imágenes miden 2400 px de ancho), en claro u oscuro según la captura. Usa los ejemplos que ya trae la app, así que todo sale siempre igual: «Con retenciones y descuento» en Generar y «Un CFDI con errores» en Validar. En el resultado de Generar abre la cadena original para que se vea, y de ahí abre también el cuadro del PDF.

- `inicio.png` e `inicio-oscuro.png`: la página entera.
- `generar.png`: de arriba hasta el final de la sección del emisor, con los totales en vivo.
- `resultado.png`: la sección «CFDI generado», después de pulsar «Generar CFDI», con sus botones de descargar y copiar.
- `dialogo-pdf.png`: el cuadro «Descargar PDF» abierto sobre ese resultado, con las opciones del logo y de la cadena y los sellos.
- `validar.png`: la sección del reporte, con sus cinco hallazgos y el botón del PDF apagado mientras el XML tenga errores.

El único dato que cambia entre corridas es el campo `Fecha` del XML generado, que lleva la hora de ese momento. Los datos son los de los certificados de prueba del SAT y los de los ejemplos, no hay nada real.

El script se apoya en los textos de los botones de ejemplo, en el botón «Descargar PDF» y en las secciones `resultado` y `totales` de la interfaz; si se cambian, avisa qué no encontró y hay que ajustarlo.

## Las hojas del PDF

`pdf-sin-timbre.png` y `pdf-con-timbre.png` no salen del navegador: son la primera hoja del PDF que dibuja la API para el ejemplo «Con retenciones y descuento», pasada a imagen. Las muestras las escribe una prueba que solo corre si se le dice dónde guardarlas, y la hoja se pasa a imagen con cualquier lector de PDF; aquí se usó `pdftoppm`, de poppler:

```bash
MUESTRAS_PDF=/tmp/muestras dotnet test backend/tests/Comprobante.UnitTests --filter MuestrasDePdf
pdftoppm -png -r 120 -f 1 -l 1 -singlefile /tmp/muestras/sin-timbre-ingreso-retenciones.pdf pdf-sin-timbre
pdftoppm -png -r 120 -f 1 -l 1 -singlefile /tmp/muestras/con-timbre-ingreso-retenciones.pdf pdf-con-timbre
```

La prueba escribe también las demás muestras (con logo PNG y JPEG, sin cadena ni sellos, de 70 conceptos) para revisarlas a ojo. El timbre de `pdf-con-timbre.png` es inventado, con el UUID de prueba de las pruebas automáticas: la app no timbra, y el PDF solo muestra lo que trae el XML. Los datos del emisor y del receptor son los de los certificados de prueba del SAT.

## Peso de las imágenes

Al tomarlas, cada una pesa unos 400 KB. Las del repositorio se pasaron por una paleta de 256 colores, que las deja en unos 150 KB sin pérdida que se note en una interfaz de pocos colores (las dos hojas del PDF, en unos 110 KB). Es opcional:

```python
from PIL import Image

imagen = Image.open('generar.png').convert('RGB')
imagen.quantize(colors=256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).save('generar.png', optimize=True)
```
