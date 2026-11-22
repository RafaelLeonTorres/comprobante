import { ConceptoFormulario, FormularioCfdi, conceptoNuevo, formularioVacio } from './formulario';

export interface EjemploDeComprobante {
  id: string;
  titulo: string;
  ayuda: string;
  /** Un formulario nuevo cada vez: así editar uno no cambia el ejemplo. */
  crear: () => FormularioCfdi;
}

const iva = (tasa: number) => ({ impuesto: '002', tipoFactor: 'Tasa', tasaOCuota: tasa });
const ivaExento = { impuesto: '002', tipoFactor: 'Exento', tasaOCuota: null };

function concepto(datos: Partial<ConceptoFormulario>): ConceptoFormulario {
  return conceptoNuevo({ traslados: [iva(0.16)], ...datos });
}

/** Los comprobantes de ejemplo no llevan fecha: la API pone la hora actual del lugar de expedición. */
function comprobante(datos: Partial<FormularioCfdi>): FormularioCfdi {
  return { ...formularioVacio(), ...datos };
}

const receptorBase = { extranjero: false, residenciaFiscal: '', numRegIdTrib: '' };

/** Cinco comprobantes que cubren lo que arma la app. Los emisores son de los certificados de prueba del SAT. */
export const EJEMPLOS: readonly EjemploDeComprobante[] = [
  {
    id: 'servicio',
    titulo: 'Un servicio con IVA',
    ayuda: 'Ingreso con un concepto y el IVA al 16 %. El más sencillo.',
    crear: () =>
      comprobante({
        serie: 'A',
        folio: '123',
        formaPago: '01',
        emisor: { rfc: 'EKU9003173C9' },
        receptor: {
          ...receptorBase,
          rfc: 'XIA190128J61',
          nombre: 'XENON INDUSTRIAL ARTICLES',
          domicilioFiscalReceptor: '76343',
          regimenFiscalReceptor: '622',
          usoCfdi: 'G03',
        },
        conceptos: [
          concepto({
            claveProdServ: '84111506',
            claveUnidad: 'E48',
            unidad: 'Unidad de servicio',
            descripcion: 'Servicio de facturación electrónica',
            cantidad: 1,
            valorUnitario: 1000,
          }),
        ],
      }),
  },
  {
    id: 'retenciones',
    titulo: 'Con retenciones y descuento',
    ayuda: 'Honorarios con descuento, IVA e ISR retenidos, un concepto con IVA al 0 % y otro exento.',
    crear: () =>
      comprobante({
        serie: 'H',
        folio: '2026-0042',
        formaPago: '03',
        condicionesDePago: 'Contado',
        emisor: { rfc: 'WATM640917J45' },
        receptor: {
          ...receptorBase,
          rfc: 'EKU9003173C9',
          nombre: 'ESCUELA KEMPER URGATE',
          domicilioFiscalReceptor: '42501',
          regimenFiscalReceptor: '601',
          usoCfdi: 'G03',
        },
        conceptos: [
          concepto({
            claveProdServ: '80101500',
            claveUnidad: 'HUR',
            unidad: 'Hora',
            descripcion: 'Asesoría en administración corporativa',
            cantidad: 10,
            valorUnitario: 850,
            descuento: 100,
            retenciones: [
              { impuesto: '001', tipoFactor: 'Tasa', tasaOCuota: 0.1 },
              { impuesto: '002', tipoFactor: 'Tasa', tasaOCuota: 0.106667 },
            ],
          }),
          concepto({
            claveProdServ: '81111500',
            noIdentificacion: 'SW-01',
            claveUnidad: 'E48',
            unidad: 'Unidad de servicio',
            descripcion: 'Configuración de software',
            cantidad: 1,
            valorUnitario: 2500.5,
            traslados: [iva(0)],
          }),
          concepto({
            claveProdServ: '43231500',
            claveUnidad: 'H87',
            unidad: 'Pieza',
            descripcion: 'Manual impreso',
            cantidad: 2,
            valorUnitario: 99.99,
            traslados: [ivaExento],
          }),
        ],
      }),
  },
  {
    id: 'dolares',
    titulo: 'En dólares',
    ayuda: 'Moneda USD con tipo de cambio y cantidades con decimales: se ve el redondeo del Anexo 20.',
    crear: () =>
      comprobante({
        serie: 'USD',
        folio: '7',
        formaPago: '03',
        moneda: 'USD',
        tipoCambio: 17.4521,
        emisor: { rfc: 'H&E951128469' },
        receptor: {
          ...receptorBase,
          rfc: 'L&O950913MSA',
          nombre: 'LUCES & OBRAS',
          domicilioFiscalReceptor: '60922',
          regimenFiscalReceptor: '626',
          usoCfdi: 'G03',
        },
        conceptos: [
          concepto({
            claveProdServ: '84111506',
            claveUnidad: 'E48',
            unidad: 'Unidad de servicio',
            descripcion: 'Servicio por hora & soporte',
            cantidad: 3.141649,
            valorUnitario: 1230,
            descuento: 864.1,
          }),
          concepto({
            claveProdServ: '43231500',
            noIdentificacion: 'SKU-9981',
            claveUnidad: 'H87',
            unidad: 'Pieza',
            descripcion: 'Licencia de uso',
            cantidad: 2,
            valorUnitario: 33.333333,
          }),
        ],
      }),
  },
  {
    id: 'publico-en-general',
    titulo: 'Público en general',
    ayuda: 'La factura global del mes: RFC genérico y el nodo de información global.',
    crear: () => {
      const hoy = new Date();
      return comprobante({
        formaPago: '01',
        emisor: { rfc: 'MISC491214B86' },
        receptor: {
          ...receptorBase,
          rfc: 'XAXX010101000',
          nombre: 'PUBLICO EN GENERAL',
          domicilioFiscalReceptor: '01010',
          regimenFiscalReceptor: '616',
          usoCfdi: 'S01',
        },
        informacionGlobal: {
          periodicidad: '01',
          meses: String(hoy.getMonth() + 1).padStart(2, '0'),
          anio: hoy.getFullYear(),
        },
        conceptos: [
          concepto({
            claveProdServ: '01010101',
            claveUnidad: 'ACT',
            descripcion: 'Venta',
            cantidad: 1,
            valorUnitario: 4580,
          }),
        ],
      });
    },
  },
  {
    id: 'egreso',
    titulo: 'Egreso con CFDI relacionado',
    ayuda: 'Una nota de crédito (tipo E) relacionada con la factura que corrige.',
    crear: () =>
      comprobante({
        tipoDeComprobante: 'E',
        serie: 'E',
        folio: '45',
        formaPago: '03',
        emisor: { rfc: 'IAÑL750210963' },
        receptor: {
          ...receptorBase,
          rfc: 'FUNK671228PH6',
          nombre: 'KARLA FUENTE NOLASCO',
          domicilioFiscalReceptor: '01160',
          regimenFiscalReceptor: '621',
          usoCfdi: 'G02',
        },
        cfdiRelacionados: { activos: true, tipoRelacion: '01', uuids: '5FB2822E-396D-4725-8521-CDC4BDD20CCF' },
        conceptos: [
          concepto({
            claveProdServ: '84111506',
            claveUnidad: 'E48',
            unidad: 'Unidad de servicio',
            descripcion: 'Devolución parcial del servicio',
            cantidad: 1,
            valorUnitario: 350.75,
          }),
        ],
      }),
  },
];
