# Certificados de prueba del SAT

Son diez Certificados de Sello Digital (CSD) de los que el SAT publica para probar el timbrado:
[Certificados_de_Prueba.zip](http://omawww.sat.gob.mx/tramitesyservicios/Paginas/documentos/Certificados_de_Prueba.zip).
Son públicos, la contraseña de todas las llaves es `12345678a` y **no valen ante el SAT**: solo
sirven para firmar comprobantes de ejemplo. Comprobante no firma con ningún otro certificado.

Cada emisor son dos archivos con el mismo nombre: `<archivo>.cer` (el certificado, DER) y
`<archivo>.key` (la llave privada, PKCS#8 cifrada con la contraseña). `emisores.json` dice de quién es
cada uno, con el nombre, código postal y régimen fiscal tal como los registra el SAT para ese RFC
(salen de `Caracteristicas fiscales de RFC.xlsx`, que viene en el mismo zip).

| RFC | Nombre | Régimen | Para probar |
| --- | --- | --- | --- |
| EKU9003173C9 | ESCUELA KEMPER URGATE | 601 | persona moral, el caso base |
| H&E951128469 | HERRERIA & ELECTRICOS | 601 | `&` en el RFC y en el nombre (escape XML) |
| URE180429TM6 | UNIVERSIDAD ROBOTICA ESPAÑOLA | 601 | `Ñ` en el nombre |
| XIA190128J61 | XENON INDUSTRIAL ARTICLES | 622 | actividades agrícolas |
| L&O950913MSA | LUCES & OBRAS | 626 | RESICO, persona moral |
| WATM640917J45 | MARIA WATEMBER TORRES | 612 | persona física |
| KICR630120NX3 | RODRIGO KITIA CASTRO | 626 | RESICO, persona física |
| FUNK671228PH6 | KARLA FUENTE NOLASCO | 621 | incorporación fiscal |
| IAÑL750210963 | LUIS IAN ÑUZCO | 608 | `Ñ` en el RFC y en el nombre |
| MISC491214B86 | CECILIA MIRANDA SANCHEZ | 612 | persona física |

Estos certificados vencen en mayo de 2027. Cuando el SAT publique unos nuevos, se baja el zip con
`sat/descargar.ps1` (queda en `sat/origen/`) y se vuelve a elegir la selección.
