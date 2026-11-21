using System.Text;
using System.Xml;
using Comprobante.Core.Documento;

namespace Comprobante.Core.Xml;

/// <summary>
/// Escribe un <see cref="Cfdi"/> como XML del CFDI 4.0: prefijo <c>cfdi</c>, el espacio de nombres del Anexo 20, la ubicación
/// oficial del esquema, los nodos en el orden del esquema y solo los atributos que tienen valor.
/// </summary>
public static class EscritorCfdi
{
    public const string EspacioCfdi = "http://www.sat.gob.mx/cfd/4";
    public const string EspacioXsi = "http://www.w3.org/2001/XMLSchema-instance";
    public const string UbicacionEsquema = "http://www.sat.gob.mx/cfd/4 http://www.sat.gob.mx/sitio_internet/cfd/4/cfdv40.xsd";

    /// <summary>El XML en UTF-8, sin marca de orden de bytes y con sangría de dos espacios.</summary>
    public static string Escribir(Cfdi cfdi)
    {
        using var memoria = new MemoryStream();
        var configuracion = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace,
        };
        using (var xml = XmlWriter.Create(memoria, configuracion))
        {
            Comprobante(xml, cfdi);
        }

        return Encoding.UTF8.GetString(memoria.ToArray());
    }

    private static void Comprobante(XmlWriter xml, Cfdi c)
    {
        xml.WriteStartDocument();
        xml.WriteStartElement("cfdi", "Comprobante", EspacioCfdi);

        // Los espacios de nombres se declaran a mano para que salgan en el orden de los ejemplos del SAT (cfdi, xsi): si no, el
        // escritor pondría primero el que se escribe como atributo.
        xml.WriteAttributeString("xmlns", "cfdi", null, EspacioCfdi);
        xml.WriteAttributeString("xmlns", "xsi", null, EspacioXsi);
        xml.WriteAttributeString("xsi", "schemaLocation", EspacioXsi, UbicacionEsquema);
        Atributo(xml, "Version", c.Version);
        Atributo(xml, "Serie", c.Serie);
        Atributo(xml, "Folio", c.Folio);
        Atributo(xml, "Fecha", c.Fecha);
        Atributo(xml, "Sello", c.Sello);
        Atributo(xml, "FormaPago", c.FormaPago);
        Atributo(xml, "NoCertificado", c.NoCertificado);
        Atributo(xml, "Certificado", c.Certificado);
        Atributo(xml, "CondicionesDePago", c.CondicionesDePago);
        Atributo(xml, "SubTotal", c.SubTotal);
        Atributo(xml, "Descuento", c.Descuento);
        Atributo(xml, "Moneda", c.Moneda);
        Atributo(xml, "TipoCambio", c.TipoCambio);
        Atributo(xml, "Total", c.Total);
        Atributo(xml, "TipoDeComprobante", c.TipoDeComprobante);
        Atributo(xml, "Exportacion", c.Exportacion);
        Atributo(xml, "MetodoPago", c.MetodoPago);
        Atributo(xml, "LugarExpedicion", c.LugarExpedicion);
        Atributo(xml, "Confirmacion", c.Confirmacion);

        if (c.InformacionGlobal is { } global)
        {
            xml.WriteStartElement("cfdi", "InformacionGlobal", EspacioCfdi);
            Atributo(xml, "Periodicidad", global.Periodicidad);
            Atributo(xml, "Meses", global.Meses);
            Atributo(xml, "Año", global.Anio);
            xml.WriteEndElement();
        }

        foreach (var relacionados in c.CfdiRelacionados)
        {
            xml.WriteStartElement("cfdi", "CfdiRelacionados", EspacioCfdi);
            Atributo(xml, "TipoRelacion", relacionados.TipoRelacion);
            foreach (var uuid in relacionados.Uuids)
            {
                xml.WriteStartElement("cfdi", "CfdiRelacionado", EspacioCfdi);
                Atributo(xml, "UUID", uuid);
                xml.WriteEndElement();
            }

            xml.WriteEndElement();
        }

        if (c.Emisor is { } emisor)
        {
            xml.WriteStartElement("cfdi", "Emisor", EspacioCfdi);
            Atributo(xml, "Rfc", emisor.Rfc);
            Atributo(xml, "Nombre", emisor.Nombre);
            Atributo(xml, "RegimenFiscal", emisor.RegimenFiscal);
            Atributo(xml, "FacAtrAdquirente", emisor.FacAtrAdquirente);
            xml.WriteEndElement();
        }

        if (c.Receptor is { } receptor)
        {
            xml.WriteStartElement("cfdi", "Receptor", EspacioCfdi);
            Atributo(xml, "Rfc", receptor.Rfc);
            Atributo(xml, "Nombre", receptor.Nombre);
            Atributo(xml, "DomicilioFiscalReceptor", receptor.DomicilioFiscalReceptor);
            Atributo(xml, "ResidenciaFiscal", receptor.ResidenciaFiscal);
            Atributo(xml, "NumRegIdTrib", receptor.NumRegIdTrib);
            Atributo(xml, "RegimenFiscalReceptor", receptor.RegimenFiscalReceptor);
            Atributo(xml, "UsoCFDI", receptor.UsoCfdi);
            xml.WriteEndElement();
        }

        xml.WriteStartElement("cfdi", "Conceptos", EspacioCfdi);
        foreach (var concepto in c.Conceptos)
        {
            Concepto(xml, concepto);
        }

        xml.WriteEndElement();

        if (c.Impuestos is { } impuestos)
        {
            ResumenDeImpuestos(xml, impuestos);
        }

        xml.WriteEndElement();
        xml.WriteEndDocument();
    }

    private static void Concepto(XmlWriter xml, CfdiConcepto c)
    {
        xml.WriteStartElement("cfdi", "Concepto", EspacioCfdi);
        Atributo(xml, "ClaveProdServ", c.ClaveProdServ);
        Atributo(xml, "NoIdentificacion", c.NoIdentificacion);
        Atributo(xml, "Cantidad", c.Cantidad);
        Atributo(xml, "ClaveUnidad", c.ClaveUnidad);
        Atributo(xml, "Unidad", c.Unidad);
        Atributo(xml, "Descripcion", c.Descripcion);
        Atributo(xml, "ValorUnitario", c.ValorUnitario);
        Atributo(xml, "Importe", c.Importe);
        Atributo(xml, "Descuento", c.Descuento);
        Atributo(xml, "ObjetoImp", c.ObjetoImp);

        if (c.Traslados.Count + c.Retenciones.Count > 0)
        {
            xml.WriteStartElement("cfdi", "Impuestos", EspacioCfdi);
            if (c.Traslados.Count > 0)
            {
                xml.WriteStartElement("cfdi", "Traslados", EspacioCfdi);
                foreach (var traslado in c.Traslados)
                {
                    ImpuestoDelConcepto(xml, "Traslado", traslado);
                }

                xml.WriteEndElement();
            }

            if (c.Retenciones.Count > 0)
            {
                xml.WriteStartElement("cfdi", "Retenciones", EspacioCfdi);
                foreach (var retencion in c.Retenciones)
                {
                    ImpuestoDelConcepto(xml, "Retencion", retencion);
                }

                xml.WriteEndElement();
            }

            xml.WriteEndElement();
        }

        xml.WriteEndElement();
    }

    private static void ImpuestoDelConcepto(XmlWriter xml, string nodo, CfdiImpuesto i)
    {
        xml.WriteStartElement("cfdi", nodo, EspacioCfdi);
        Atributo(xml, "Base", i.Base);
        Atributo(xml, "Impuesto", i.Impuesto);
        Atributo(xml, "TipoFactor", i.TipoFactor);
        Atributo(xml, "TasaOCuota", i.TasaOCuota);
        Atributo(xml, "Importe", i.Importe);
        xml.WriteEndElement();
    }

    private static void ResumenDeImpuestos(XmlWriter xml, CfdiImpuestos i)
    {
        xml.WriteStartElement("cfdi", "Impuestos", EspacioCfdi);
        Atributo(xml, "TotalImpuestosRetenidos", i.TotalImpuestosRetenidos);
        Atributo(xml, "TotalImpuestosTrasladados", i.TotalImpuestosTrasladados);

        // En el resumen, el esquema pide primero las retenciones y después los traslados.
        if (i.Retenciones.Count > 0)
        {
            xml.WriteStartElement("cfdi", "Retenciones", EspacioCfdi);
            foreach (var retencion in i.Retenciones)
            {
                ImpuestoDelConcepto(xml, "Retencion", retencion);
            }

            xml.WriteEndElement();
        }

        if (i.Traslados.Count > 0)
        {
            xml.WriteStartElement("cfdi", "Traslados", EspacioCfdi);
            foreach (var traslado in i.Traslados)
            {
                ImpuestoDelConcepto(xml, "Traslado", traslado);
            }

            xml.WriteEndElement();
        }

        xml.WriteEndElement();
    }

    private static void Atributo(XmlWriter xml, string nombre, string? valor)
    {
        if (!string.IsNullOrEmpty(valor))
        {
            xml.WriteAttributeString(nombre, valor);
        }
    }
}
