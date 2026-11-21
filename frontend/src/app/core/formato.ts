/** El formato de dígitos de <c>number</c> y <c>currency</c> de Angular para una cantidad fija de decimales: <c>1.2-2</c>. */
export function digitos(decimales: number): string {
  return `1.${decimales}-${decimales}`;
}

/** Un importe con los decimales de su moneda y separador de miles, en español de México: <c>1,160.00</c>. */
export function importe(valor: number | null | undefined, decimales = 2): string {
  if (valor === null || valor === undefined) {
    return '';
  }

  return new Intl.NumberFormat('es-MX', { minimumFractionDigits: decimales, maximumFractionDigits: decimales }).format(valor);
}

/** Una tasa como porcentaje: <c>0.16</c> es <c>16 %</c>; <c>0.106667</c> es <c>10.6667 %</c> (sin ceros de más). */
export function porcentaje(tasa: number): string {
  const texto = new Intl.NumberFormat('es-MX', { maximumFractionDigits: 4 }).format(tasa * 100);
  return `${texto} %`;
}

/** Nombre de un impuesto por su clave de c_Impuesto. */
export function nombreDeImpuesto(clave: string): string {
  switch (clave) {
    case '001':
      return 'ISR';
    case '002':
      return 'IVA';
    case '003':
      return 'IEPS';
    default:
      return clave;
  }
}
