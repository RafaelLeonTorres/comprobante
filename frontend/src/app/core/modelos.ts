/**
 * Los tipos que viajan por la API. Siguen los nombres de los registros de C# con la primera letra en minúscula (el JSON de
 * la API); los enumerados van como texto.
 */

// ---------------------------------------------------------------------------------------------------------------------
// Solicitud: lo que se manda para calcular y generar un comprobante
// ---------------------------------------------------------------------------------------------------------------------

export interface ImpuestoSolicitud {
  /** Clave de c_Impuesto: 001 ISR, 002 IVA, 003 IEPS. */
  impuesto: string;
  /** Tasa, Cuota o Exento. */
  tipoFactor: string;
  /** La tasa (0.16 es el 16 %) o la cuota; no se manda con Exento. */
  tasaOCuota?: number | null;
  base?: number | null;
}

export interface ConceptoSolicitud {
  claveProdServ: string;
  noIdentificacion?: string;
  cantidad: number;
  claveUnidad: string;
  unidad?: string;
  descripcion: string;
  valorUnitario: number;
  descuento?: number | null;
  /** Si se omite, la API pone 02 cuando el concepto lleva impuestos y 01 cuando no. */
  objetoImp?: string;
  traslados?: ImpuestoSolicitud[];
  retenciones?: ImpuestoSolicitud[];
}

export interface EmisorSolicitud {
  rfc: string;
  nombre?: string;
  regimenFiscal?: string;
}

export interface ReceptorSolicitud {
  rfc: string;
  nombre: string;
  domicilioFiscalReceptor: string;
  residenciaFiscal?: string;
  numRegIdTrib?: string;
  regimenFiscalReceptor: string;
  usoCfdi: string;
}

export interface InformacionGlobalSolicitud {
  periodicidad: string;
  meses: string;
  anio: number;
}

export interface CfdiRelacionadosSolicitud {
  tipoRelacion: string;
  uuids: string[];
}

export interface SolicitudCfdi {
  serie?: string;
  folio?: string;
  /** Hora local del lugar de expedición, sin zona: <c>2026-11-21T09:30:00</c>. */
  fecha?: string;
  formaPago?: string;
  metodoPago?: string;
  condicionesDePago?: string;
  moneda?: string;
  tipoCambio?: number | null;
  tipoDeComprobante?: 'I' | 'E';
  exportacion?: string;
  lugarExpedicion?: string;
  emisor?: EmisorSolicitud;
  receptor?: ReceptorSolicitud;
  informacionGlobal?: InformacionGlobalSolicitud;
  cfdiRelacionados?: CfdiRelacionadosSolicitud[];
  conceptos?: ConceptoSolicitud[];
}

// ---------------------------------------------------------------------------------------------------------------------
// Cálculo y generación
// ---------------------------------------------------------------------------------------------------------------------

// La API no escribe lo que es nulo (DefaultIgnoreCondition = WhenWritingNull): en las respuestas esos campos pueden faltar.

export interface ImpuestoCalculado {
  impuesto: string;
  tipoFactor: string;
  tasaOCuota?: number | null;
  base: number;
  importe?: number | null;
}

export interface ConceptoCalculado {
  importe: number;
  descuento?: number | null;
  traslados: ImpuestoCalculado[];
  retenciones: ImpuestoCalculado[];
}

export interface TrasladoResumen {
  base: number;
  impuesto: string;
  tipoFactor: string;
  tasaOCuota?: number | null;
  importe?: number | null;
}

export interface RetencionResumen {
  impuesto: string;
  importe: number;
}

export interface ResultadoCalculo {
  moneda: string;
  decimalesMoneda: number;
  conceptos: ConceptoCalculado[];
  subTotal: number;
  descuento?: number | null;
  traslados: TrasladoResumen[];
  retenciones: RetencionResumen[];
  totalImpuestosTrasladados?: number | null;
  totalImpuestosRetenidos?: number | null;
  total: number;
}

export interface ResultadoGeneracion {
  xml: string;
  nombreArchivo: string;
  cadenaOriginal: string;
  sello: string;
  noCertificado: string;
  calculo: ResultadoCalculo;
  validacion: ReporteValidacion;
}

// ---------------------------------------------------------------------------------------------------------------------
// Validación
// ---------------------------------------------------------------------------------------------------------------------

export type Severidad = 'error' | 'advertencia';

export interface Hallazgo {
  /** El código de la matriz de errores del SAT (CFDI40161) o uno propio con prefijo CMP. */
  codigo: string;
  severidad: Severidad;
  /** Ruta del nodo: cfdi:Comprobante/cfdi:Conceptos/cfdi:Concepto[2]. */
  nodo: string;
  atributo?: string | null;
  mensaje: string;
  encontrado?: string | null;
  esperado?: string | null;
  linea?: number | null;
}

export interface DatosCertificado {
  noCertificado: string;
  rfc?: string | null;
  nombre?: string | null;
  validoDesde: string;
  validoHasta: string;
}

export interface ReporteValidacion {
  esValido: boolean;
  hallazgos: Hallazgo[];
  cadenaOriginal?: string | null;
  /** Nulo (o ausente) cuando no se pudo comprobar: falta el sello, el certificado o la cadena. */
  selloValido?: boolean | null;
  certificado?: DatosCertificado | null;
}

export type EstadoDeRegla = 'revisada' | 'parcial' | 'noRevisada';

export interface Regla {
  codigo: string;
  elemento: string;
  atributo?: string | null;
  descripcion: string;
  estado: EstadoDeRegla;
  nota?: string | null;
  cubiertaPor?: string | null;
}

export interface CoberturaDeReglas {
  resumen: { total: number; revisadas: number; parciales: number; noRevisadas: number };
  reglas: Regla[];
}

// ---------------------------------------------------------------------------------------------------------------------
// Emisores de prueba y catálogos
// ---------------------------------------------------------------------------------------------------------------------

export interface EmisorDePrueba {
  rfc: string;
  nombre: string;
  persona: 'moral' | 'fisica';
  codigoPostal: string;
  regimenFiscal: string;
  noCertificado: string;
  validoDesde: string;
  validoHasta: string;
}

export interface ColumnaCatalogo {
  clave: string;
  titulo: string;
}

/** Una fila de un catálogo: las columnas dependen del catálogo (clave, descripcion, nombre...). */
export type FilaCatalogo = Readonly<Record<string, string | number | boolean | null>>;

export interface PaginaCatalogo {
  total: number;
  pagina: number;
  tamano: number;
  columnas: ColumnaCatalogo[];
  filas: FilaCatalogo[];
}
