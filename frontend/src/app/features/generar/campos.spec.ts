import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { form } from '@angular/forms/signals';

import { buscarCampo, etiquetaDeRuta, primerMensaje, rutaDeCampo } from './campos';
import { EJEMPLOS } from './ejemplos';
import { FormularioCfdi, esquemaFormulario } from './formulario';

function crearFormulario(modelo: FormularioCfdi) {
  const datos = signal(modelo);
  return { datos, formulario: TestBed.runInInjectionContext(() => form(datos, esquemaFormulario)) };
}

const retenciones = () => EJEMPLOS.find((e) => e.id === 'retenciones')!.crear();

describe('buscarCampo', () => {
  it('encuentra campos por la ruta que usa la API para sus errores', () => {
    const { formulario } = crearFormulario(retenciones());

    expect(buscarCampo(formulario, 'emisor.rfc')).toBe(formulario.emisor.rfc);
    expect(buscarCampo(formulario, 'receptor.usoCfdi')).toBe(formulario.receptor.usoCfdi);
    expect(buscarCampo(formulario, 'conceptos')).toBe(formulario.conceptos);
    expect(buscarCampo(formulario, 'conceptos[1].cantidad')).toBe(formulario.conceptos[1]!.cantidad);
    expect(buscarCampo(formulario, 'conceptos[0].retenciones[1].tasaOCuota')).toBe(
      formulario.conceptos[0]!.retenciones[1]!.tasaOCuota,
    );
    expect(buscarCampo(formulario, 'moneda')).toBe(formulario.moneda);
  });

  it('devuelve undefined cuando la ruta no es de ningún campo', () => {
    const { formulario } = crearFormulario(retenciones());

    for (const ruta of ['', 'nada', 'conceptos[9].cantidad', 'conceptos[0].nada', 'emisor.rfc.otra', 'conceptos[x]', 'a..b']) {
      expect(buscarCampo(formulario, ruta), ruta).toBeUndefined();
    }
  });
});

describe('rutaDeCampo', () => {
  it('es la inversa de buscarCampo', () => {
    const f = retenciones();
    const { formulario } = crearFormulario(f);

    for (const ruta of [
      'moneda',
      'emisor.rfc',
      'receptor.usoCfdi',
      'conceptos',
      'conceptos[2].descripcion',
      'conceptos[0].retenciones[1].tasaOCuota',
      'cfdiRelacionados.uuids',
    ]) {
      expect(rutaDeCampo(formulario, f, buscarCampo(formulario, ruta)), ruta).toBe(ruta);
    }
  });

  it('devuelve undefined para algo que no es un campo del formulario', () => {
    const f = retenciones();
    const { formulario } = crearFormulario(f);
    const { formulario: otro } = crearFormulario(f);

    expect(rutaDeCampo(formulario, f, otro.moneda)).toBeUndefined();
    expect(rutaDeCampo(formulario, f, 'moneda')).toBeUndefined();
  });

  it('el campo de un error del resumen es el que da la ruta', () => {
    const f = retenciones();
    f.conceptos[1]!.descripcion = '';
    const { formulario } = crearFormulario(f);

    const error = formulario().errorSummary()[0]!;

    expect(error.message).toBe('Describe el concepto.');
    expect(error.fieldTree).toBe(buscarCampo(formulario, 'conceptos[1].descripcion'));
    expect(rutaDeCampo(formulario, f, error.fieldTree)).toBe('conceptos[1].descripcion');
  });
});

describe('etiquetaDeRuta', () => {
  it.each([
    ['moneda', 'Moneda'],
    ['emisor.rfc', 'Emisor'],
    ['receptor.rfc', 'Receptor › RFC'],
    ['receptor.usoCfdi', 'Receptor › Uso del CFDI'],
    ['conceptos', 'Conceptos'],
    ['conceptos[1].cantidad', 'Concepto 2 › Cantidad'],
    ['conceptos[0].traslados[2].tasaOCuota', 'Concepto 1 › Traslado 3 › Tasa o cuota'],
    ['conceptos[0].retenciones[0].impuesto', 'Concepto 1 › Retención 1 › Impuesto'],
    ['informacionGlobal.anio', 'Información global › Año'],
    ['algoRaro.otro', 'algoRaro › otro'],
  ])('%s se llama %s', (ruta, etiqueta) => {
    expect(etiquetaDeRuta(ruta)).toBe(etiqueta);
  });
});

describe('primerMensaje', () => {
  it('da el primer error del campo o nada', () => {
    const f = retenciones();
    f.receptor.rfc = '';
    const { formulario } = crearFormulario(f);

    expect(primerMensaje(formulario.receptor.rfc)).toBe('Escribe el RFC del receptor.');
    expect(primerMensaje(formulario.receptor.nombre)).toBe('');
  });
});
