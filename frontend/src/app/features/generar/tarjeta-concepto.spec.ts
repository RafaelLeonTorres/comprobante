import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { form } from '@angular/forms/signals';
import { MatIconRegistry } from '@angular/material/icon';
import { By, DomSanitizer } from '@angular/platform-browser';

import { CampoCatalogo } from '../../compartido/campo-catalogo';
import { registrarIconos } from '../../core/iconos';
import { provideIdioma } from '../../core/idioma';
import { ConceptoCalculado } from '../../core/modelos';
import { esquemaFormulario, formularioVacio } from './formulario';
import { TarjetaConcepto } from './tarjeta-concepto';

const CALCULADO: ConceptoCalculado = {
  importe: 1000,
  descuento: null,
  traslados: [{ impuesto: '002', tipoFactor: 'Tasa', tasaOCuota: 0.16, base: 1000, importe: 160 }],
  retenciones: [],
};

describe('TarjetaConcepto', () => {
  function pintar(entradas: { numero?: number; calculado?: ConceptoCalculado; puedeQuitar?: boolean } = {}) {
    TestBed.resetTestingModule(); // varias pinturas en la misma prueba: cada una con su propio módulo
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideIdioma()] });
    registrarIconos(TestBed.inject(MatIconRegistry), TestBed.inject(DomSanitizer));
    HTMLElement.prototype.scrollIntoView = vi.fn();
    const modelo = signal(formularioVacio());
    const formulario = TestBed.runInInjectionContext(() => form(modelo, esquemaFormulario));
    const concepto = formulario.conceptos[0]!;
    const fixture = TestBed.createComponent(TarjetaConcepto);
    fixture.componentRef.setInput('concepto', concepto);
    fixture.componentRef.setInput('numero', entradas.numero ?? 1);
    fixture.componentRef.setInput('calculado', entradas.calculado);
    fixture.componentRef.setInput('puedeQuitar', entradas.puedeQuitar ?? false);
    fixture.detectChanges();
    const elemento = fixture.nativeElement as HTMLElement;
    const refrescar = async () => {
      await new Promise((resolve) => setTimeout(resolve));
      TestBed.tick();
      fixture.detectChanges();
    };
    const campo = (etiqueta: string) =>
      [...elemento.querySelectorAll('mat-form-field')]
        .find((f) => f.querySelector('mat-label')?.textContent?.trim() === etiqueta)
        ?.querySelector<HTMLInputElement>('input, textarea');
    const botonDeQuitar = () => [...elemento.querySelectorAll<HTMLButtonElement>('header button')].find((b) => b.textContent!.includes('Quitar concepto'));
    return { fixture, modelo, concepto, elemento, refrescar, campo, botonDeQuitar };
  }

  it('titula la tarjeta con el número del concepto y la asocia con su título', () => {
    const { elemento } = pintar({ numero: 3 });

    const titulo = elemento.querySelector('h3')!;
    expect(titulo.textContent).toBe('Concepto 3');
    expect(elemento.querySelector('section')!.getAttribute('aria-labelledby')).toBe(titulo.id);
  });

  it('con dos tarjetas cada una se titula con su propio identificador', () => {
    const primera = pintar();
    const segunda = TestBed.createComponent(TarjetaConcepto);
    segunda.componentRef.setInput('concepto', primera.concepto);
    segunda.componentRef.setInput('numero', 2);
    segunda.detectChanges();

    expect(segunda.nativeElement.querySelector('h3').id).not.toBe(primera.elemento.querySelector('h3')!.id);
  });

  it('muestra el importe del concepto que calculó la API y nada antes de eso', () => {
    expect(pintar().elemento.querySelector('header .importe')).toBeNull();
    expect(pintar({ calculado: CALCULADO }).elemento.querySelector('header .importe')!.textContent!.replace(/\s+/g, ' ').trim()).toBe('Importe 1,000.00');
  });

  it('el botón de quitar solo existe si hay más de un concepto y avisa con su salida', () => {
    expect(pintar({ puedeQuitar: false }).botonDeQuitar()).toBeUndefined();

    const { fixture, botonDeQuitar } = pintar({ puedeQuitar: true });
    let avisos = 0;
    fixture.componentInstance.quitar.subscribe(() => avisos++);
    botonDeQuitar()!.click();

    expect(avisos).toBe(1);
  });

  it('escribir en los campos cambia el modelo del concepto', async () => {
    const { modelo, campo, refrescar } = pintar();

    const cantidad = campo('Cantidad')!;
    cantidad.value = '2.5';
    cantidad.dispatchEvent(new Event('input'));
    const valor = campo('Valor unitario')!;
    valor.value = '99.99';
    valor.dispatchEvent(new Event('input'));
    const descripcion = campo('Descripción')!;
    descripcion.value = 'Servicio de consultoría';
    descripcion.dispatchEvent(new Event('input'));
    await refrescar();

    expect(modelo().conceptos[0]).toMatchObject({ cantidad: 2.5, valorUnitario: 99.99, descripcion: 'Servicio de consultoría' });
  });

  it('al elegir una clave de unidad rellena «Unidad» con su nombre solo si estaba vacía', async () => {
    const { fixture, modelo, concepto, refrescar } = pintar();
    const claveDeUnidad = fixture.debugElement.queryAll(By.directive(CampoCatalogo))[1]!;

    claveDeUnidad.triggerEventHandler('seleccionada', { clave: 'E48', nombre: 'Unidad de servicio' });
    await refrescar();
    expect(modelo().conceptos[0]!.unidad).toBe('Unidad de servicio');

    concepto.unidad().value.set('Mi unidad');
    claveDeUnidad.triggerEventHandler('seleccionada', { clave: 'H87', nombre: 'Pieza' });
    await refrescar();
    expect(modelo().conceptos[0]!.unidad).toBe('Mi unidad');
  });

  it('trae los impuestos trasladados y retenidos del concepto', () => {
    const { elemento } = pintar();

    expect([...elemento.querySelectorAll('app-impuestos-concepto h4')].map((h) => h.textContent)).toEqual(['Impuestos trasladados', 'Impuestos retenidos']);
  });
});
