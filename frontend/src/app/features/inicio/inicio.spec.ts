import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';

import { registrarIconos } from '../../core/iconos';
import { provideIdioma } from '../../core/idioma';
import { Inicio } from './inicio';

describe('Inicio', () => {
  function pintar() {
    TestBed.configureTestingModule({
      imports: [Inicio],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideIdioma(), provideRouter([])],
    });
    registrarIconos(TestBed.inject(MatIconRegistry), TestBed.inject(DomSanitizer));
    const fixture = TestBed.createComponent(Inicio);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    TestBed.tick();

    const refrescar = async () => {
      TestBed.tick();
      await fixture.whenStable();
      fixture.detectChanges();
    };
    return { elemento: fixture.nativeElement as HTMLElement, http, refrescar };
  }

  it('explica que no timbra', () => {
    const { elemento } = pintar();

    expect(elemento.querySelector('h1')!.textContent).toContain('CFDI 4.0');
    expect(elemento.querySelector('[role="note"]')!.textContent).toContain('Sin validez fiscal');
  });

  it('ofrece los tres caminos: generar, validar y consultar los catálogos', () => {
    const { elemento } = pintar();

    const caminos = [...elemento.querySelectorAll<HTMLAnchorElement>('.caminos a')];
    expect(caminos.map((a) => a.getAttribute('href'))).toEqual(['/generar', '/validar', '/catalogos']);
    expect(caminos.map((a) => a.querySelector('.titulo')!.textContent)).toEqual(['Generar un CFDI', 'Validar un XML', 'Consultar los catálogos']);
    expect(elemento.querySelector('.caminos')!.getAttribute('aria-labelledby')).toBe('caminos-titulo');
  });

  it('mientras pregunta a la API dice que está comprobando', () => {
    const { elemento } = pintar();

    expect(elemento.textContent).toContain('Comprobando la API');
  });

  it('muestra que la API responde y la versión de los catálogos', async () => {
    const { elemento, http, refrescar } = pintar();

    http.expectOne('/api/health').flush({ estado: 'ok' });
    http.expectOne('/api/catalogos').flush({
      version: { archivo: 'catCFDI_V_4_20261120.xls', publicacion: '2026-11-20', cargadoEn: '2026-11-21T20:00:00Z' },
      catalogos: [
        { nombre: 'moneda', titulo: 'Moneda (c_Moneda)', filas: 183 },
        { nombre: 'clave-prod-serv', titulo: 'Clave de producto o servicio', filas: 52513 },
      ],
    });
    await refrescar();

    expect(elemento.textContent).toContain('La API responde');
    expect(elemento.textContent).toContain('20 de noviembre de 2026');
    expect(elemento.textContent).toContain('catCFDI_V_4_20261120.xls');
    expect(elemento.textContent).toContain('2 catálogos y 52,696 filas');
  });

  it('avisa cuando la API no responde', async () => {
    const { elemento, http, refrescar } = pintar();

    http.expectOne('/api/health').flush(null, { status: 502, statusText: 'Bad Gateway' });
    http.expectOne('/api/catalogos').flush(null, { status: 502, statusText: 'Bad Gateway' });
    await refrescar();

    const alerta = elemento.querySelector('[role="alert"]')!;
    expect(alerta.textContent).toContain('La API no responde');
    expect(alerta.textContent).toContain('docker compose ps');
  });
});
