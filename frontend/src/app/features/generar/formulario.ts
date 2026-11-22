import { applyEach, hidden, maxLength, min, pattern, required, schema, validate } from '@angular/forms/signals';

import { ConceptoSolicitud, ImpuestoSolicitud, SolicitudCfdi } from '../../core/modelos';

/**
 * El formulario de Generar: el mismo comprobante que acepta la API, con los números como <c>number | null</c> (un campo
 * vacío es null, no cero) y los textos como cadenas vacías. <c>aSolicitud</c> lo convierte a lo que se manda.
 */

export interface ImpuestoFormulario {
  /** Clave de c_Impuesto: 001 ISR, 002 IVA, 003 IEPS. */
  impuesto: string;
  /** Tasa, Cuota o Exento. */
  tipoFactor: string;
  tasaOCuota: number | null;
}

export interface ConceptoFormulario {
  claveProdServ: string;
  noIdentificacion: string;
  claveUnidad: string;
  unidad: string;
  descripcion: string;
  cantidad: number | null;
  valorUnitario: number | null;
  descuento: number | null;
  traslados: ImpuestoFormulario[];
  retenciones: ImpuestoFormulario[];
}

export interface ReceptorFormulario {
  rfc: string;
  nombre: string;
  domicilioFiscalReceptor: string;
  regimenFiscalReceptor: string;
  usoCfdi: string;
  /** Casilla "receptor extranjero": habilita la residencia fiscal y el número de registro tributario. */
  extranjero: boolean;
  residenciaFiscal: string;
  numRegIdTrib: string;
}

export interface FormularioCfdi {
  tipoDeComprobante: 'I' | 'E';
  serie: string;
  folio: string;
  /** Valor de un <c>datetime-local</c> (<c>2026-11-21T09:30</c>) o vacío para usar la hora actual del lugar de expedición. */
  fecha: string;
  formaPago: string;
  metodoPago: string;
  condicionesDePago: string;
  moneda: string;
  tipoCambio: number | null;
  lugarExpedicion: string;
  emisor: { rfc: string };
  receptor: ReceptorFormulario;
  informacionGlobal: { periodicidad: string; meses: string; anio: number | null };
  cfdiRelacionados: {
    activos: boolean;
    tipoRelacion: string;
    /** Un folio fiscal (UUID) por renglón; también se aceptan separados por comas o espacios. */
    uuids: string;
  };
  conceptos: ConceptoFormulario[];
}

export const RFC_PUBLICO_EN_GENERAL = 'XAXX010101000';
export const NOMBRE_PUBLICO_EN_GENERAL = 'PUBLICO EN GENERAL';

/** Con el RFC y el nombre genéricos nacionales el comprobante lleva el nodo de información global. */
export function esPublicoEnGeneral(receptor: Pick<ReceptorFormulario, 'rfc' | 'nombre'>): boolean {
  return (
    receptor.rfc.trim().toUpperCase() === RFC_PUBLICO_EN_GENERAL &&
    receptor.nombre.trim().toUpperCase() === NOMBRE_PUBLICO_EN_GENERAL
  );
}

/** Con moneda MXN o XXX el tipo de cambio no se captura. */
export function usaTipoDeCambio(moneda: string): boolean {
  const clave = moneda.trim().toUpperCase();
  return clave !== '' && clave !== 'MXN' && clave !== 'XXX';
}

// ---------------------------------------------------------------------------------------------------------------------
// Valores iniciales
// ---------------------------------------------------------------------------------------------------------------------

export function impuestoNuevo(tipo: 'traslado' | 'retencion'): ImpuestoFormulario {
  return tipo === 'traslado'
    ? { impuesto: '002', tipoFactor: 'Tasa', tasaOCuota: 0.16 }
    : { impuesto: '001', tipoFactor: 'Tasa', tasaOCuota: 0.1 };
}

export function conceptoNuevo(datos: Partial<ConceptoFormulario> = {}): ConceptoFormulario {
  return {
    claveProdServ: '',
    noIdentificacion: '',
    claveUnidad: '',
    unidad: '',
    descripcion: '',
    cantidad: 1,
    valorUnitario: null,
    descuento: null,
    traslados: [impuestoNuevo('traslado')],
    retenciones: [],
    ...datos,
  };
}

export function formularioVacio(): FormularioCfdi {
  return {
    tipoDeComprobante: 'I',
    serie: '',
    folio: '',
    fecha: '',
    formaPago: '01',
    metodoPago: 'PUE',
    condicionesDePago: '',
    moneda: 'MXN',
    tipoCambio: null,
    lugarExpedicion: '',
    emisor: { rfc: '' },
    receptor: {
      rfc: '',
      nombre: '',
      domicilioFiscalReceptor: '',
      regimenFiscalReceptor: '',
      usoCfdi: '',
      extranjero: false,
      residenciaFiscal: '',
      numRegIdTrib: '',
    },
    informacionGlobal: { periodicidad: '', meses: '', anio: null },
    cfdiRelacionados: { activos: false, tipoRelacion: '', uuids: '' },
    conceptos: [conceptoNuevo()],
  };
}

// ---------------------------------------------------------------------------------------------------------------------
// Validación en el formulario
// ---------------------------------------------------------------------------------------------------------------------

const RFC = /^[A-ZÑ&]{3,4}\d{6}[A-Z\d]{3}$/i;
const CODIGO_POSTAL = /^\d{5}$/;
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Hasta seis decimales, como pide el Anexo 20 para cantidades, valores unitarios, descuentos y tasas. */
function tieneSeisDecimalesOMenos(valor: number): boolean {
  return /^-?\d+(\.\d{1,6})?$/.test(String(valor));
}

/** Separa los folios fiscales escritos uno por renglón, o con comas, puntos y coma o espacios. */
export function separarUuids(texto: string): string[] {
  return texto
    .split(/[\s,;]+/)
    .map((uuid) => uuid.trim())
    .filter((uuid) => uuid.length > 0);
}

const esquemaImpuesto = schema<ImpuestoFormulario>((impuesto) => {
  required(impuesto.impuesto, { message: 'Elige el impuesto.' });
  required(impuesto.tipoFactor, { message: 'Elige el tipo de factor.' });
  hidden(impuesto.tasaOCuota, { when: ({ valueOf }) => valueOf(impuesto.tipoFactor) === 'Exento' });
  required(impuesto.tasaOCuota, { message: 'Escribe la tasa o la cuota.' });
  min(impuesto.tasaOCuota, 0, { message: 'La tasa o la cuota no puede ser negativa.' });
  validate(impuesto.tasaOCuota, ({ value }) => {
    const tasa = value();
    return tasa !== null && !tieneSeisDecimalesOMenos(tasa)
      ? { kind: 'decimales', message: 'La tasa o la cuota admite hasta 6 decimales.' }
      : undefined;
  });
});

const esquemaConcepto = schema<ConceptoFormulario>((concepto) => {
  required(concepto.claveProdServ, { message: 'Escribe la clave de producto o servicio.' });
  pattern(concepto.claveProdServ, /^\d{8}$/, { message: 'La clave de producto o servicio tiene 8 dígitos.' });
  required(concepto.claveUnidad, { message: 'Escribe la clave de unidad.' });
  required(concepto.descripcion, { message: 'Describe el concepto.' });
  maxLength(concepto.descripcion, 1000, { message: 'La descripción admite hasta 1,000 caracteres.' });

  required(concepto.cantidad, { message: 'Escribe la cantidad.' });
  required(concepto.valorUnitario, { message: 'Escribe el valor unitario.' });
  for (const [campo, nombre] of [
    [concepto.cantidad, 'La cantidad'],
    [concepto.valorUnitario, 'El valor unitario'],
  ] as const) {
    min(campo, 0.000001, { message: `${nombre} debe ser mayor que cero.` });
    validate(campo, ({ value }) => {
      const numero = value();
      return numero !== null && !tieneSeisDecimalesOMenos(numero)
        ? { kind: 'decimales', message: `${nombre} admite hasta 6 decimales.` }
        : undefined;
    });
  }

  min(concepto.descuento, 0, { message: 'El descuento no puede ser negativo.' });
  validate(concepto.descuento, ({ value }) => {
    const descuento = value();
    return descuento !== null && !tieneSeisDecimalesOMenos(descuento)
      ? { kind: 'decimales', message: 'El descuento admite hasta 6 decimales.' }
      : undefined;
  });

  applyEach(concepto.traslados, esquemaImpuesto);
  applyEach(concepto.retenciones, esquemaImpuesto);
});

/** Las reglas del formulario: lo que falta o está mal escrito. Que las claves existan en los catálogos lo dice el validador. */
export const esquemaFormulario = schema<FormularioCfdi>((ruta) => {
  maxLength(ruta.serie, 25, { message: 'La serie admite hasta 25 caracteres.' });
  maxLength(ruta.folio, 40, { message: 'El folio admite hasta 40 caracteres.' });
  required(ruta.formaPago, { message: 'Elige la forma de pago.' });
  required(ruta.metodoPago, { message: 'Elige el método de pago.' });
  required(ruta.moneda, { message: 'Elige la moneda.' });
  pattern(ruta.moneda, /^[A-Za-z]{3}$/, { message: 'La moneda es una clave de tres letras: MXN, USD...' });
  pattern(ruta.lugarExpedicion, CODIGO_POSTAL, { message: 'El código postal tiene 5 dígitos.' });
  maxLength(ruta.lugarExpedicion, 5, { message: 'El código postal tiene 5 dígitos.' });

  hidden(ruta.tipoCambio, { when: ({ valueOf }) => !usaTipoDeCambio(valueOf(ruta.moneda)) });
  required(ruta.tipoCambio, { message: 'Con esta moneda el tipo de cambio es obligatorio.' });
  min(ruta.tipoCambio, 0.000001, { message: 'El tipo de cambio debe ser mayor que cero.' });
  validate(ruta.tipoCambio, ({ value }) => {
    const tipo = value();
    return tipo !== null && !tieneSeisDecimalesOMenos(tipo)
      ? { kind: 'decimales', message: 'El tipo de cambio admite hasta 6 decimales.' }
      : undefined;
  });

  required(ruta.emisor.rfc, { message: 'Elige el emisor de prueba con el que se sella.' });

  required(ruta.receptor.rfc, { message: 'Escribe el RFC del receptor.' });
  pattern(ruta.receptor.rfc, RFC, { message: 'El RFC debe tener 12 o 13 caracteres, como EKU9003173C9.' });
  required(ruta.receptor.nombre, { message: 'Escribe el nombre del receptor, tal como está en su constancia.' });
  maxLength(ruta.receptor.nombre, 254, { message: 'El nombre admite hasta 254 caracteres.' });
  required(ruta.receptor.domicilioFiscalReceptor, { message: 'Escribe el código postal del domicilio fiscal del receptor.' });
  pattern(ruta.receptor.domicilioFiscalReceptor, CODIGO_POSTAL, { message: 'El código postal tiene 5 dígitos.' });
  maxLength(ruta.receptor.domicilioFiscalReceptor, 5, { message: 'El código postal tiene 5 dígitos.' });
  required(ruta.receptor.regimenFiscalReceptor, { message: 'Escribe el régimen fiscal del receptor.' });
  required(ruta.receptor.usoCfdi, { message: 'Escribe el uso del CFDI.' });
  hidden(ruta.receptor.residenciaFiscal, { when: ({ valueOf }) => !valueOf(ruta.receptor.extranjero) });
  hidden(ruta.receptor.numRegIdTrib, { when: ({ valueOf }) => !valueOf(ruta.receptor.extranjero) });
  required(ruta.receptor.residenciaFiscal, { message: 'Escribe el país de residencia fiscal.' });

  hidden(ruta.informacionGlobal, { when: ({ valueOf }) => !esPublicoEnGeneral(valueOf(ruta.receptor)) });
  required(ruta.informacionGlobal.periodicidad, { message: 'Elige la periodicidad.' });
  required(ruta.informacionGlobal.meses, { message: 'Elige el mes o el bimestre.' });
  required(ruta.informacionGlobal.anio, { message: 'Escribe el año.' });
  min(ruta.informacionGlobal.anio, 2022, { message: 'El año no puede ser anterior a 2022.' });

  hidden(ruta.cfdiRelacionados.tipoRelacion, { when: ({ valueOf }) => !valueOf(ruta.cfdiRelacionados.activos) });
  hidden(ruta.cfdiRelacionados.uuids, { when: ({ valueOf }) => !valueOf(ruta.cfdiRelacionados.activos) });
  required(ruta.cfdiRelacionados.tipoRelacion, { message: 'Elige el tipo de relación.' });
  required(ruta.cfdiRelacionados.uuids, { message: 'Escribe al menos un folio fiscal (UUID).' });
  validate(ruta.cfdiRelacionados.uuids, ({ value }) => {
    const malos = separarUuids(value()).filter((uuid) => !UUID.test(uuid));
    return malos.length > 0
      ? { kind: 'uuid', message: `No es un folio fiscal válido: ${malos[0]}. Tiene el formato 5FB2822E-396D-4725-8521-CDC4BDD20CCF.` }
      : undefined;
  });

  validate(ruta.conceptos, ({ value }) =>
    value().length === 0 ? { kind: 'sinConceptos', message: 'Agrega al menos un concepto.' } : undefined,
  );
  applyEach(ruta.conceptos, esquemaConcepto);
});

// ---------------------------------------------------------------------------------------------------------------------
// De formulario a solicitud
// ---------------------------------------------------------------------------------------------------------------------

function texto(valor: string): string | undefined {
  const limpio = valor.trim();
  return limpio === '' ? undefined : limpio;
}

/** El <c>datetime-local</c> trae minutos (<c>2026-11-21T09:30</c>); la API quiere los segundos (<c>…:00</c>). */
function fechaConSegundos(valor: string): string | undefined {
  const limpio = valor.trim();
  if (limpio === '') {
    return undefined;
  }

  return /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(limpio) ? `${limpio}:00` : limpio;
}

function aImpuesto(impuesto: ImpuestoFormulario): ImpuestoSolicitud {
  return {
    impuesto: impuesto.impuesto,
    tipoFactor: impuesto.tipoFactor,
    tasaOCuota: impuesto.tipoFactor === 'Exento' ? undefined : impuesto.tasaOCuota,
  };
}

function aConcepto(concepto: ConceptoFormulario): ConceptoSolicitud {
  return {
    claveProdServ: concepto.claveProdServ.trim(),
    noIdentificacion: texto(concepto.noIdentificacion),
    cantidad: concepto.cantidad ?? 0,
    claveUnidad: concepto.claveUnidad.trim(),
    unidad: texto(concepto.unidad),
    descripcion: concepto.descripcion.trim(),
    valorUnitario: concepto.valorUnitario ?? 0,
    descuento: concepto.descuento ?? undefined,
    traslados: concepto.traslados.map(aImpuesto),
    retenciones: concepto.retenciones.map(aImpuesto),
  };
}

/** Lo que se manda a la API: sin los campos vacíos, con la fecha completa y solo lo que aplica a la moneda y al receptor. */
export function aSolicitud(formulario: FormularioCfdi): SolicitudCfdi {
  const { receptor, informacionGlobal, cfdiRelacionados } = formulario;
  const uuids = separarUuids(cfdiRelacionados.uuids);

  return {
    serie: texto(formulario.serie),
    folio: texto(formulario.folio),
    fecha: fechaConSegundos(formulario.fecha),
    formaPago: texto(formulario.formaPago),
    metodoPago: texto(formulario.metodoPago),
    condicionesDePago: texto(formulario.condicionesDePago),
    moneda: texto(formulario.moneda),
    tipoCambio: usaTipoDeCambio(formulario.moneda) ? formulario.tipoCambio : undefined,
    tipoDeComprobante: formulario.tipoDeComprobante,
    lugarExpedicion: texto(formulario.lugarExpedicion),
    emisor: { rfc: formulario.emisor.rfc.trim() },
    receptor: {
      rfc: receptor.rfc.trim(),
      nombre: receptor.nombre.trim(),
      domicilioFiscalReceptor: receptor.domicilioFiscalReceptor.trim(),
      regimenFiscalReceptor: receptor.regimenFiscalReceptor.trim(),
      usoCfdi: receptor.usoCfdi.trim(),
      residenciaFiscal: receptor.extranjero ? texto(receptor.residenciaFiscal) : undefined,
      numRegIdTrib: receptor.extranjero ? texto(receptor.numRegIdTrib) : undefined,
    },
    informacionGlobal: esPublicoEnGeneral(receptor)
      ? {
          periodicidad: informacionGlobal.periodicidad.trim(),
          meses: informacionGlobal.meses.trim(),
          anio: informacionGlobal.anio ?? 0,
        }
      : undefined,
    cfdiRelacionados:
      cfdiRelacionados.activos && uuids.length > 0
        ? [{ tipoRelacion: cfdiRelacionados.tipoRelacion.trim(), uuids }]
        : undefined,
    conceptos: formulario.conceptos.map(aConcepto),
  };
}

/** ¿Hay con qué calcular totales? Al menos un concepto con cantidad y valor unitario mayores que cero. */
export function esCalculable(formulario: FormularioCfdi): boolean {
  return formulario.conceptos.some((c) => (c.cantidad ?? 0) > 0 && (c.valorUnitario ?? 0) > 0);
}
