# Capturas del README

`inicio.png`, `inicio-oscuro.png`, `generar.png`, `resultado.png` y `validar.png` salen de la app corriendo, no de un montaje. Las toma [`tomar-capturas.mjs`](tomar-capturas.mjs) con Edge o Chrome sin interfaz, hablándole por el protocolo DevTools. No instala nada: solo necesita Node.js 22 o más reciente (el proyecto usa el 24).

```bash
docker compose up -d --build --wait                          # la app en http://localhost:8081
node docs/capturas/tomar-capturas.mjs                        # o con otra dirección: ... http://localhost:4200
```

Si no encuentra el navegador, dile cuál con la variable `NAVEGADOR` (la ruta del ejecutable).

## Qué hace

Abre cada pantalla con un ancho de 1200 px y al doble de resolución (las imágenes miden 2400 px de ancho), en claro u oscuro según la captura. Usa los ejemplos que ya trae la app, así que todo sale siempre igual: «Con retenciones y descuento» en Generar y «Un CFDI con errores» en Validar. En el resultado de Generar abre la cadena original para que se vea.

- `inicio.png` e `inicio-oscuro.png`: la página entera.
- `generar.png`: de arriba hasta el final de la sección del emisor, con los totales en vivo.
- `resultado.png`: la sección «CFDI generado», después de pulsar «Generar CFDI».
- `validar.png`: la sección del reporte, con sus cinco hallazgos.

El único dato que cambia entre corridas es el campo `Fecha` del XML generado, que lleva la hora de ese momento. Los datos son los de los certificados de prueba del SAT y los de los ejemplos, no hay nada real.

El script se apoya en los textos de los botones de ejemplo y en las secciones `resultado` y `totales` de la interfaz; si se cambian, avisa qué no encontró y hay que ajustarlo.

## Peso de las imágenes

Al tomarlas, cada una pesa unos 400 KB. Las del repositorio se pasaron por una paleta de 256 colores, que las deja en unos 150 KB sin pérdida que se note en una interfaz de pocos colores. Es opcional:

```python
from PIL import Image

imagen = Image.open('generar.png').convert('RGB')
imagen.quantize(colors=256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).save('generar.png', optimize=True)
```
