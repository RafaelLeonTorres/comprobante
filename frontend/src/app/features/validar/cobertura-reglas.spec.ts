import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';

import { registrarIconos } from '../../core/iconos';
import { CoberturaDeReglas } from '../../core/modelos';
import { CoberturaReglas } from './cobertura-reglas';

const COBERTURA: CoberturaDeReglas = {
  resumen: { total: 4, revisadas: 2, parciales: 1, noRevisadas: 1 },
  reglas: [
    { codigo: 'CFDI40101', elemento: 'Comprobante', atributo: 'Fecha', descripcion: 'El campo Fecha no cumple con el patrón requerido.', estado: 'revisada' },
    { codigo: 'CFDI40151', elemento: 'Receptor', atributo: 'ResidenciaFiscal', descripcion: 'La ResidenciaFiscal debe estar vacía.', estado: 'parcial', nota: 'Solo el RFC genérico extranjero.' },
    { codigo: 'CFDI40138', elemento: 'Receptor', atributo: 'Rfc', descripcion: 'El RFC debe estar en la LRFC.', estado: 'noRevisada', nota: 'Requiere la lista de RFC inscritos (LRFC).' },
    { codigo: 'CFDI40209', elemento: 'Comprobante', atributo: null, descripcion: 'Misma condición.', estado: 'revisada', cubiertaPor: 'CFDI40206' },
  ],
};

describe('CoberturaReglas', () => {
  function pintar() {
    TestBed.configureTestingModule({ imports: [CoberturaReglas], providers: [provideHttpClient(), provideHttpClientTesting()] });
    registrarIconos(TestBed.inject(MatIconRegistry), TestBed.inject(DomSanitizer));
    const fixture = TestBed.createComponent(CoberturaReglas);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    /** Deja correr lo que Angular no sigue (promesas de la API) y pinta. Solo con las peticiones ya respondidas. */
    const refrescar = async () => {
      await new Promise((resolve) => setTimeout(resolve));
      TestBed.tick();
      await fixture.whenStable();
      fixture.detectChanges();
    };
    const elemento = fixture.nativeElement as HTMLElement;
    /** Abre el bloque. No espera: la petición queda pendiente hasta que la prueba la responda. */
    const abrir = () => {
      const detalles = elemento.querySelector('details')!;
      detalles.open = true;
      detalles.dispatchEvent(new Event('toggle'));
      TestBed.tick();
      fixture.detectChanges();
    };
    const filas = () => [...elemento.querySelectorAll('tbody tr')].map((fila) => fila.querySelector('th')?.textContent?.trim());
    return { elemento, http, refrescar, abrir, filas };
  }

  it('no pide las reglas hasta que se abre el bloque', () => {
    const { http, abrir } = pintar();

    http.expectNone('/api/validaciones/reglas');
    abrir();

    http.expectOne('/api/validaciones/reglas');
  });

  it('muestra el resumen y todas las reglas con su estado y su nota', async () => {
    const { elemento, http, abrir, refrescar, filas } = pintar();
    abrir();

    http.expectOne('/api/validaciones/reglas').flush(COBERTURA);
    await refrescar();

    expect(elemento.querySelector('.resumen')!.textContent).toContain('De las 4 reglas de la matriz, la app revisa 2 completas y 1 en parte');
    expect(filas()).toEqual(['CFDI40101', 'CFDI40151', 'CFDI40138', 'CFDI40209']);
    const noRevisada = elemento.querySelectorAll('tbody tr')[2]!;
    expect(noRevisada.querySelector('.estado')!.textContent).toContain('No revisada');
    expect(noRevisada.textContent).toContain('Requiere la lista de RFC inscritos (LRFC).');
    expect(elemento.querySelectorAll('tbody tr')[3]!.textContent).toContain('Se informa con el código CFDI40206.');
    expect(elemento.querySelector('.conteo')!.textContent).toContain('Se muestran 4 reglas.');
  });

  it('filtra por estado', async () => {
    const { elemento, http, abrir, refrescar, filas } = pintar();
    abrir();
    http.expectOne('/api/validaciones/reglas').flush(COBERTURA);
    await refrescar();

    elemento.querySelectorAll<HTMLButtonElement>('mat-button-toggle button')[3]!.click();
    await refrescar();

    expect(filas()).toEqual(['CFDI40138']);
    expect(elemento.querySelector('.conteo')!.textContent).toContain('Se muestra 1 regla.');
  });

  it('busca por código o por texto, sin importar mayúsculas', async () => {
    const { elemento, http, abrir, refrescar, filas } = pintar();
    abrir();
    http.expectOne('/api/validaciones/reglas').flush(COBERTURA);
    await refrescar();

    const campo = elemento.querySelector<HTMLInputElement>('input[type="search"]')!;
    campo.value = 'lrfc';
    campo.dispatchEvent(new Event('input'));
    await refrescar();
    expect(filas()).toEqual(['CFDI40138']);

    campo.value = 'cfdi4015';
    campo.dispatchEvent(new Event('input'));
    await refrescar();
    expect(filas()).toEqual(['CFDI40151']);

    campo.value = 'nada de esto';
    campo.dispatchEvent(new Event('input'));
    await refrescar();
    expect(filas()).toEqual([undefined]);
    expect(elemento.querySelector('tbody')!.textContent).toContain('Ninguna regla coincide');
  });

  it('si la API falla lo dice', async () => {
    const { elemento, http, abrir, refrescar } = pintar();
    abrir();

    http.expectOne('/api/validaciones/reglas').flush(null, { status: 500, statusText: 'Server Error' });
    await refrescar();

    expect(elemento.querySelector('[role="alert"]')!.textContent).toContain('No se pudo cargar la lista de reglas');
  });
});
