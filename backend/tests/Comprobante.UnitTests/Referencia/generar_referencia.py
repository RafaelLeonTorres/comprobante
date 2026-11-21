#!/usr/bin/env python3
"""
Genera los archivos de referencia de las pruebas: para cada escenario, el XML sellado y su cadena original, hechos con una
implementación independiente de la de C# (Python, lxml y cryptography). Las pruebas generan los mismos comprobantes con
Comprobante y exigen que el XML sea idéntico, byte por byte, sello incluido: la firma RSASSA-PKCS1-v1_5 es determinista.

Qué valida cada pieza:
  * El cálculo de importes e impuestos se repite aquí con Decimal y las reglas del Anexo 20.
  * La cadena original sale del XSLT oficial del SAT con libxslt (el motor de lxml), no con System.Xml.
  * El sello lo firma OpenSSL (vía cryptography) con la llave del CSD de prueba.
  * El XML se valida con el XSD oficial con libxml2.

Uso (desde la raíz del repositorio):
    python3 backend/tests/Comprobante.UnitTests/Referencia/generar_referencia.py
"""
import base64
import json
import re
import shutil
import sys
import tempfile
from decimal import ROUND_HALF_UP, Decimal
from pathlib import Path

from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding
from lxml import etree

AQUI = Path(__file__).resolve().parent
RAIZ = AQUI.parents[3]
SAT = RAIZ / "sat"

ESPACIO_CFDI = "http://www.sat.gob.mx/cfd/4"
ESPACIO_XSI = "http://www.w3.org/2001/XMLSchema-instance"
UBICACION = "http://www.sat.gob.mx/cfd/4 http://www.sat.gob.mx/sitio_internet/cfd/4/cfdv40.xsd"


# ---------------------------------------------------------------------------------------------------------------------
# Números: las mismas reglas que Calculador y Formato en C#
# ---------------------------------------------------------------------------------------------------------------------
def redondear(valor: Decimal, decimales: int) -> Decimal:
    return valor.quantize(Decimal(1).scaleb(-decimales), rounding=ROUND_HALF_UP)


def decimales_de(valor: Decimal) -> int:
    n = 0
    while redondear(valor, n) != valor:
        n += 1
    return n


def fijo(valor: Decimal, decimales: int) -> str:
    return f"{redondear(valor, decimales):.{decimales}f}"


def importe(valor: Decimal, minimo: int) -> str:
    return fijo(valor, max(decimales_de(valor), minimo))


def calcular(solicitud: dict, decimales_moneda: int) -> dict:
    conceptos = []
    for c in solicitud["conceptos"]:
        imp = redondear(c["cantidad"] * c["valorUnitario"], 6)
        descuento = c.get("descuento")
        base_monetaria = imp - (descuento or Decimal(0))

        def impuesto(i, _c=c, _base=base_monetaria):
            base = i.get("base")
            if base is None:
                base = _c["cantidad"] if i["tipoFactor"] == "Cuota" else _base
            if i["tipoFactor"] == "Exento":
                return {"impuesto": i["impuesto"], "factor": "Exento", "tasa": None, "base": base, "importe": None}
            tasa = i["tasaOCuota"]
            return {"impuesto": i["impuesto"], "factor": i["tipoFactor"], "tasa": tasa, "base": base,
                    "importe": redondear(base * tasa, 6)}

        conceptos.append({
            "importe": imp,
            "descuento": descuento,
            "traslados": [impuesto(i) for i in c.get("traslados", [])],
            "retenciones": [impuesto(i) for i in c.get("retenciones", [])],
        })

    subtotal = redondear(sum(c["importe"] for c in conceptos), decimales_moneda)
    hay_descuento = any(c["descuento"] is not None for c in conceptos)
    descuento = redondear(sum((c["descuento"] or Decimal(0)) for c in conceptos), decimales_moneda) if hay_descuento else None

    grupos = {}
    for c in conceptos:
        for t in c["traslados"]:
            grupos.setdefault((t["impuesto"], t["factor"], t["tasa"]), []).append(t)
    orden_factor = {"Tasa": 0, "Cuota": 1, "Exento": 2}
    traslados = []
    for (imp, factor, tasa), filas in sorted(
        grupos.items(), key=lambda kv: (kv[0][0], orden_factor[kv[0][1]], kv[0][2] if kv[0][2] is not None else Decimal(-1))
    ):
        traslados.append({
            "base": redondear(sum(f["base"] for f in filas), decimales_moneda),
            "impuesto": imp, "factor": factor, "tasa": tasa,
            "importe": None if factor == "Exento" else redondear(sum(f["importe"] for f in filas), decimales_moneda),
        })

    grupos_ret = {}
    for c in conceptos:
        for r in c["retenciones"]:
            grupos_ret.setdefault(r["impuesto"], []).append(r)
    retenciones = [
        {"impuesto": imp, "importe": redondear(sum(f["importe"] for f in filas), decimales_moneda)}
        for imp, filas in sorted(grupos_ret.items())
    ]

    con_importe = [t for t in traslados if t["importe"] is not None]
    total_traslados = sum(t["importe"] for t in con_importe) if con_importe else None
    total_retenidos = sum(r["importe"] for r in retenciones) if retenciones else None
    total = subtotal - (descuento or Decimal(0)) + (total_traslados or Decimal(0)) - (total_retenidos or Decimal(0))
    return {"conceptos": conceptos, "subtotal": subtotal, "descuento": descuento, "traslados": traslados,
            "retenciones": retenciones, "total_traslados": total_traslados, "total_retenidos": total_retenidos,
            "total": total}


# ---------------------------------------------------------------------------------------------------------------------
# XML: el mismo formato que EscritorCfdi (XmlWriter con sangría de dos espacios)
# ---------------------------------------------------------------------------------------------------------------------
def esc(valor: str) -> str:
    return valor.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;").replace('"', "&quot;")


def atributos(pares) -> str:
    return "".join(f' {nombre}="{esc(valor)}"' for nombre, valor in pares if valor not in (None, ""))


def armar_xml(s: dict, emisor: dict, calculo: dict, decimales: int, certificado: str, no_certificado: str, sello: str | None) -> str:
    moneda = s["moneda"]
    tipo_cambio = None if moneda in ("MXN", "XXX") or s.get("tipoCambio") is None else importe(s["tipoCambio"], 0)
    lineas = ['<?xml version="1.0" encoding="utf-8"?>']

    raiz = [
        ("Version", "4.0"), ("Serie", s.get("serie")), ("Folio", s.get("folio")), ("Fecha", s["fecha"]),
        ("Sello", sello), ("FormaPago", s.get("formaPago")), ("NoCertificado", no_certificado),
        ("Certificado", certificado), ("CondicionesDePago", s.get("condicionesDePago")),
        ("SubTotal", fijo(calculo["subtotal"], decimales)),
        ("Descuento", fijo(calculo["descuento"], decimales) if calculo["descuento"] is not None else None),
        ("Moneda", moneda), ("TipoCambio", tipo_cambio), ("Total", fijo(calculo["total"], decimales)),
        ("TipoDeComprobante", s["tipoDeComprobante"]), ("Exportacion", s["exportacion"]),
        ("MetodoPago", s.get("metodoPago")), ("LugarExpedicion", s["lugarExpedicion"]),
    ]
    lineas.append(
        f'<cfdi:Comprobante xmlns:cfdi="{ESPACIO_CFDI}" xmlns:xsi="{ESPACIO_XSI}" xsi:schemaLocation="{UBICACION}"'
        + atributos(raiz) + ">"
    )

    if (g := s.get("informacionGlobal")) is not None:
        lineas.append("  <cfdi:InformacionGlobal" + atributos(
            [("Periodicidad", g["periodicidad"]), ("Meses", g["meses"]), ("Año", str(g["anio"]))]) + " />")

    for r in s.get("cfdiRelacionados", []):
        lineas.append("  <cfdi:CfdiRelacionados" + atributos([("TipoRelacion", r["tipoRelacion"])]) + ">")
        for uuid in r["uuids"]:
            lineas.append("    <cfdi:CfdiRelacionado" + atributos([("UUID", uuid)]) + " />")
        lineas.append("  </cfdi:CfdiRelacionados>")

    e = s["emisor"]
    lineas.append("  <cfdi:Emisor" + atributos(
        [("Rfc", e["rfc"]), ("Nombre", e["nombre"]), ("RegimenFiscal", e["regimenFiscal"])]) + " />")

    r = s["receptor"]
    lineas.append("  <cfdi:Receptor" + atributos([
        ("Rfc", r["rfc"]), ("Nombre", r.get("nombre")), ("DomicilioFiscalReceptor", r.get("domicilioFiscalReceptor")),
        ("ResidenciaFiscal", r.get("residenciaFiscal")), ("NumRegIdTrib", r.get("numRegIdTrib")),
        ("RegimenFiscalReceptor", r.get("regimenFiscalReceptor")), ("UsoCFDI", r.get("usoCfdi"))]) + " />")

    lineas.append("  <cfdi:Conceptos>")
    for c, calc in zip(s["conceptos"], calculo["conceptos"]):
        lleva = bool(calc["traslados"] or calc["retenciones"])
        objeto = c.get("objetoImp") or ("02" if lleva else "01")
        descuento = calc["descuento"]
        decimales_importe = max(decimales, decimales_de(descuento or Decimal(0)))
        atrs = [
            ("ClaveProdServ", c["claveProdServ"]), ("NoIdentificacion", c.get("noIdentificacion")),
            ("Cantidad", importe(c["cantidad"], 0)), ("ClaveUnidad", c["claveUnidad"]), ("Unidad", c.get("unidad")),
            ("Descripcion", c["descripcion"]), ("ValorUnitario", importe(c["valorUnitario"], decimales)),
            ("Importe", importe(calc["importe"], decimales_importe)),
            ("Descuento", importe(descuento, decimales) if descuento is not None else None), ("ObjetoImp", objeto),
        ]
        if not lleva:
            lineas.append("    <cfdi:Concepto" + atributos(atrs) + " />")
            continue
        lineas.append("    <cfdi:Concepto" + atributos(atrs) + ">")
        lineas.append("      <cfdi:Impuestos>")
        for nombre, plural, filas in (("Traslado", "Traslados", calc["traslados"]), ("Retencion", "Retenciones", calc["retenciones"])):
            if not filas:
                continue
            lineas.append(f"        <cfdi:{plural}>")
            for f in filas:
                lineas.append(f"          <cfdi:{nombre}" + atributos([
                    ("Base", importe(f["base"], decimales)), ("Impuesto", f["impuesto"]), ("TipoFactor", f["factor"]),
                    ("TasaOCuota", fijo(f["tasa"], 6) if f["tasa"] is not None else None),
                    ("Importe", importe(f["importe"], decimales) if f["importe"] is not None else None)]) + " />")
            lineas.append(f"        </cfdi:{plural}>")
        lineas.append("      </cfdi:Impuestos>")
        lineas.append("    </cfdi:Concepto>")
    lineas.append("  </cfdi:Conceptos>")

    if calculo["traslados"] or calculo["retenciones"]:
        lineas.append("  <cfdi:Impuestos" + atributos([
            ("TotalImpuestosRetenidos", fijo(calculo["total_retenidos"], decimales) if calculo["total_retenidos"] is not None else None),
            ("TotalImpuestosTrasladados", fijo(calculo["total_traslados"], decimales) if calculo["total_traslados"] is not None else None),
        ]) + ">")
        if calculo["retenciones"]:
            lineas.append("    <cfdi:Retenciones>")
            for f in calculo["retenciones"]:
                lineas.append("      <cfdi:Retencion" + atributos(
                    [("Impuesto", f["impuesto"]), ("Importe", fijo(f["importe"], decimales))]) + " />")
            lineas.append("    </cfdi:Retenciones>")
        if calculo["traslados"]:
            lineas.append("    <cfdi:Traslados>")
            for f in calculo["traslados"]:
                lineas.append("      <cfdi:Traslado" + atributos([
                    ("Base", fijo(f["base"], decimales)), ("Impuesto", f["impuesto"]), ("TipoFactor", f["factor"]),
                    ("TasaOCuota", fijo(f["tasa"], 6) if f["tasa"] is not None else None),
                    ("Importe", fijo(f["importe"], decimales) if f["importe"] is not None else None)]) + " />")
            lineas.append("    </cfdi:Traslados>")
        lineas.append("  </cfdi:Impuestos>")

    lineas.append("</cfdi:Comprobante>")
    return "\n".join(lineas)


# ---------------------------------------------------------------------------------------------------------------------
# Cadena original (XSLT del SAT con libxslt), sello (RSA SHA-256) y XSD (libxml2)
# ---------------------------------------------------------------------------------------------------------------------
class ResolverLocal(etree.Resolver):
    """Los XSLT del SAT se incluyen unos a otros por su dirección en www.sat.gob.mx: se sirven de sat/xslt."""

    def resolve(self, url, id, context):
        if url.startswith("http://www.sat.gob.mx/"):
            return self.resolve_filename(str(SAT / "xslt" / url.rsplit("/", 1)[1]), context)
        return None


def cargar_xslt():
    parser = etree.XMLParser()
    parser.resolvers.add(ResolverLocal())
    return etree.XSLT(etree.parse(str(SAT / "xslt" / "cadenaoriginal_4_0.xslt"), parser))


def cadena_original(xslt, xml: str) -> str:
    return str(xslt(etree.fromstring(xml.encode("utf-8"))))


def cargar_esquema():
    carpeta = Path(tempfile.mkdtemp(prefix="cfdi-xsd-"))
    for archivo in (SAT / "esquemas").glob("*.xsd"):
        texto = archivo.read_text(encoding="utf-8")
        for destino in ("catCFDI.xsd", "tdCFDI.xsd", "TimbreFiscalDigitalv11.xsd"):
            texto = re.sub(r'schemaLocation="[^"]*/' + re.escape(destino) + '"', f'schemaLocation="{destino}"', texto)
        (carpeta / archivo.name).write_text(texto, encoding="utf-8")
    esquema = etree.XMLSchema(etree.parse(str(carpeta / "cfdv40.xsd")))
    shutil.rmtree(carpeta, ignore_errors=True)
    return esquema


def main() -> int:
    with open(SAT / "csd-prueba" / "emisores.json", encoding="utf-8") as f:
        registro = json.load(f)
    contrasena = registro["contrasena"].encode()
    emisores = {e["rfc"]: e for e in registro["emisores"]}

    xslt = cargar_xslt()
    esquema = cargar_esquema()
    decimales_moneda = {"MXN": 2, "USD": 2, "XXX": 0}

    escenarios = sorted(AQUI.glob("*.solicitud.json"))
    if not escenarios:
        print("No hay archivos *.solicitud.json en", AQUI)
        return 1

    for ruta in escenarios:
        nombre = ruta.name.removesuffix(".solicitud.json")
        s = json.loads(ruta.read_text(encoding="utf-8"), parse_float=Decimal, parse_int=Decimal)

        # Lo que Comprobante completa si se omite: moneda, tipo, exportación, datos del emisor de prueba.
        s.setdefault("moneda", "MXN")
        s.setdefault("tipoDeComprobante", "I")
        s.setdefault("exportacion", "01")
        emisor = emisores[s["emisor"]["rfc"]]
        s["emisor"].setdefault("nombre", emisor["nombre"])
        s["emisor"].setdefault("regimenFiscal", emisor["regimenFiscal"])
        s.setdefault("lugarExpedicion", emisor["codigoPostal"])

        cer = (SAT / "csd-prueba" / f"{emisor['archivo']}.cer").read_bytes()
        llave = serialization.load_der_private_key((SAT / "csd-prueba" / f"{emisor['archivo']}.key").read_bytes(), contrasena)
        certificado = x509.load_der_x509_certificate(cer)
        no_certificado = certificado.serial_number.to_bytes((certificado.serial_number.bit_length() + 7) // 8, "big").decode("ascii")
        cer_base64 = base64.b64encode(cer).decode("ascii")

        decimales = decimales_moneda[s["moneda"]]
        calculo = calcular(s, decimales)

        sin_sello = armar_xml(s, emisor, calculo, decimales, cer_base64, no_certificado, None)
        cadena = cadena_original(xslt, sin_sello)
        sello = base64.b64encode(llave.sign(cadena.encode("utf-8"), padding.PKCS1v15(), hashes.SHA256())).decode("ascii")
        xml = armar_xml(s, emisor, calculo, decimales, cer_base64, no_certificado, sello)

        documento = etree.fromstring(xml.encode("utf-8"))
        if not esquema.validate(documento):
            print(f"{nombre}: el XML no cumple el XSD:")
            for error in esquema.error_log:
                print("  ", error.message)
            return 1

        (AQUI / f"{nombre}.xml").write_bytes(xml.encode("utf-8"))
        (AQUI / f"{nombre}.cadena.txt").write_bytes(cadena.encode("utf-8"))
        print(f"{nombre}: total {fijo(calculo['total'], decimales)}, NoCertificado {no_certificado}, cadena de {len(cadena)} caracteres")
    return 0


if __name__ == "__main__":
    sys.exit(main())
