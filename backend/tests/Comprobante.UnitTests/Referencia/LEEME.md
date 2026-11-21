# Archivos de referencia

Cada escenario son tres archivos con el mismo nombre:

| Archivo | Qué es |
| --- | --- |
| `<escenario>.solicitud.json` | La solicitud que recibe `POST /api/comprobantes`. |
| `<escenario>.xml` | El CFDI 4.0 sellado que debe salir de esa solicitud, byte por byte. |
| `<escenario>.cadena.txt` | Su cadena original. |

El XML y la cadena **no los produce Comprobante**: los hace `generar_referencia.py` con una implementación independiente.

| Pieza | Comprobante (C#) | Referencia (Python) |
| --- | --- | --- |
| Importes, impuestos y totales | `decimal` y `Calculador` | `Decimal` y las mismas reglas del Anexo 20, reescritas |
| XML | `XmlWriter` | cadenas de texto con el mismo formato |
| Cadena original | `XslCompiledTransform` (System.Xml) | libxslt (lxml) con el mismo XSLT del SAT |
| Sello | `System.Security.Cryptography` | OpenSSL (`cryptography`) con la llave del CSD de prueba |
| Esquema (XSD) | `XmlSchemaSet` | libxml2 (lxml); el script no escribe un archivo que no lo cumpla |

Como el sello RSASSA-PKCS1-v1_5 es determinista, `ReferenciaTests` exige que el XML generado sea idéntico al de aquí,
sello incluido, y que el validador de C# acepte el XML que firmó OpenSSL. Si dos implementaciones distintas coinciden en
el byte, lo más probable es que ninguna tenga un error de implementación; los errores de interpretación del Anexo 20 que
comparta quien escribió las dos solo los descubre el SAT, por eso los esquemas y el XSLT son los oficiales.

## Regenerarlos

Hace falta Python 3 con `lxml` y `cryptography`. Desde la raíz del repositorio:

```
python3 backend/tests/Comprobante.UnitTests/Referencia/generar_referencia.py
```

Sirve para agregar un escenario (un `<nombre>.solicitud.json` nuevo) o cuando cambian los esquemas, el XSLT o los
certificados de prueba del SAT. Si cambian archivos que ya estaban, `git diff` muestra qué cambió en el XML.

Cada escenario cubre algo distinto: `ingreso-basico` (IVA 16 %), `ingreso-retenciones` (persona física con retenciones de
ISR e IVA, tasa 0 %, exento, descuento y varios conceptos), `ingreso-dolares` (moneda USD con tipo de cambio, cantidad con
seis decimales, `&` en el RFC y el nombre), `egreso-relacionado` (egreso con CFDI relacionado y `Ñ` en el RFC) y
`publico-en-general` (RFC genérico con información global).
