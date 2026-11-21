import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Injector, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { form, required } from '@angular/forms/signals';

import { FilaCatalogo, PaginaCatalogo } from '../core/modelos';
import { CampoCatalogo } from './campo-catalogo';

function pagina(...filas: FilaCatalogo[]): PaginaCatalogo {
  return {
    total: filas.length,
    pagina: 1,
    tamano: 10,
    columnas: [
      { clave: 'clave', titulo: 'Clave' },
      { clave: 'descripcion', titulo: 'Descripción' },
    ],
    filas,
  };
}

const G01 = { clave: 'G01', descripcion: 'Adquisición de mercancías.' };
const G03 = { clave: 'G03', descripcion: 'Gastos en general.' };

describe('CampoCatalogo', () => {
  function pintar(valorInicial = '', entradas: { minimo?: number; ayuda?: string } = {}) {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const modelo = signal({ uso: valorInicial });
    const formulario = TestBed.runInInjectionContext(() =>
      form(modelo, (ruta) => {
        required(ruta.uso, { message: 'Elige el uso del CFDI.' });
      }),
    );
    const fixture = TestBed.createComponent(CampoCatalogo);
    fixture.componentRef.setInput('campo', formulario.uso);
    fixture.componentRef.setInput('catalogo', 'uso-cfdi');
    fixture.componentRef.setInput('etiqueta', 'Uso del CFDI');
    if (entradas.minimo !== undefined) {
      fixture.componentRef.setInput('minimo', entradas.minimo);
    }

    if (entradas.ayuda !== undefined) {
      fixture.componentRef.setInput('ayuda', entradas.ayuda);
    }

    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    const elemento = fixture.nativeElement as HTMLElement;
    const entrada = () => elemento.querySelector<HTMLInputElement>('input')!;
    const refrescar = async () => {
      await new Promise((resolve) => setTimeout(resolve));
      TestBed.tick();
      await fixture.whenStable();
      fixture.detectChanges();
    };
    const consulta = (texto?: string) =>
      vi.waitFor(() => {
        const encontradas = http.match(
          (r) => r.url === '/api/catalogos/uso-cfdi' && (texto === undefined ? !r.params.has('q') : r.params.get('q') === texto),
        );
        if (encontradas.length === 0) {
          throw new Error(`todavía no hay consulta de ${texto ?? '(todo)'}`);
        }

        return encontradas[0]!;
      });
    const opciones = () => [...document.querySelectorAll('mat-option')].map((o) => o.textContent!.replace(/\s+/g, ' ').trim());
    return { fixture, modelo, formulario, http, elemento, entrada, refrescar, consulta, opciones, injector: TestBed.inject(Injector) };
  }

  afterEach(() => {
    document.querySelectorAll('.cdk-overlay-container').forEach((c) => (c.innerHTML = ''));
  });

  it('no consulta nada hasta que el campo recibe el foco', async () => {
    const { http, refrescar } = pintar();

    await new Promise((resolve) => setTimeout(resolve, 350));
    await refrescar();

    http.expectNone((r) => r.url === '/api/catalogos/uso-cfdi');
  });

  it('con el foco sugiere y al elegir deja la clave en el campo y emite la fila', async () => {
    const { fixture, modelo, entrada, consulta, refrescar, opciones } = pintar();
    const elegidas: FilaCatalogo[] = [];
    fixture.componentInstance.seleccionada.subscribe((fila) => elegidas.push(fila));

    entrada().focus();
    (await consulta()).flush(pagina(G01, G03));
    await refrescar();

    expect(opciones()).toEqual(['G01 Adquisición de mercancías.', 'G03 Gastos en general.']);
    document.querySelectorAll<HTMLElement>('mat-option')[1]!.click();
    await refrescar();

    expect(modelo().uso).toBe('G03');
    expect(entrada().value).toBe('G03');
    expect(elegidas).toEqual([G03]);
  });

  it('al escribir busca ese texto, con un momento de espera para no preguntar en cada tecla', async () => {
    const { http, entrada, consulta, refrescar } = pintar();
    entrada().focus();
    (await consulta()).flush(pagina(G01, G03));
    await refrescar();

    for (const texto of ['g', 'g0', 'g03']) {
      entrada().value = texto;
      entrada().dispatchEvent(new Event('input'));
    }

    const llamada = await consulta('g03');
    llamada.flush(pagina(G03));
    http.expectNone((r) => r.params.get('q') === 'g' || r.params.get('q') === 'g0');
  });

  it('con un mínimo de caracteres no sugiere antes de llegar a él', async () => {
    const { http, entrada, refrescar, opciones } = pintar('', { minimo: 3 });

    entrada().focus();
    entrada().value = 'ab';
    entrada().dispatchEvent(new Event('input'));
    await new Promise((resolve) => setTimeout(resolve, 350));
    await refrescar();

    http.expectNone((r) => r.params.get('q') === 'ab');
    expect(opciones()).toEqual([]);
  });

  it('con una clave puesta de antemano dice qué significa, sin haber tocado el campo', async () => {
    const { elemento, consulta, refrescar } = pintar('G03');

    (await consulta('G03')).flush(pagina(G03));
    await vi.waitFor(async () => {
      await refrescar();
      expect(elemento.querySelector('mat-hint')?.textContent).toContain('Gastos en general.');
    });
  });

  it('una clave que no es del catálogo se avisa solo después de salir del campo', async () => {
    const { elemento, formulario, consulta, refrescar } = pintar('Z99');

    (await consulta('Z99')).flush(pagina());
    await new Promise((resolve) => setTimeout(resolve, 20));
    await refrescar();
    expect(elemento.querySelector('mat-hint')).toBeNull();

    formulario.uso().markAsTouched();
    await refrescar();

    expect(elemento.querySelector('mat-hint')!.textContent).toContain('La clave Z99 no aparece en este catálogo.');
  });

  it('sin descripción que mostrar enseña la ayuda', async () => {
    const { elemento } = pintar('', { ayuda: 'Por ejemplo G03' });

    expect(elemento.querySelector('mat-hint')!.textContent).toContain('Por ejemplo G03');
  });

  it('muestra el primer error del campo cuando se toca', async () => {
    const { elemento, formulario, refrescar } = pintar();

    formulario.uso().markAsTouched();
    await refrescar();

    expect(elemento.querySelector('mat-error')!.textContent).toContain('Elige el uso del CFDI.');
  });
});
