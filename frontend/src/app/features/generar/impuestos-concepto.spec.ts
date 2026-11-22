import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { form } from '@angular/forms/signals';
import { MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';

import { registrarIconos } from '../../core/iconos';
import { provideIdioma } from '../../core/idioma';
import { ImpuestoCalculado } from '../../core/modelos';
import { esquemaFormulario, formularioVacio } from './formulario';
import { ImpuestosConcepto } from './impuestos-concepto';

const IVA_160: ImpuestoCalculado = { impuesto: '002', tipoFactor: 'Tasa', tasaOCuota: 0.16, base: 1000, importe: 160 };

describe('ImpuestosConcepto', () => {
  function pintar(tipo: 'traslado' | 'retencion', calculados: ImpuestoCalculado[] = [], decimales = 2) {
    TestBed.resetTestingModule(); // varias pinturas en la misma prueba: cada una con su propio módulo
    TestBed.configureTestingModule({ providers: [provideIdioma()] });
    registrarIconos(TestBed.inject(MatIconRegistry), TestBed.inject(DomSanitizer));
    const modelo = signal(formularioVacio());
    const formulario = TestBed.runInInjectionContext(() => form(modelo, esquemaFormulario));
    const concepto = formulario.conceptos[0]!;
    const fixture = TestBed.createComponent(ImpuestosConcepto);
    fixture.componentRef.setInput('impuestos', tipo === 'traslado' ? concepto.traslados : concepto.retenciones);
    fixture.componentRef.setInput('tipo', tipo);
    fixture.componentRef.setInput('calculados', calculados);
    fixture.componentRef.setInput('decimales', decimales);
    fixture.detectChanges();
    const elemento = fixture.nativeElement as HTMLElement;
    const refrescar = async () => {
      await new Promise((resolve) => setTimeout(resolve));
      TestBed.tick();
      fixture.detectChanges();
    };
    const filas = () => [...elemento.querySelectorAll('.fila')];
    const boton = (texto: string) =>
      [...elemento.querySelectorAll<HTMLButtonElement>('button')].find((b) => (b.textContent!.trim() + (b.getAttribute('aria-label') ?? '')).includes(texto))!;
    return { fixture, modelo, concepto, elemento, refrescar, filas, boton };
  }

  it('los traslados llegan con el IVA al 16 % de un concepto nuevo y su importe calculado', async () => {
    const { elemento, filas, refrescar } = pintar('traslado', [IVA_160]);
    await refrescar();

    expect(elemento.querySelector('h4')!.textContent).toBe('Impuestos trasladados');
    expect(filas()).toHaveLength(1);
    expect(filas()[0]!.getAttribute('aria-label')).toBe('Traslado 1');
    expect(filas()[0]!.querySelector('mat-select')!.textContent).toContain('002 · IVA');
    expect((filas()[0]!.querySelector('input') as HTMLInputElement).value).toBe('0.16');
    expect(filas()[0]!.querySelector('[matTextSuffix]')!.textContent).toBe('16 %');
    expect(filas()[0]!.querySelector('.importe')!.textContent).toBe('160.00');
  });

  it('el importe usa los decimales de la moneda y queda vacío mientras no hay cálculo', async () => {
    const sinCalculo = pintar('traslado');
    await sinCalculo.refrescar();
    expect(sinCalculo.filas()[0]!.querySelector('.importe')!.textContent).toBe('');

    const yenes = pintar('traslado', [{ ...IVA_160, importe: 160 }], 0);
    await yenes.refrescar();
    expect(yenes.filas()[0]!.querySelector('.importe')!.textContent).toBe('160');
  });

  it('agrega y quita renglones, y lo que se agrega es una retención de ISR al 10 % o un traslado de IVA al 16 %', async () => {
    const traslados = pintar('traslado');
    traslados.boton('Agregar traslado').click();
    await traslados.refrescar();
    expect(traslados.filas()).toHaveLength(2);
    expect(traslados.concepto.traslados[1]!.impuesto().value()).toBe('002');
    expect(traslados.concepto.traslados[1]!.tasaOCuota().value()).toBe(0.16);

    traslados.boton('Quitar Traslado 1').click();
    await traslados.refrescar();
    expect(traslados.filas()).toHaveLength(1);
    expect(traslados.filas()[0]!.getAttribute('aria-label')).toBe('Traslado 1');

    const retenciones = pintar('retencion');
    await retenciones.refrescar();
    expect(retenciones.elemento.querySelector('h4')!.textContent).toBe('Impuestos retenidos');
    expect(retenciones.filas()).toHaveLength(0);
    retenciones.boton('Agregar retención').click();
    await retenciones.refrescar();
    expect(retenciones.filas()).toHaveLength(1);
    expect(retenciones.concepto.retenciones[0]!.impuesto().value()).toBe('001');
    expect(retenciones.concepto.retenciones[0]!.tasaOCuota().value()).toBe(0.1);
    expect(retenciones.filas()[0]!.getAttribute('aria-label')).toBe('Retención 1');
  });

  it('un traslado exento no lleva tasa ni importe, solo lo dice', async () => {
    const { concepto, elemento, filas, refrescar } = pintar('traslado', [IVA_160]);
    await refrescar();

    concepto.traslados[0]!.tipoFactor().value.set('Exento');
    await refrescar();

    expect(filas()[0]!.querySelector('input')).toBeNull();
    expect(elemento.querySelector('.exento')!.textContent).toBe('Sin tasa ni cuota');
    expect(filas()[0]!.querySelector('.importe')!.textContent).toBe('Exento');

    concepto.traslados[0]!.tipoFactor().value.set('Tasa');
    await refrescar();
    expect(filas()[0]!.querySelector('input')).not.toBeNull();
  });

  it('la tasa escrita como 16 en lugar de 0.16 se lee como 1,600 % para que se note', async () => {
    const { concepto, filas, refrescar } = pintar('traslado');
    await refrescar();

    concepto.traslados[0]!.tasaOCuota().value.set(16);
    await refrescar();

    expect(filas()[0]!.querySelector('[matTextSuffix]')!.textContent).toBe('1,600 %');
  });

  it('el factor Exento solo se ofrece en los traslados', async () => {
    const opcionesDeFactor = async (tipo: 'traslado' | 'retencion') => {
      const { elemento, filas, boton, refrescar } = pintar(tipo);
      if (tipo === 'retencion') {
        boton('Agregar retención').click();
      }

      await refrescar();
      const selector = filas()[0]!.querySelectorAll<HTMLElement>('mat-select')[1]!;
      selector.querySelector<HTMLElement>('.mat-mdc-select-trigger')!.click();
      await refrescar();
      const opciones = [...document.querySelectorAll('.cdk-overlay-container mat-option')].map((o) => o.textContent!.trim());
      document.querySelectorAll('.cdk-overlay-container').forEach((c) => (c.innerHTML = ''));
      expect(elemento).toBeTruthy();
      return opciones;
    };

    expect(await opcionesDeFactor('traslado')).toEqual(['Tasa', 'Cuota', 'Exento']);
    expect(await opcionesDeFactor('retencion')).toEqual(['Tasa', 'Cuota']);
  });
});
