import { TestBed } from '@angular/core/testing';

import { ErrorApi } from '../../core/errores-api';
import { ResultadoCalculo } from '../../core/modelos';
import { EstadoDeTotales, TotalesCalculo } from './totales-calculo';

const CALCULO: ResultadoCalculo = {
  moneda: 'MXN',
  decimalesMoneda: 2,
  conceptos: [],
  subTotal: 3000,
  descuento: 100,
  traslados: [
    { base: 2900, impuesto: '002', tipoFactor: 'Tasa', tasaOCuota: 0.16, importe: 464 },
    { base: 1000, impuesto: '002', tipoFactor: 'Exento' }, // la API no manda tasa ni importe de lo exento
    { base: 500, impuesto: '003', tipoFactor: 'Cuota', tasaOCuota: 1.5, importe: 750 },
  ],
  retenciones: [
    { impuesto: '001', importe: 290 },
    { impuesto: '002', importe: 309.33 },
  ],
  totalImpuestosTrasladados: 1214,
  totalImpuestosRetenidos: 599.33,
  total: 3514.67,
};

describe('TotalesCalculo', () => {
  function pintar(estado: EstadoDeTotales, calculo?: ResultadoCalculo, error: ErrorApi | null = null) {
    const fixture = TestBed.createComponent(TotalesCalculo);
    fixture.componentRef.setInput('estado', estado);
    fixture.componentRef.setInput('calculo', calculo);
    fixture.componentRef.setInput('error', error);
    fixture.detectChanges();
    const elemento = fixture.nativeElement as HTMLElement;
    const limpio = (nodo: Element | null) => nodo!.textContent!.replace(/\s+/g, ' ').trim();
    const filas = () =>
      Object.fromEntries([...elemento.querySelectorAll('dl > div')].map((fila) => [limpio(fila.querySelector('dt')), limpio(fila.querySelector('dd'))]));
    return { fixture, elemento, filas, limpio };
  }

  it('sin conceptos calculables explica qué falta y no muestra cifras', () => {
    const { elemento } = pintar('vacio');

    expect(elemento.textContent).toContain('Agrega un concepto con cantidad y valor unitario para ver los totales.');
    expect(elemento.querySelector('dl')).toBeNull();
  });

  it('desglosa subtotal, descuento, traslados, retenciones y total con los decimales de la moneda', () => {
    const { filas } = pintar('listo', CALCULO);

    expect(filas()).toEqual({
      Subtotal: '3,000.00',
      Descuento: '−100.00',
      'IVA 16 % trasladado base 2,900.00': '464.00',
      'IVA exento base 1,000.00': '—',
      'IEPS cuota 1.5 trasladado base 500.00': '750.00',
      'ISR retenido': '−290.00',
      'IVA retenido': '−309.33',
      Total: '3,514.67 MXN',
    });
  });

  it('sin descuento ni impuestos solo muestra subtotal y total, venga el dato ausente (como lo manda la API) o nulo', () => {
    const { descuento: _sinDescuento, ...ausente } = CALCULO;
    expect(pintar('listo', { ...ausente, traslados: [], retenciones: [], total: 3000 }).filas()).toEqual({ Subtotal: '3,000.00', Total: '3,000.00 MXN' });

    expect(pintar('listo', { ...CALCULO, descuento: null, traslados: [], retenciones: [], total: 3000 }).filas()).toEqual({
      Subtotal: '3,000.00',
      Total: '3,000.00 MXN',
    });
  });

  it('usa los decimales de la moneda del cálculo', () => {
    const { filas } = pintar('listo', { ...CALCULO, moneda: 'JPY', decimalesMoneda: 0, descuento: null, traslados: [], retenciones: [], subTotal: 3000, total: 3000 });

    expect(filas()).toEqual({ Subtotal: '3,000', Total: '3,000 JPY' });
  });

  it('mientras recalcula conserva las cifras anteriores atenuadas y sin cifras dice que calcula', () => {
    const recalculando = pintar('calculando', CALCULO);
    expect(recalculando.elemento.querySelector('dl')!.classList).toContain('actualizando');
    expect(recalculando.filas()['Total']).toBe('3,514.67 MXN');

    const primera = pintar('calculando');
    expect(primera.elemento.textContent).toContain('Calculando…');
    expect(primera.elemento.querySelector('dl')).toBeNull();
  });

  it('con un error muestra lo primero que hay que corregir', () => {
    const conCampos = pintar(
      'error',
      undefined,
      new ErrorApi(400, 'Hay datos que corregir', 'Revisa los campos.', { 'conceptos[0].descuento': ['El descuento no puede ser mayor que el importe.'] }),
    );
    expect(conCampos.limpio(conCampos.elemento.querySelector('.aviso'))).toBe(
      'Todavía no se pueden calcular los totales. El descuento no puede ser mayor que el importe.',
    );

    const sinCampos = pintar('error', undefined, new ErrorApi(500, 'La API falló', 'Intenta de nuevo.'));
    expect(sinCampos.limpio(sinCampos.elemento.querySelector('.aviso'))).toBe('Todavía no se pueden calcular los totales. Intenta de nuevo.');
  });

  it('con un 429 dice cuánto esperar', () => {
    const detalle = 'Hiciste demasiadas solicitudes en poco tiempo. Espera 12 segundos e inténtalo de nuevo.';
    const { elemento, limpio } = pintar('error', CALCULO, new ErrorApi(429, 'Demasiadas solicitudes', detalle));

    expect(limpio(elemento.querySelector('.aviso'))).toBe(`Todavía no se pueden calcular los totales. ${detalle}`);
  });

  it('anuncia los cambios a quien usa lector de pantalla sin interrumpirlo', () => {
    const { elemento } = pintar('listo', CALCULO);

    expect(elemento.querySelector('[aria-live="polite"]')).not.toBeNull();
    expect(elemento.querySelector('section')!.getAttribute('aria-labelledby')).toBe('totales-titulo');
  });
});
