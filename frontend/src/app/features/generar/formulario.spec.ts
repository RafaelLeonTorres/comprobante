import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { form } from '@angular/forms/signals';

import { EJEMPLOS } from './ejemplos';
import {
  FormularioCfdi,
  aSolicitud,
  conceptoNuevo,
  esCalculable,
  esPublicoEnGeneral,
  esquemaFormulario,
  formularioVacio,
  separarUuids,
  usaTipoDeCambio,
} from './formulario';

function crearFormulario(modelo: FormularioCfdi) {
  const datos = signal(modelo);
  const formulario = TestBed.runInInjectionContext(() => form(datos, esquemaFormulario));
  return { datos, formulario };
}

function ejemplo(id: string): FormularioCfdi {
  return EJEMPLOS.find((e) => e.id === id)!.crear();
}

describe('aSolicitud', () => {
  it('no manda los campos vacíos y recorta los espacios', () => {
    const f = ejemplo('servicio');
    f.serie = '  A ';
    f.folio = '';
    f.condicionesDePago = '   ';

    const solicitud = aSolicitud(f);

    expect(solicitud.serie).toBe('A');
    expect(solicitud.folio).toBeUndefined();
    expect(solicitud.condicionesDePago).toBeUndefined();
    expect(solicitud.fecha).toBeUndefined();
    expect(solicitud.lugarExpedicion).toBeUndefined();
    expect(JSON.parse(JSON.stringify(solicitud))).not.toHaveProperty('folio');
  });

  it('completa los segundos de una fecha de datetime-local', () => {
    const f = ejemplo('servicio');

    f.fecha = '2026-11-21T09:30';
    expect(aSolicitud(f).fecha).toBe('2026-11-21T09:30:00');

    f.fecha = '2026-11-21T09:30:15';
    expect(aSolicitud(f).fecha).toBe('2026-11-21T09:30:15');
  });

  it('el tipo de cambio solo viaja con una moneda que lo usa', () => {
    const dolares = ejemplo('dolares');
    expect(aSolicitud(dolares).tipoCambio).toBe(17.4521);

    dolares.moneda = 'MXN';
    expect(aSolicitud(dolares).tipoCambio).toBeUndefined();

    dolares.moneda = 'XXX';
    expect(aSolicitud(dolares).tipoCambio).toBeUndefined();

    expect([usaTipoDeCambio('USD'), usaTipoDeCambio(' mxn '), usaTipoDeCambio('')]).toEqual([true, false, false]);
  });

  it('los impuestos exentos no llevan tasa', () => {
    const f = ejemplo('retenciones');

    const exento = aSolicitud(f).conceptos![2]!.traslados![0]!;

    expect(exento).toEqual({ impuesto: '002', tipoFactor: 'Exento', tasaOCuota: undefined });
  });

  it('un campo numérico vacío viaja como cero para que la API diga qué falta', () => {
    const f = ejemplo('servicio');
    f.conceptos[0]!.cantidad = null;
    f.conceptos[0]!.valorUnitario = null;

    const concepto = aSolicitud(f).conceptos![0]!;

    expect([concepto.cantidad, concepto.valorUnitario]).toEqual([0, 0]);
  });

  it('la información global solo viaja con el público en general', () => {
    const global = ejemplo('publico-en-general');
    expect(aSolicitud(global).informacionGlobal).toEqual({
      periodicidad: '01',
      meses: String(new Date().getMonth() + 1).padStart(2, '0'),
      anio: new Date().getFullYear(),
    });

    global.receptor.rfc = 'XIA190128J61';
    expect(aSolicitud(global).informacionGlobal).toBeUndefined();
  });

  it('el público en general se reconoce por el RFC y el nombre, sin importar mayúsculas ni espacios', () => {
    expect(esPublicoEnGeneral({ rfc: ' xaxx010101000 ', nombre: 'publico en general' })).toBe(true);
    expect(esPublicoEnGeneral({ rfc: 'XAXX010101000', nombre: 'Otro' })).toBe(false);
    expect(esPublicoEnGeneral({ rfc: 'XEXX010101000', nombre: 'PUBLICO EN GENERAL' })).toBe(false);
  });

  it('los CFDI relacionados salen con sus UUID separados, y solo si la casilla está activa', () => {
    const f = ejemplo('egreso');
    f.cfdiRelacionados.uuids = '5FB2822E-396D-4725-8521-CDC4BDD20CCF,\n  7a1c2d3e-0000-4000-8000-0123456789ab ';

    expect(aSolicitud(f).cfdiRelacionados).toEqual([
      { tipoRelacion: '01', uuids: ['5FB2822E-396D-4725-8521-CDC4BDD20CCF', '7a1c2d3e-0000-4000-8000-0123456789ab'] },
    ]);

    f.cfdiRelacionados.activos = false;
    expect(aSolicitud(f).cfdiRelacionados).toBeUndefined();
  });

  it('los datos de un receptor extranjero solo viajan con la casilla activa', () => {
    const f = ejemplo('servicio');
    f.receptor.residenciaFiscal = 'USA';
    f.receptor.numRegIdTrib = '123456789';

    expect(aSolicitud(f).receptor).toMatchObject({ residenciaFiscal: undefined, numRegIdTrib: undefined });

    f.receptor.extranjero = true;
    expect(aSolicitud(f).receptor).toMatchObject({ residenciaFiscal: 'USA', numRegIdTrib: '123456789' });
  });

  it('separa folios por renglones, comas, puntos y coma o espacios', () => {
    expect(separarUuids('a\nb, c;d  e\r\nf')).toEqual(['a', 'b', 'c', 'd', 'e', 'f']);
    expect(separarUuids('  ')).toEqual([]);
  });
});

describe('esCalculable', () => {
  it('pide un concepto con cantidad y valor unitario mayores que cero', () => {
    const f = formularioVacio();
    expect(esCalculable(f)).toBe(false);

    f.conceptos[0]!.valorUnitario = 100;
    expect(esCalculable(f)).toBe(true);

    f.conceptos[0]!.cantidad = 0;
    expect(esCalculable(f)).toBe(false);

    f.conceptos.push(conceptoNuevo({ cantidad: 2, valorUnitario: 5 }));
    expect(esCalculable(f)).toBe(true);
  });
});

describe('ejemplos', () => {
  it.each(EJEMPLOS.map((e) => [e.id]))('%s pasa las reglas del formulario', (id) => {
    const { formulario } = crearFormulario(ejemplo(id!));

    expect(formulario().errorSummary().map((e) => e.message)).toEqual([]);
    expect(formulario().valid()).toBe(true);
  });

  it('cada llamada devuelve un formulario nuevo', () => {
    const uno = EJEMPLOS[0]!.crear();
    uno.conceptos[0]!.descripcion = 'cambiada';

    expect(EJEMPLOS[0]!.crear().conceptos[0]!.descripcion).toBe('Servicio de facturación electrónica');
  });
});

describe('reglas del formulario', () => {
  it('el formulario vacío pide lo que falta, con su explicación', () => {
    const { formulario } = crearFormulario(formularioVacio());

    const mensajes = formulario().errorSummary().map((e) => e.message);

    expect(mensajes).toEqual(
      expect.arrayContaining([
        'Elige el emisor de prueba con el que se sella.',
        'Escribe el RFC del receptor.',
        'Escribe el nombre del receptor, tal como está en su constancia.',
        'Escribe el código postal del domicilio fiscal del receptor.',
        'Escribe el régimen fiscal del receptor.',
        'Escribe el uso del CFDI.',
        'Escribe la clave de producto o servicio.',
        'Escribe la clave de unidad.',
        'Describe el concepto.',
        'Escribe el valor unitario.',
      ]),
    );
    expect(formulario().valid()).toBe(false);
  });

  it('el RFC, el código postal y la clave de producto deben tener su formato', () => {
    const f = ejemplo('servicio');
    f.receptor.rfc = 'ABC';
    f.receptor.domicilioFiscalReceptor = '123';
    f.lugarExpedicion = '4250';
    f.conceptos[0]!.claveProdServ = '8411';
    const { formulario } = crearFormulario(f);

    const mensajes = formulario().errorSummary().map((e) => e.message);

    expect(mensajes).toEqual(
      expect.arrayContaining([
        'El RFC debe tener 12 o 13 caracteres, como EKU9003173C9.',
        'El código postal tiene 5 dígitos.',
        'La clave de producto o servicio tiene 8 dígitos.',
      ]),
    );
    expect(mensajes.filter((m) => m === 'El código postal tiene 5 dígitos.')).toHaveLength(2);
  });

  it('acepta los RFC con Ñ y & y los genéricos', () => {
    for (const rfc of ['IAÑL750210963', 'H&E951128469', 'XAXX010101000', 'XEXX010101000', 'xia190128j61']) {
      const f = ejemplo('servicio');
      f.receptor.rfc = rfc;
      const { formulario } = crearFormulario(f);
      expect(formulario.receptor.rfc().errors(), rfc).toEqual([]);
    }
  });

  it('cantidad y valor unitario deben ser mayores que cero y de hasta seis decimales', () => {
    const f = ejemplo('servicio');
    f.conceptos[0]!.cantidad = 0;
    f.conceptos[0]!.valorUnitario = 1.1234567;
    f.conceptos[0]!.descuento = -1;
    const { formulario } = crearFormulario(f);

    const mensajes = formulario().errorSummary().map((e) => e.message);

    expect(mensajes).toEqual(
      expect.arrayContaining([
        'La cantidad debe ser mayor que cero.',
        'El valor unitario admite hasta 6 decimales.',
        'El descuento no puede ser negativo.',
      ]),
    );
  });

  it('el tipo de cambio solo se exige con una moneda que lo usa', () => {
    const f = ejemplo('dolares');
    f.tipoCambio = null;
    const { datos, formulario } = crearFormulario(f);

    expect(formulario.tipoCambio().hidden()).toBe(false);
    expect(formulario.tipoCambio().errors().map((e) => e.message)).toEqual(['Con esta moneda el tipo de cambio es obligatorio.']);

    datos.update((actual) => ({ ...actual, moneda: 'MXN' }));

    expect(formulario.tipoCambio().hidden()).toBe(true);
    expect(formulario().valid()).toBe(true);
  });

  it('un impuesto exento no pide tasa, uno con tasa sí', () => {
    const f = ejemplo('retenciones');
    const { datos, formulario } = crearFormulario(f);
    expect(formulario().valid()).toBe(true);

    datos.update((actual) => {
      const conceptos = structuredClone(actual.conceptos);
      conceptos[0]!.traslados[0]!.tasaOCuota = null;
      return { ...actual, conceptos };
    });

    expect(formulario.conceptos[0]!.traslados[0]!.tasaOCuota().errors().map((e) => e.message)).toEqual(['Escribe la tasa o la cuota.']);
    expect(formulario.conceptos[2]!.traslados[0]!.tasaOCuota().hidden()).toBe(true);
  });

  it('sin conceptos pide al menos uno', () => {
    const f = ejemplo('servicio');
    f.conceptos = [];
    const { formulario } = crearFormulario(f);

    expect(formulario.conceptos().errors().map((e) => e.message)).toEqual(['Agrega al menos un concepto.']);
  });

  it('la información global solo se exige con el público en general', () => {
    const f = ejemplo('publico-en-general');
    f.informacionGlobal = { periodicidad: '', meses: '', anio: null };
    const { datos, formulario } = crearFormulario(f);

    expect(formulario.informacionGlobal().hidden()).toBe(false);
    expect(formulario().errorSummary().map((e) => e.message)).toEqual(
      expect.arrayContaining(['Elige la periodicidad.', 'Elige el mes o el bimestre.', 'Escribe el año.']),
    );

    datos.update((actual) => ({ ...actual, receptor: { ...actual.receptor, rfc: 'XIA190128J61', nombre: 'XENON' } }));

    expect(formulario.informacionGlobal().hidden()).toBe(true);
    expect(formulario().valid()).toBe(true);
  });

  it('los CFDI relacionados piden tipo y folios con formato de UUID solo si están activos', () => {
    const f = ejemplo('egreso');
    f.cfdiRelacionados = { activos: true, tipoRelacion: '', uuids: 'no-es-un-uuid' };
    const { datos, formulario } = crearFormulario(f);

    expect(formulario().errorSummary().map((e) => e.message)).toEqual(
      expect.arrayContaining(['Elige el tipo de relación.', expect.stringContaining('No es un folio fiscal válido: no-es-un-uuid.')]),
    );

    datos.update((actual) => ({ ...actual, cfdiRelacionados: { ...actual.cfdiRelacionados, activos: false } }));
    expect(formulario().valid()).toBe(true);
  });

  it('un receptor extranjero pide el país de residencia fiscal', () => {
    const f = ejemplo('servicio');
    f.receptor.extranjero = true;
    const { datos, formulario } = crearFormulario(f);

    expect(formulario.receptor.residenciaFiscal().errors().map((e) => e.message)).toEqual(['Escribe el país de residencia fiscal.']);

    datos.update((actual) => ({ ...actual, receptor: { ...actual.receptor, extranjero: false } }));
    expect(formulario().valid()).toBe(true);
  });
});
