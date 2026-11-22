import { FieldTree, isFieldTree } from '@angular/forms/signals';

/** El primer mensaje de error de un campo, o cadena vacía si no tiene. */
export function primerMensaje<T>(campo: FieldTree<T>): string {
  return campo().errors()[0]?.message ?? '';
}

/**
 * El campo del formulario al que se refiere la clave de un error de la API: <c>emisor.rfc</c>, <c>conceptos[1].cantidad</c>,
 * <c>conceptos</c>. Devuelve undefined si la clave no corresponde a ningún campo (un error general, o un campo que el
 * formulario no tiene).
 */
export function buscarCampo(raiz: unknown, ruta: string): FieldTree<unknown> | undefined {
  let actual: unknown = raiz;
  for (const parte of ruta.split('.')) {
    const coincidencia = /^(\w+)(?:\[(\d+)\])?$/.exec(parte);
    if (!coincidencia || !isFieldTree(actual)) {
      return undefined;
    }

    actual = (actual as unknown as Record<string, unknown>)[coincidencia[1]!];
    if (coincidencia[2] !== undefined && isFieldTree(actual)) {
      actual = (actual as unknown as Record<number, unknown>)[Number(coincidencia[2])];
    }

    if (!isFieldTree(actual)) {
      return undefined;
    }
  }

  return actual as FieldTree<unknown>;
}

/**
 * La ruta de un campo (<c>conceptos[1].cantidad</c>), buscándolo entre los hijos de <c>raiz</c> según la forma de <c>valor</c>,
 * el valor del modelo en ese nivel. Es la operación inversa de <c>buscarCampo</c>. Devuelve undefined si no está.
 */
export function rutaDeCampo(raiz: unknown, valor: unknown, objetivo: unknown, prefijo = ''): string | undefined {
  if (raiz === objetivo) {
    return prefijo;
  }

  if (!isFieldTree(raiz) || valor === null || typeof valor !== 'object') {
    return undefined;
  }

  const hijos = raiz as unknown as Record<string | number, unknown>;
  const claves: (string | number)[] = Array.isArray(valor) ? valor.map((_, indice) => indice) : Object.keys(valor);
  for (const clave of claves) {
    const subruta = typeof clave === 'number' ? `${prefijo}[${clave}]` : prefijo ? `${prefijo}.${clave}` : clave;
    const encontrada = rutaDeCampo(hijos[clave], (valor as Record<string | number, unknown>)[clave], objetivo, subruta);
    if (encontrada !== undefined) {
      return encontrada;
    }
  }

  return undefined;
}

const NOMBRES: Readonly<Record<string, string>> = {
  tipoDeComprobante: 'Tipo de comprobante',
  serie: 'Serie',
  folio: 'Folio',
  fecha: 'Fecha',
  formaPago: 'Forma de pago',
  metodoPago: 'Método de pago',
  condicionesDePago: 'Condiciones de pago',
  moneda: 'Moneda',
  tipoCambio: 'Tipo de cambio',
  lugarExpedicion: 'Lugar de expedición',
  emisor: 'Emisor',
  receptor: 'Receptor',
  rfc: 'RFC',
  nombre: 'Nombre',
  domicilioFiscalReceptor: 'Domicilio fiscal',
  regimenFiscalReceptor: 'Régimen fiscal',
  usoCfdi: 'Uso del CFDI',
  residenciaFiscal: 'Residencia fiscal',
  numRegIdTrib: 'Registro tributario',
  informacionGlobal: 'Información global',
  periodicidad: 'Periodicidad',
  meses: 'Mes',
  anio: 'Año',
  cfdiRelacionados: 'CFDI relacionados',
  tipoRelacion: 'Tipo de relación',
  uuids: 'Folios fiscales',
  conceptos: 'Conceptos',
  claveProdServ: 'Producto o servicio',
  noIdentificacion: 'Identificación',
  claveUnidad: 'Clave de unidad',
  unidad: 'Unidad',
  descripcion: 'Descripción',
  cantidad: 'Cantidad',
  valorUnitario: 'Valor unitario',
  descuento: 'Descuento',
  traslados: 'Traslados',
  retenciones: 'Retenciones',
  impuesto: 'Impuesto',
  tipoFactor: 'Tipo de factor',
  tasaOCuota: 'Tasa o cuota',
};

const SINGULARES: Readonly<Record<string, string>> = {
  conceptos: 'Concepto',
  traslados: 'Traslado',
  retenciones: 'Retención',
};

/** El nombre que ve el usuario de un campo: <c>conceptos[1].cantidad</c> es "Concepto 2 › Cantidad". */
export function etiquetaDeRuta(ruta: string): string {
  if (ruta === 'emisor.rfc') {
    return 'Emisor';
  }

  return ruta
    .split('.')
    .map((parte) => {
      const coincidencia = /^(\w+)(?:\[(\d+)\])?$/.exec(parte);
      if (!coincidencia) {
        return parte;
      }

      const [, nombre = parte, indice] = coincidencia;
      return indice === undefined
        ? (NOMBRES[nombre] ?? nombre)
        : `${SINGULARES[nombre] ?? NOMBRES[nombre] ?? nombre} ${Number(indice) + 1}`;
    })
    .join(' › ');
}
