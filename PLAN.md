# Plan: generador y validador de XML CFDI 4.0 (sin timbrado)

Aplicación web y librería .NET para armar, sellar y validar comprobantes fiscales CFDI 4.0 sin timbrarlos. Calcula importes e impuestos con las reglas de redondeo del Anexo 20, genera el XML, lo sella con un certificado de prueba del SAT y revisa cualquier XML contra los esquemas y las reglas de validación del SAT, con el código de error de cada falla. Es un proyecto de portafolio: pequeño de mantener y pensado para mostrar dominio del estándar mexicano de facturación, buenas prácticas con .NET y Angular, y pruebas.

> **Nombre de trabajo: Comprobante** (el nodo raíz del CFDI). En tu sitio aparece como "Generador y validador de XML CFDI 4.0 (sin timbrado)". El nombre y el repositorio quedaron confirmados (sección 14).

> **Sin validez fiscal.** La app no timbra ni envía nada al SAT ni a un PAC: los XML que genera no son facturas válidas. Lo dicen la pantalla, el README y el nombre del archivo descargado (`…-prueba.xml`).

Dónde vive cada cosa:

- **Este plan:** `PLAN.md`, en la raíz del repositorio. Nació en `C:\Users\rafik\Downloads\Claude\ProyectosPortafolio\Generador y validador de XML CFDI 4.0 (sin timbrado)`; esta copia es la que se mantiene al día.
- **Código:** `F:\Respaldo\Respaldo\Cursos\Comprobante`, junto a Mediateca.
- **Repositorio:** `https://github.com/RafaelLeonTorres/comprobante` (público).

---

## 1. Alcance

### Incluye (MVP)

- **Generar** CFDI 4.0 de ingreso y de egreso: emisor, receptor (incluido "público en general" con `InformacionGlobal`), conceptos con sus impuestos (IVA, IEPS, retenciones de ISR e IVA, exentos y tasa 0), descuentos, CFDI relacionados, moneda y tipo de cambio.
- **Calcular** importes, impuestos y totales con `decimal`, con los decimales de cada moneda y los límites de redondeo del Anexo 20.
- **Sellar** con los Certificados de Sello Digital (CSD) de prueba que publica el SAT: cadena original con el XSLT oficial y sello RSA con SHA-256.
- **Validar** cualquier XML CFDI 4.0: esquemas XSD del SAT, reglas de la matriz de errores (catálogos, cálculos y relaciones entre campos) y el sello contra su certificado. Cada falla trae su código del SAT (por ejemplo, `CFDI40161`), el nodo y un mensaje en español.
- **Catálogos del SAT** con la fecha de su versión, para las validaciones y para buscar claves: productos y servicios, unidades, códigos postales, usos de CFDI, regímenes, formas de pago, monedas, tasas o cuotas…
- **Ver** el XML formateado, la cadena original, el sello y los datos del certificado, y descargar el XML.

### No incluye (extensiones posibles)

- Timbrado (PAC y Timbre Fiscal Digital), cancelación y consulta del estatus en el SAT.
- Validaciones que dependen de servicios del SAT: que el RFC exista y que su nombre, régimen y código postal coincidan con el padrón, o que el CSD esté vigente en la lista del SAT. La app revisa forma y consistencia, no existencia, y lo dice.
- Complementos: Pagos 2.0 (la extensión más útil), Carta Porte, Nómina y Comercio Exterior. Un XML con complementos se valida en su parte CFDI y los complementos aparecen como "no revisados".
- Representación impresa en PDF, CFDI de retenciones y addendas.
- Firmar con el CSD propio del usuario en la app: una llave privada real no debe subir a un servidor. La librería sí lo permite para uso local (extensión: CLI).

---

## 2. Tecnologías

| Capa | Tecnología |
|---|---|
| Núcleo | .NET 11 (C# 15): `System.Xml` (`XmlWriter`, `XmlSchemaSet`, `XslCompiledTransform`) y `System.Security.Cryptography` (RSA, SHA-256, X.509). Sin dependencias externas |
| API | ASP.NET Core 11 con minimal APIs, OpenAPI y Scalar en desarrollo |
| Catálogos | PostgreSQL 18 con Entity Framework Core 11 (Npgsql): tablas, migraciones y búsqueda con índices `pg_trgm`. Los CSV versionados en el repositorio son la fuente y se cargan a la base al arrancar. Importador del Excel del SAT con ExcelDataReader |
| Frontend | Angular 22 (última 22.x), Angular Material, zoneless, Signal Forms y Vitest |
| Pruebas | xUnit, WebApplicationFactory con Testcontainers (un PostgreSQL real y desechable) y archivos de referencia (XML y cadenas originales esperadas); Vitest en Angular |
| Infraestructura | Docker Compose con tres contenedores (nginx con Angular, la API y PostgreSQL) y GitHub Actions |

Notas:

- Mismas versiones que Mediateca: .NET 11 salió el 10 de noviembre de 2026 y Angular 22 es la vigente. La única base de datos es la de los catálogos del SAT, que la API solo lee: del comprobante que el usuario escribe o sube no se guarda nada.
- **Catálogos en PostgreSQL (decisión del 21 de noviembre de 2026).** El plan inicial los cargaba en memoria desde los CSV. Se cambió a una base de datos con EF Core para mostrar modelado, migraciones y consultas con índices. El costo es un contenedor más y una carga inicial de unos 14 segundos (151 346 filas) la primera vez que arranca.
- **CFDI 4.0 sigue siendo la única versión válida** (lo es desde el 1 de abril de 2023). En 2026 lo que cambió fueron la guía de llenado, los catálogos y algunas validaciones complementarias, no la estructura del XML.
- **Los catálogos cambian varias veces al año.** Por ejemplo, el 3 de septiembre de 2025 (productos y servicios, pedimentos y patentes aduanales) y el 2 de marzo de 2026 (pedimentos). Por eso la versión de los catálogos se ve en la app y actualizarlos es correr el importador.
- **XSLT 2.0 contra 1.0:** los XSLT del SAT se declaran como XSLT 2.0, pero usan funciones compatibles con 1.0, y otras librerías los procesan con motores 1.0. `XslCompiledTransform` implementa XSLT 1.0, así que las pruebas comparan las cadenas originales contra las que calcula aparte libxslt (lxml) con los mismos XSLT. Comprobado en la fase 2: coinciden en los cinco comprobantes de referencia (acentos, `&`, `Ñ`, retenciones, exentos, relacionados e información global). Si un XSLT nuevo usara funciones exclusivas de 2.0, el plan B es armar la cadena original en C#.

---

## 3. Arquitectura

```mermaid
flowchart LR
  subgraph Navegador
    UI[Angular 22]
  end
  subgraph Compose["Docker Compose"]
    WEB["web: nginx con el build de Angular"]
    subgraph APIC["api: ASP.NET Core 11"]
      API[Endpoints]
      CORE["Comprobante.Core: cálculo, XML, cadena original, sello y validador"]
      CAT["Comprobante.Catalogos: EF Core"]
    end
    DB[("db: PostgreSQL 18, catálogos del SAT")]
  end
  SAT[["sat/: XSD, XSLT, CSD de prueba y catálogos en CSV, versionados"]]
  UI -- "páginas y /api" --> WEB
  WEB -- "/api" --> API
  API --> CORE
  API --> CAT
  CORE -- "ICatalogos" --> CAT
  CAT --> DB
  CAT -. "siembra si la huella cambió" .-> SAT
  CORE -. "esquemas y XSLT" .-> SAT
```

Decisiones principales:

- **Sin estado del usuario.** La API procesa cada solicitud en memoria y no guarda ni registra el XML, porque un CFDI trae RFC, nombres y montos. La única base de datos guarda los catálogos del SAT y la API solo la lee.
- **Catálogos en PostgreSQL con EF Core.** Los 16 catálogos chicos se leen una vez y se guardan en memoria; los tres grandes (códigos postales, productos y servicios, y unidades) se consultan por clave, y la búsqueda de texto usa índices `pg_trgm`. La siembra es idempotente: compara la huella SHA-256 de los CSV con la de la última carga y, con un candado de PostgreSQL por si arrancan dos instancias a la vez, solo recarga si cambió.
- **Un núcleo sin paquetes de terceros.** `Comprobante.Core` define `ICatalogos` y no sabe de EF ni de PostgreSQL; se puede usar como librería (y publicar en NuGet). La API y la interfaz son capas delgadas encima. El cálculo vive solo en C#: Angular pide la vista previa de los totales a la API en lugar de duplicar las reglas.
- **Los archivos del SAT, versionados.** Los XSD (`cfdv40.xsd`, `catCFDI.xsd`, `tdCFDI.xsd`), el XSLT de la cadena original con sus `xsl:include`, los CSD de prueba y los catálogos viven en `sat/`, con la fecha en que se descargaron. Un `XmlResolver` propio traduce las direcciones `http://www.sat.gob.mx/sitio_internet/…` a esos archivos: nada se descarga en tiempo de ejecución y las pruebas son reproducibles.
- **Reglas con su código del SAT.** El validador es una lista de reglas pequeñas. Cada una tiene el código de la matriz de errores, el nodo que revisa y su mensaje en español, y se prueba por separado.
- **El generador pasa por el validador.** Todo lo que se genera se valida antes de entregarse; si aparece un hallazgo, la respuesta lo dice en lugar de presentar el XML como correcto.
- **Un solo origen**, como Mediateca: nginx sirve Angular y pasa `/api` a la API.

Estructura de carpetas:

```text
Comprobante/                            F:\Respaldo\Respaldo\Cursos\Comprobante (raíz del repositorio)
├── PLAN.md                             este plan
├── README.md                           qué hace, cómo levantarlo y cómo actualizar los archivos del SAT
├── docker-compose.yml                  web, api y db
├── .env.example                        puertos y contraseña de la base (distintos a los de Mediateca)
├── sat/
│   ├── descargar.ps1, fuentes.json     de dónde sale cada archivo, cuándo se bajó y su huella
│   ├── origen/                         lo que baja descargar.ps1 (el Excel de catálogos, la matriz de errores y PDF); no va a git
│   ├── esquemas/                       cfdv40.xsd, catCFDI.xsd, tdCFDI.xsd y TimbreFiscalDigital
│   ├── xslt/                           cadenaoriginal_4_0.xslt y los XSLT que incluye
│   ├── csd-prueba/                     .cer, .key y emisores.json de los CSD de prueba del SAT (son públicos)
│   ├── catalogos/                      un CSV por catálogo y version.json con la fecha de publicación
│   └── matriz/                         matriz-errores.csv: las 129 reglas con su código y su mensaje
├── backend/
│   ├── Dockerfile
│   ├── Comprobante.slnx
│   ├── src/
│   │   ├── Comprobante.Core/           solicitud, cálculo y redondeo, XML, cadena original, sello, validador y reglas
│   │   ├── Comprobante.Catalogos/      EF Core: modelo, migraciones, siembra y consultas
│   │   ├── Comprobante.Herramientas/   importador del Excel del SAT (consola)
│   │   └── Comprobante.Api/            endpoints, límites y OpenAPI
│   └── tests/
│       ├── Comprobante.UnitTests/      incluye Referencia/: XML sellados y cadenas originales hechos con Python, lxml y OpenSSL
│       └── Comprobante.IntegrationTests/   la API contra un PostgreSQL desechable (Testcontainers)
└── frontend/                           Angular 22
    ├── Dockerfile
    └── nginx.conf
```

---

## 4. Modelo

El comprobante no se guarda: su modelo son registros inmutables de C#. Lo único que vive en PostgreSQL son los catálogos del SAT (sección 7).

| Tipo | Contenido |
|---|---|
| `SolicitudCfdi` | Lo que llena el usuario, en JSON: Serie, Folio, Fecha, FormaPago, MetodoPago, CondicionesDePago, Moneda, TipoCambio, TipoDeComprobante, Exportacion, LugarExpedicion; Emisor, Receptor, InformacionGlobal, CfdiRelacionados y Conceptos. Se normaliza antes de calcular (mayúsculas, moneda MXN y tipo I por omisión, `ObjetoImp` inferido) |
| `Cfdi` y sus nodos | El comprobante tal como está en el XML, con todos los valores en texto: lo que escribe el generador y lee el validador (Version 4.0, y los nodos de abajo) |
| `Emisor` | Rfc, Nombre, RegimenFiscal, FacAtrAdquirente |
| `Receptor` | Rfc, Nombre, DomicilioFiscalReceptor, ResidenciaFiscal, NumRegIdTrib, RegimenFiscalReceptor, UsoCFDI |
| `Concepto` | ClaveProdServ, NoIdentificacion, Cantidad, ClaveUnidad, Unidad, Descripcion, ValorUnitario, Descuento, ObjetoImp, traslados y retenciones (Base, Impuesto, TipoFactor, TasaOCuota), ACuentaTerceros, InformacionAduanera y CuentaPredial |
| `ResultadoCalculo` | Importe e impuestos de cada concepto, impuestos agrupados por impuesto, tipo de factor y tasa o cuota, SubTotal, Descuento, totales de impuestos y Total, ya redondeados |
| `Hallazgo` | Código (del SAT o propio), severidad (error o advertencia), nodo, atributo, mensaje en español, y valor encontrado y esperado |
| `ReporteValidacion` | EsValido, hallazgos, cadena original, si el sello es válido y los datos del certificado (RFC, nombre, NoCertificado y vigencia) |
| `EmisorDePrueba` | RFC, nombre, régimen y código postal de un CSD de prueba, con su NoCertificado y vigencia. La llave y su contraseña no salen del servidor |

Detalles que vale la pena mostrar en el portafolio:

- **`decimal` siempre, nunca `double`.** Cada importe se calcula con hasta 6 decimales y se redondea al final, a los decimales de la moneda (`c_Moneda`), como recomienda el SAT. El importe de cada impuesto debe quedar entre un límite inferior y uno superior que el Anexo 20 calcula a partir de la base, la tasa y los decimales. El generador lo cumple y el validador lo revisa (por ejemplo, `CFDI40186` en retenciones). Las fórmulas son las del estándar técnico del Anexo 20 (DOF del 13 de enero de 2022), están en `Limites` y se prueban con los ejemplos de la guía de llenado. Los decimales cuentan como se escribieron: `1.50` tiene dos.
- **XML exacto:** prefijo `cfdi`, espacio de nombres `http://www.sat.gob.mx/cfd/4`, `xsi:schemaLocation` con la ruta oficial del esquema, atributos solo cuando tienen valor y números en formato invariable (punto decimal, sin separador de miles).
- **Exentos:** un traslado exento lleva Base, Impuesto y TipoFactor, sin TasaOCuota ni Importe.
- **Certificado:** `NoCertificado` sale del número de serie del CSD (el número de serie en hexadecimal codifica los 20 dígitos en ASCII) y `Certificado` es el `.cer` en Base64. La llave `.key` es PKCS#8 cifrada con contraseña y se lee con `RSA.ImportEncryptedPkcs8PrivateKey`, sin OpenSSL.

---

## 5. Generación y sello

1. **Entrada:** el comprobante llega en JSON. Primero se revisan los campos requeridos y las claves de catálogo; los errores vuelven por campo (`ValidationProblem`), en español.
2. **Cálculo:** importes por concepto, impuestos por concepto, resumen por impuesto, factor y tasa, y totales, con el redondeo y los límites de la sección 4.
3. **XML sin sello:** con `NoCertificado` y `Certificado` del emisor de prueba elegido.
4. **Cadena original:** se aplica el XSLT oficial del SAT con el resolver local.
5. **Sello:** firma RSA (PKCS#1 v1.5) con SHA-256 sobre los bytes UTF-8 de la cadena original, en Base64.
6. **Validación final:** el XML sellado pasa por el validador completo antes de entregarse.

La respuesta trae el XML, la cadena original, el sello, el cálculo y el reporte de validación. El archivo descargado se llama `{RFC}-{Serie}-{Folio}-prueba.xml` (con la fecha en lugar de la serie y el folio si no los hay; los caracteres que no son letras, números, guion o guion bajo, como el `&` de algunos RFC de prueba, pasan a guion bajo).

---

## 6. Validación

En este orden; si una capa falla de forma que impide seguir (XML mal formado, otra versión), el reporte se detiene ahí y lo explica:

1. **Lectura segura:** tamaño máximo, sin DTD ni entidades externas (contra XXE y bombas de entidades) y codificación UTF-8. Si no es un CFDI 4.0 (espacio de nombres o versión), lo dice en lugar de inundar el reporte de errores.
2. **Esquemas:** `cfdv40.xsd` con `catCFDI.xsd` y `tdCFDI.xsd`, más el del Timbre Fiscal Digital para aceptar XML ya timbrados. Los complementos sin esquema local se marcan como "no revisados".
3. **Reglas de la matriz de errores**, agrupadas:

   | Grupo | Ejemplos |
   |---|---|
   | Comprobante | `SubTotal` igual a la suma de importes; `Total` = SubTotal − Descuento + trasladados − retenidos; `MetodoPago` PPD exige `FormaPago` 99; `TipoCambio` de acuerdo con la moneda; `LugarExpedicion` en `c_CodigoPostal` |
   | Emisor y receptor | RFC con formato de persona física (13 caracteres) o moral (12); régimen que aplica al tipo de persona; `UsoCFDI` que corresponde al tipo de persona y al régimen del receptor (`CFDI40161`); público en general (`XAXX010101000`) con `InformacionGlobal`, régimen 616, uso S01 y domicilio igual a `LugarExpedicion`; `ResidenciaFiscal` cuando se registra `NumRegIdTrib` (`CFDI40153`) |
   | Conceptos | claves en `c_ClaveProdServ` y `c_ClaveUnidad`; `Importe` = Cantidad × ValorUnitario dentro de los límites; `ObjetoImp` 02 exige el nodo de impuestos |
   | Impuestos | `TasaOCuota` válida en `c_TasaOCuota` para el impuesto y el factor (valores fijos o rangos); exentos sin tasa ni importe; importes dentro de los límites; el resumen del comprobante coincide con la suma por impuesto, factor y tasa |
   | Sello y certificado | `Certificado` es un X.509 válido; `NoCertificado` coincide con su número de serie; la `Fecha` cae dentro de su vigencia; el sello verifica contra la cadena original con la llave pública |

4. **Advertencias** que no rechazaría un PAC pero conviene revisar, por ejemplo una `Fecha` en el futuro según la zona horaria del código postal de `LugarExpedicion` (el catálogo `c_CodigoPostal` la trae).

Los códigos y textos de cada regla se copian de la matriz de errores oficial del CFDI 4.0. Lo que no tiene código en la matriz (errores del XSD, advertencias) usa códigos propios con prefijo `CMP`: `CMP001` (XML ilegible), `CMP002` (no es un CFDI 4.0), `CMP003` (error del esquema), `CMP004` (complemento sin revisar, advertencia), `CMP005` (no se pudo calcular la cadena original, así que tampoco el sello), `CMP006` (hubo más de 300 hallazgos de reglas: se guardan los primeros y se cuenta el resto) y `CMP101` (fecha en el futuro, advertencia).

**Cobertura de la matriz.** La matriz trae 129 reglas. El validador revisa 103 completas y 5 en parte; las otras 21 no, porque necesitan datos que la app no tiene sin conexión: la lista de RFC inscritos (LRFC), el tipo de cambio oficial del DOF, la lista de contribuyentes obligados, los catálogos de aduanas y patentes, el límite del `Total` de la Resolución Miscelánea o lo que verifica el PAC al timbrar. Cada regla se publica con su estado y su motivo (`GET /api/validaciones/reglas`), así que ninguna se aprueba en silencio. Las cinco parciales:

- `CFDI40151` y `CFDI40154`: solo el RFC genérico extranjero (`XEXX010101000`) puede traer `ResidenciaFiscal` y `NumRegIdTrib`.
- `CFDI40164`: solo las claves que el catálogo marca con «Sí» para IVA o IEPS trasladado (el catálogo no trae retenciones por clave).
- `CFDI40189` y `CFDI40193`: solo con el RFC `EXT990101NI1` (nombre y domicilio).

`CFDI40209` tiene la misma condición que `CFDI40206` y se informa una sola vez, con ese código. Un error del esquema en el mismo nodo y atributo que una regla del SAT se descarta: queda el código de la matriz, que es el que explica qué corregir.

Un complemento que la app no trae en sus esquemas (Pagos, Nómina, Comercio Exterior...) no invalida el CFDI: el complemento queda sin revisar con una advertencia `CMP004`. Un CFDI ya timbrado se acepta porque el esquema del Timbre Fiscal Digital sí está cargado.

---

## 7. Catálogos

- **Fuente:** el Excel de catálogos del CFDI 4.0 que publica el SAT junto con el Anexo 20 (`catCFDI_V_4_20261120.xls`, que baja `sat/descargar.ps1` a `sat/origen/`).
- **Importador:** `Comprobante.Herramientas` lee el Excel con ExcelDataReader (también `.xls` antiguos), revisa lo que lee (claves repetidas, vigencias, columnas) y escribe un CSV por catálogo en `sat/catalogos/`, más `version.json` con el nombre del archivo y la fecha de publicación. Los CSV van a git: los cambios de una versión a otra se ven en el diff.
- **Base de datos:** `Comprobante.Catalogos` define las 19 tablas con la API fluida de EF Core (los tipos son clases planas de `Core`; nombres en `snake_case`) y las migraciones. Los CSV se incrustan en el ensamblado, así la API no necesita la carpeta `sat/` para sembrar.
- **Siembra:** al arrancar, la API aplica las migraciones y compara la huella SHA-256 de los CSV con la de la última carga (tabla `versiones_catalogos`). Si cambió, vacía y vuelve a llenar las tablas por lotes en una transacción, con `pg_advisory_xact_lock` para que dos instancias no se pisen; si no, no hace nada. La primera carga (151 346 filas) tarda unos 14 segundos.
- **Consulta:** los 16 catálogos chicos se leen una vez y se guardan en memoria (`CatalogosPequenos`) para que las reglas no vayan a la base; productos y servicios (52 513), unidades (2 418) y códigos postales (95 748) se consultan por clave. La búsqueda de texto usa `ILIKE` con índices GIN `pg_trgm`, y `%` y `_` se buscan como texto.
- **Relaciones** que usan las reglas: régimen fiscal y tipo de persona; uso de CFDI, tipo de persona y régimen del receptor; tasa o cuota, impuesto y factor; moneda y decimales; código postal, estado y zona horaria.
- **Visible:** `GET /api/catalogos` y la interfaz muestran la fecha de la versión. Una prueba de integración revisa que los 19 catálogos estén cargados y no vacíos, y que sembrar dos veces no duplique nada.

---

## 8. API

| Método y ruta | Para qué |
|---|---|
| `GET /api/health` | Saber si la API responde |
| `GET /api/emisores-prueba` | Emisores de los CSD de prueba del SAT: RFC, nombre, régimen y código postal |
| `POST /api/comprobantes/calculo` | Vista previa: recibe el comprobante en JSON y devuelve importes, impuestos y totales, con los errores de entrada por campo |
| `POST /api/comprobantes` | Genera y sella con el emisor de prueba elegido: `{ xml, cadenaOriginal, sello, noCertificado, calculo, validacion }` |
| `POST /api/validaciones` | Recibe el XML en el cuerpo (`application/xml` o `text/xml`, hasta 2 MB) y devuelve el reporte: `{ esValido, hallazgos, selloValido, cadenaOriginal, certificado }`. Un XML con errores es un 200 con sus hallazgos, no un error de la solicitud |
| `GET /api/validaciones/reglas` | Las 129 reglas de la matriz de errores y lo que la app hace con cada una (`revisada`, `parcial` o `noRevisada`, con su motivo) |
| `GET /api/catalogos` · `GET /api/catalogos/{nombre}?q=&pagina=&tamano=` | Versión y tamaño de los catálogos, y búsqueda paginada de claves |
| `GET /api/openapi/v1.json` · `GET /api/scalar` | La descripción OpenAPI y la página para probar la API |

- Errores con `ProblemDetails` en español: 400 con los errores por campo, 413 si el XML pasa del límite, 415 si no es XML y 429 si se excede el límite de solicitudes.
- La API se documenta con OpenAPI y se explora con Scalar, también en producción: no tiene nada privado. Todo vive bajo `/api`, así nginx lo pasa con una sola regla.

---

## 9. Frontend (Angular 22)

| Ruta | Pantalla |
|---|---|
| `/` | Qué hace, el aviso de que no timbra, los tres caminos (generar, validar o consultar los catálogos) y el estado del servicio |
| `/generar` | Formulario por secciones (comprobante, emisor de prueba, receptor, conceptos con sus impuestos), con buscadores de catálogos y totales en vivo. Al generar: XML con resaltado, cadena original, sello, certificado, resultado de la validación y "Descargar XML" |
| `/validar` | Soltar o elegir un XML. Reporte con los hallazgos agrupados por nodo y con su código del SAT, la cadena original y el estado del sello |
| `/catalogos` | Buscador de claves y fecha de la versión de los catálogos |

Prácticas, las mismas que en Mediateca:

- Componentes standalone, signals, `@if` y `@for`, rutas con carga diferida y Angular Material (Material 3) con diseño adaptable y modo oscuro.
- **Signal Forms** en todos los formularios, con los conceptos como arreglo. Las reglas que se repiten (RFC, código postal) viven en un solo lugar.
- `httpResource` leído siempre con `hasValue()` antes de `value()`.
- **Ejemplos precargados:** ingreso con IVA y retenciones, venta a público en general, factura en dólares y egreso relacionado con un ingreso. Sirven para la demo y para las capturas.
- El visor de XML resalta la sintaxis con un componente propio, sin librerías pesadas.

---

## 10. Seguridad

- **XML del usuario:** se lee con `DtdProcessing.Prohibit` y sin `XmlResolver`, contra XXE y bombas de entidades. El resolver local solo se usa para los archivos del SAT. El tamaño máximo se aplica en nginx y en la API.
- **Nada se guarda:** ni el XML ni los formularios se guardan o se registran. Los registros solo llevan tamaño, duración y resultado.
- **Solo CSD de prueba:** sus llaves y su contraseña son públicas, y la API nunca recibe llaves.
- **Límite de solicitudes** por IP en generar y validar, con el middleware de rate limiting.
- **Cabeceras en nginx:** `Content-Security-Policy`, `X-Content-Type-Options` y `server_tokens off`.
- **Sin cuentas ni sesiones:** no hay datos personales que proteger más allá de la solicitud en curso.

---

## 11. Pruebas

- **Unitarias (xUnit):**
  - cálculo y redondeo con muchos casos `[Theory]`: varios conceptos, descuentos, exentos, tasa 0, IEPS por cuota, retenciones y una moneda con otro número de decimales;
  - el XML generado contra archivos de referencia;
  - la cadena original contra las que calcula libxslt;
  - firma y verificación con los diez CSD de prueba, y el XML sellado idéntico byte por byte al que arma una implementación independiente (Python, lxml y OpenSSL; el sello PKCS#1 v1.5 es determinista);
  - cada regla con casos que pasan y que fallan;
  - el importador de catálogos con un Excel pequeño de ejemplo.
- **Integración (WebApplicationFactory):**
  - generar y luego validar ese XML: cero hallazgos;
  - un XML con errores sembrados devuelve sus códigos;
  - 413, 415, 429 y un XML con XXE rechazado;
  - los catálogos cargan en un PostgreSQL desechable (Testcontainers, necesita Docker) y la búsqueda responde.
- **Frontend (Vitest):** formulario de conceptos y totales, validador (archivo → reporte) y los servicios de la API.
- **CI (GitHub Actions):** compilar y probar el backend, probar y compilar Angular, y una prueba de humo que levanta los tres contenedores y consulta `/api/health` a través de nginx.

---

## 12. Configuración

```jsonc
// appsettings.json: sin secretos (los CSD de prueba son públicos, y la base de desarrollo es local)
{
  "ConnectionStrings": {
    "Catalogos": "Host=localhost;Port=5435;Database=comprobante;Username=comprobante;Password=comprobante-dev"
  },
  "Catalogos": {
    "SembrarAlIniciar": true       // aplica migraciones y carga los catálogos si cambiaron
  },
  "Limites": {
    "XmlMaximoBytes": 2097152,     // 2 MB, también en nginx
    "SolicitudesPorMinuto": 30
  }
}
```

- En Docker, los puertos y la contraseña de la base salen de `.env`: `WEB_PORT=8081`, `API_PORT=5081` y `DB_PORT=5435`, distintos a los de Mediateca para poder correr los dos proyectos a la vez. La API espera a que la base responda (`depends_on` con `service_healthy`) y su healthcheck da tiempo (`start_period`) a la primera carga de catálogos.
- Los archivos de `sat/` que usa la API (catálogos, esquemas, XSLT y CSD de prueba) viajan dentro de sus ensamblados como recursos: no hay rutas que configurar.
- Actualizar los archivos del SAT es un procedimiento documentado en el README: descargar, correr el importador (catálogos), correr las pruebas y hacer commit con la fecha de la versión. Al arrancar, la API detecta la nueva huella y recarga las tablas sola.

---

## 13. Fases

Cada fase entrega algo que funciona de punta a punta, con pruebas. Desde la fase 0 se trabaja con ramas y pull requests.

**Dónde vamos (21 de noviembre de 2026).** Hechas la fase 0 (backend, archivos del SAT, Angular, Docker Compose y CI; `docker compose up -d --build --wait` levanta los tres contenedores en unos 35 s con la base vacía), la fase 1 (importador, catálogos en PostgreSQL con EF Core, cálculo con los límites del Anexo 20 y `POST /api/comprobantes/calculo`), la fase 2 (XML, cadena original y sello: `POST /api/comprobantes` entrega el CFDI sellado con un CSD de prueba, que pasa el XSD y cuyo sello verifica) la fase 3 (el validador: `POST /api/validaciones` aplica el esquema, las reglas de la matriz de errores y el sello; 103 de las 129 reglas se revisan completas, 5 en parte y 21 no, con su motivo en `GET /api/validaciones/reglas`) y la fase 4 (Angular: desde el navegador se genera un CFDI con totales en vivo, se descarga, se valida con su reporte por nodo y se consultan los catálogos; probado a mano en escritorio, en una ventana de 782 px y en celular, en claro y oscuro). Van 355 pruebas unitarias y 50 de integración en el backend y 201 con Vitest en el frontend. Lo siguiente: la fase 5 (seguridad y pulido).

### Fase 0. Base del proyecto
- [x] Confirmar las decisiones de la sección 14.
- [x] Crear la carpeta del código y copiar este `PLAN.md` a la raíz.
- [x] `.gitignore`, `.editorconfig` y licencia MIT.
- [x] Solución .NET 11 con `Core`, `Catalogos`, `Herramientas`, `Api` y los dos proyectos de pruebas, con la versión más reciente de cada paquete.
- [x] App de Angular 22 con Angular Material (Material 3, claro u oscuro según el sistema; fuentes e iconos sin servidores externos) y proxy hacia la API. La pantalla de inicio muestra el estado de la API y la versión de los catálogos.
- [x] Descargar a `sat/` los XSD, el XSLT de la cadena original con sus includes, los CSD de prueba, el Excel de catálogos y la matriz de errores, y anotar la fecha y la huella de cada uno (`sat/descargar.ps1` y `sat/fuentes.json`).
- [x] Docker Compose con `web`, `api` y `db`, healthchecks y puertos en `.env`.
- [x] Repositorio público.
- [x] GitHub Actions (`.github/workflows/ci.yml`: backend con Testcontainers, frontend y los tres contenedores). Se estrena con el primer push.

**Entregable:** `docker compose up -d --build --wait` levanta `web`, `api` y `db`, y la página muestra que la API responde.

### Fase 1. Catálogos y cálculo
- [x] Importador del Excel del SAT a CSV y `version.json`.
- [x] Catálogos en PostgreSQL con EF Core: modelo, migración, siembra idempotente, relaciones entre catálogos y búsqueda (`GET /api/catalogos`).
- [x] Modelo de la solicitud y cálculo de importes, impuestos y totales, con redondeo por moneda y límites.
- [x] `POST /api/comprobantes/calculo`.
- [x] Pruebas: muchos casos de cálculo, el importador y la API contra un PostgreSQL real. Medido: la primera carga de 151 346 filas tarda unos 14 s; con la base al día, el arranque no carga nada.

**Entregable:** la API calcula un comprobante con varios conceptos e impuestos, y los totales cuadran.

### Fase 2. XML, cadena original y sello
- [x] Escritor del XML con el orden y el formato del esquema (`EscritorCfdi`, con la declaración de espacios de nombres en el orden de los ejemplos del SAT).
- [x] Validación XSD con los esquemas locales (`ValidadorXsd`; el esquema de 6 MB se compila una vez y los mensajes salen en español, con el nodo, el atributo y la línea).
- [x] Cadena original con el XSLT del SAT y el resolver local, comparada contra la que calcula libxslt (`sat/` ya no lleva `ejemplos/`: las pruebas usan `backend/tests/Comprobante.UnitTests/Referencia/`).
- [x] Lectura del CSD (`.cer` y `.key`), `NoCertificado`, firma y verificación, con los diez certificados de prueba.
- [x] `POST /api/comprobantes` y `GET /api/emisores-prueba`.
- [x] Pruebas: 116 unitarias nuevas (CSD, cadena original, XSD, sello, zona horaria, nombre de archivo y cinco comprobantes idénticos byte por byte a los de la implementación independiente) y 8 de integración.

**Entregable:** un CFDI de ingreso sellado con un CSD de prueba pasa el XSD y su sello verifica.

### Fase 3. Validador
- [x] Lectura segura del XML: sin DTD ni resolver, tope de 2 MB (también con el cuerpo sin longitud) y versión. Los errores de XML mal formado salen en español, con la línea (`TraductorXml`, `CMP001`).
- [x] Motor de reglas y reporte (`MotorDeReglas`, `ContextoValidacion`: los catálogos que usan las reglas se consultan una vez por XML).
- [x] Reglas de comprobante, emisor y receptor, conceptos, impuestos, complementos, y sello y certificado, con sus códigos de la matriz: de las 129, 103 completas, 5 parciales y 21 no revisadas, cada una con su motivo (`ReglasSat`).
- [x] `POST /api/validaciones` (`application/xml` y `text/xml`, charset declarado o UTF-8 con BOM; 400, 413 y 415 como `ProblemDetails`) y `GET /api/validaciones/reglas`.
- [x] Pruebas por regla y XML de ejemplo con errores conocidos: un error sembrado para cada una de 104 reglas (109 casos, cada uno firmado de nuevo para que solo falle lo sembrado), la cobertura de las 129 reglas, 21 XML rotos explicados, los avisos de fecha y complementos, y todos los XML de referencia sin falsos positivos. 162 pruebas unitarias nuevas y 17 de integración.

**Entregable:** un XML con errores sembrados devuelve cada error con su código, nodo y mensaje.

### Fase 4. Angular: generar y validar
- [x] Generador con Signal Forms, buscadores de catálogos y totales en vivo; resultado con XML, cadena original, sello y descarga. Lleva el comprobante, el emisor de prueba, el receptor, la información global (público en general), los CFDI relacionados y los conceptos con sus impuestos; los errores de la API caen sobre el campo que corresponde y el foco pasa al aviso o al resultado.
- [x] Validador con arrastrar y soltar (también elegir o pegar el XML) y reporte por nodo, con el código de la matriz, lo encontrado y lo esperado, y un salto a la línea del XML. Incluye la tabla de las reglas que revisa y las que no.
- [x] Ejemplos precargados: cinco para generar (servicio con IVA, retenciones y descuento, dólares, público en general y egreso con relacionado) y dos para validar (uno sin errores y otro con cinco).
- [x] Pantalla de catálogos: los 19 catálogos con su número de filas, búsqueda por clave o descripción, paginación y una URL que se puede compartir (`?catalogo=…&q=…&pagina=…`).
- [x] Pruebas con Vitest (201 en 21 archivos) y revisión en el navegador, en escritorio (1280 px), en una ventana de 782 px y en celular (375 px), en claro y oscuro. La revisión encontró y corrigió: ayudas cortadas en los campos, un renglón de descuento vacío (la API omite los valores nulos), impuestos mal acomodados al apilarse, una línea larga de XML que ensanchaba la página, títulos de resultado mal redactados y el anillo de foco de Material, que viene apagado.

**Entregable:** desde el navegador se genera un CFDI, se descarga y, al volver a validarlo, no tiene hallazgos. Verificado con los cinco ejemplos contra la API real (la descarga, sin guardar el archivo: nombre, tipo, tamaño y sello) y con uno de ellos subido a la pantalla Validar.

### Fase 5. Seguridad y pulido
- [ ] Límites de tamaño y de solicitudes, cabeceras de nginx y registros sin contenido.
- [ ] Pruebas de integración de seguridad: XXE, 413, 415 y 429.
- [ ] Revisión de accesibilidad y del modo oscuro. Adelantada en la fase 4 y verificada por código en el navegador: contraste AA de todo el texto en las cuatro pantallas, en claro y oscuro; anillo de foco visible en todos los controles de las cuatro pantallas; nombres accesibles, títulos sin saltos y regiones; enlace del pie con blanco de pulsación de 24 px. Falta probar con un lector de pantalla.

**Entregable:** las pruebas de seguridad pasan en la CI y la app se usa con teclado y lector de pantalla.

### Fase 6. Cierre para el portafolio
- [ ] README con descripción, capturas, diagrama, decisiones técnicas y cómo actualizar los archivos del SAT.
- [ ] Artículo en el sitio, en `_posts/es` y `_posts/en`, con `repo` en el front matter.
- [ ] En `_data/projects.yml`, agregar al proyecto `url`, `status: done` y `post`.
- [ ] Decidir si hay demo pública (sección 14).

**Entregable:** el proyecto aparece terminado en el sitio, con su artículo y el enlace al código.

### Extensiones (después del MVP)
- [ ] Complemento de Pagos 2.0: generar y validar.
- [ ] CLI: `comprobante validar factura.xml` y `comprobante sellar` con un CSD propio, en la computadora del usuario.
- [ ] Representación impresa en PDF.
- [ ] Publicar `Comprobante.Core` en NuGet.
- [ ] Validar el RFC con la librería del proyecto "Validador de CURP y RFC".

---

## 14. Decisiones

Confirmadas el 21 de noviembre de 2026:

1. **Nombre y repositorio:** Comprobante, en `RafaelLeonTorres/comprobante`. Es corto, en español y no usa la marca de nadie.
2. **Carpeta del código:** `F:\Respaldo\Respaldo\Cursos\Comprobante`, junto a Mediateca.
3. **Tipos de comprobante del MVP:** ingreso y egreso. Traslado y pagos (con el complemento 2.0) quedan como extensión.
4. **Firma:** en la app, solo con los CSD de prueba del SAT. El CSD propio, solo en la CLI y en la computadora del usuario.
5. **Catálogos:** PostgreSQL con EF Core, a petición del dueño del proyecto, en lugar de CSV en memoria. Los CSV siguen en el repositorio como fuente y se siembran a la base. Lo que se gana es mostrar modelado, migraciones, índices y pruebas contra una base real; lo que cuesta, un contenedor más.
6. **Demo pública:** se decide en la fase 6. Mientras tanto se corre con Docker en local. Si se publica, solo con CSD de prueba y con los límites de la sección 10.

---

## 15. Criterios de aceptación del MVP

1. `docker compose up -d --build --wait` levanta la web, la API y la base de datos sin configurar nada.
2. Un CFDI de ingreso con IVA 16 % y retenciones de ISR e IVA, generado en la app, pasa la validación XSD con los esquemas oficiales, su cadena original coincide con la del XSLT del SAT y su sello verifica con el certificado de prueba.
3. Los cálculos cuadran en los casos de prueba (descuentos, exentos, tasa 0 y otra moneda) y los importes de los impuestos quedan dentro de los límites.
4. Un XML con errores sembrados reporta cada uno con su código de la matriz, el nodo y un mensaje en español.
5. La API no guarda ni registra el contenido de los XML.
6. Las pruebas de backend y frontend pasan en GitHub Actions.

---

## 16. Riesgos

| Riesgo | Cómo se mitiga |
|---|---|
| El SAT actualiza catálogos y validaciones varias veces al año | Catálogos versionados con su fecha, importador, prueba en la CI y la versión visible en la app |
| Los XSLT del SAT se declaran como XSLT 2.0 y .NET implementa XSLT 1.0 | Usan funciones compatibles con 1.0; pruebas contra cadenas originales conocidas; plan B: armar la cadena original en C# |
| Diferencias de centavos por redondeo | `decimal`, un solo módulo de cálculo, muchos casos de prueba, y el validador revisa lo que genera el generador |
| XML malicioso: XXE, bombas de entidades o archivos enormes | Lectura sin DTD ni resolver, y límite de tamaño en nginx y en la API |
| Que alguien confunda el XML con una factura válida | Aviso en la app, en el README y en el nombre del archivo; la app no timbra |
| Los CSD de prueba vencen (el 18 de mayo de 2027) o el SAT los reemplaza | Versionados con su vigencia, que `GET /api/emisores-prueba` muestra; el README explica cómo cambiarlos y, para regenerar `Referencia/`, un script |
| Carga y consulta de los catálogos grandes | PostgreSQL con índices `pg_trgm`; siembra idempotente con huella y candado; healthcheck con `start_period` para la primera carga (unos 14 s, medida en la fase 1) |
| Las pruebas de integración y la CI necesitan Docker | Testcontainers levanta un PostgreSQL desechable; GitHub Actions ya trae Docker |
| Reglas que dependen del padrón del SAT (nombre, régimen y código postal reales del receptor) | Quedan fuera (21 de las 129 reglas) y la app lo explica: valida forma y consistencia, no existencia. `GET /api/validaciones/reglas` las lista con su motivo |

---

## 17. Fuentes

- Esquema del CFDI 4.0, en la ruta que exige el Anexo 20: <http://www.sat.gob.mx/sitio_internet/cfd/4/cfdv40.xsd> (espacio de nombres `http://www.sat.gob.mx/cfd/4`).
- CSD de prueba del SAT (dónde se publican), en el Anexo 29 de la RMF: <https://www.sat.gob.mx/minisitio/NormatividadRMFyRGCE/documentos2025/rmf/anexos/Primera_Modificacion_Anexo29_DOF-03112025.pdf>
- Cambios del CFDI en 2026 (la versión 4.0 sigue vigente; guía de llenado del 16 de enero de 2026): <https://www.soyconta.com/?p=20188>
- Guía de llenado actualizada al 29 de diciembre de 2025, sin cambios en la estructura del XML: <https://contadormx.com/cambios-al-cfdi-4-0-en-2026-actualizacion-del-sat/>
- Actualización de catálogos del 3 de septiembre de 2025: <https://www.elcontribuyente.mx/2025/09/nuevos-catalogos-del-sat-evita-rechazos-de-timbrado-con-estos-nuevos-ajustes/>
- Actualización de catálogos del 2 de marzo de 2026: <https://www.vatupdate.com/2026/03/17/sat-updates-cfdi-4-0-catalogs-march-2026-changes-affect-customs-records-no-schema-modifications/>
- Matriz de errores del CFDI 4.0, ejemplos de códigos: <https://www.satfacil.com.mx/errores-sat/cfdi40161>, <https://www.satfacil.com.mx/errores-sat/cfdi40153>
- Anexo 20 de la RMF para 2022 (DOF del 13 de enero de 2022), estándar técnico del CFDI 4.0: de ahí salen las fórmulas de los límites inferior y superior del importe de cada concepto y de cada impuesto: <https://www.gncys.com/anexo20/docs/Anexo20_2022.pdf>
- Manejo de decimales y redondeo recomendado por el SAT: <https://contadormx.com/?p=26352>
- Preguntas frecuentes del CFDI 4.0 (impuestos por concepto y exentos): <https://cfdi.com.mx/help/details/PregFrecCFDIVer4_0.pdf>
- Sello con RSASSA-PKCS1-v1_5 y SHA-256 sobre la cadena original de `cadenaoriginal_4_0.xslt`: <https://facturama.mx/docs/es/herramientas/validar-xml>
- Los XSLT del SAT se declaran 2.0 pero funcionan con motores 1.0: <https://mintlify.wiki/eclipxe13/CfdiUtils/components/cadena-origen>
- Ejemplos de CFDI 4.0 con su cadena original verificada (útiles para las pruebas): <https://cryptosys.net/firmasat/cfdv40-examples.html>
- Versiones de .NET 11 y Angular 22: las mismas fuentes que el plan de Mediateca.
