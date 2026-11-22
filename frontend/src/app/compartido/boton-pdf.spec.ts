import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { MatDialog } from '@angular/material/dialog';
import { MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';

import { LIMITES_DEL_PDF } from '../core/api-comprobante';
import { registrarIconos } from '../core/iconos';
import { BotonPdf } from './boton-pdf';

const XML = '<?xml version="1.0" encoding="utf-8"?>\n<cfdi:Comprobante Version="4.0"/>';

describe('BotonPdf', () => {
  const original = { crear: URL.createObjectURL, soltar: URL.revokeObjectURL };
  let descargas: string[];

  beforeEach(() => {
    descargas = [];
    URL.createObjectURL = vi.fn(() => 'blob:prueba');
    URL.revokeObjectURL = vi.fn();
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      descargas.push(this.download);
    });
  });

  afterEach(() => {
    TestBed.inject(MatDialog).closeAll();
    document.querySelectorAll('.cdk-overlay-container').forEach((c) => (c.innerHTML = ''));
    vi.restoreAllMocks();
    URL.createObjectURL = original.crear;
    URL.revokeObjectURL = original.soltar;
  });

  function pintar(entradas: { xml?: string; habilitado?: boolean; motivo?: string } = {}) {
    TestBed.configureTestingModule({
      imports: [BotonPdf],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        // Sin animaciones el diálogo se abre y se cierra sin esperar un evento que jsdom no dispara.
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
      ],
    });
    registrarIconos(TestBed.inject(MatIconRegistry), TestBed.inject(DomSanitizer));
    const fixture = TestBed.createComponent(BotonPdf);
    fixture.componentRef.setInput('xml', entradas.xml ?? XML);
    if (entradas.habilitado !== undefined) {
      fixture.componentRef.setInput('habilitado', entradas.habilitado);
    }
    if (entradas.motivo !== undefined) {
      fixture.componentRef.setInput('motivo', entradas.motivo);
    }
    fixture.detectChanges();
    const elemento = fixture.nativeElement as HTMLElement;
    const http = TestBed.inject(HttpTestingController);

    const refrescar = async () => {
      await new Promise((resolve) => setTimeout(resolve));
      TestBed.tick();
      fixture.detectChanges();
    };
    const hasta = (condicion: () => unknown) =>
      vi.waitFor(async () => {
        await refrescar();
        if (!condicion()) {
          throw new Error('todavía no');
        }
      });
    const boton = () => elemento.querySelector<HTMLButtonElement>('button')!;
    const estado = () => elemento.querySelector('[role="status"]')!.textContent!.trim();
    const dialogo = () => document.querySelector('mat-dialog-container');
    const botonDelDialogo = (texto: string) =>
      [...document.querySelectorAll<HTMLButtonElement>('mat-dialog-container button')].find(
        (b) => b.textContent!.replace(/\s+/g, ' ').trim() === texto,
      )!;
    return { fixture, elemento, http, refrescar, hasta, boton, estado, dialogo, botonDelDialogo };
  }

  it('habilitado: el botón avisa que abre un diálogo y lo abre con el XML', async () => {
    const { boton, elemento, refrescar, dialogo } = pintar();

    expect(boton().textContent).toContain('Descargar PDF');
    expect(boton().getAttribute('aria-haspopup')).toBe('dialog');
    expect(boton().getAttribute('aria-disabled')).not.toBe('true');
    expect(boton().hasAttribute('aria-describedby')).toBe(false);
    expect(elemento.querySelector('.nota:not([role])')).toBeNull();
    expect(dialogo()).toBeNull();

    boton().click();
    await refrescar();

    expect(dialogo()).not.toBeNull();
    expect(dialogo()!.querySelector('app-dialogo-pdf h2')!.textContent).toContain('Descargar PDF');
  });

  it('deshabilitado: se queda a la vista, dice por qué y no abre el diálogo', async () => {
    const { boton, elemento, refrescar, dialogo } = pintar({ habilitado: false, motivo: 'El PDF se habilita cuando el XML no tiene errores.' });

    const motivo = elemento.querySelector<HTMLElement>('.nota:not([role])')!;
    expect(motivo.textContent).toBe('El PDF se habilita cuando el XML no tiene errores.');
    expect(boton().getAttribute('aria-disabled')).toBe('true');
    expect(boton().getAttribute('aria-describedby')).toBe(motivo.id);

    boton().click();
    await refrescar();

    expect(dialogo()).toBeNull();
  });

  it('deshabilitado sin motivo no deja un aviso vacío ni una referencia rota', () => {
    const { boton, elemento } = pintar({ habilitado: false });

    expect(boton().getAttribute('aria-disabled')).toBe('true');
    expect(boton().hasAttribute('aria-describedby')).toBe(false);
    expect(elemento.querySelector('.nota:not([role])')).toBeNull();
  });

  it('un XML de 1 MB justos todavía se puede dibujar', () => {
    const { boton } = pintar({ xml: 'a'.repeat(LIMITES_DEL_PDF.xml) });

    expect(boton().getAttribute('aria-disabled')).not.toBe('true');
  });

  it('un XML de más de 1 MB no se puede dibujar, y lo dice', async () => {
    const { boton, elemento, refrescar, dialogo } = pintar({ xml: 'a'.repeat(LIMITES_DEL_PDF.xml + 1) });

    expect(boton().getAttribute('aria-disabled')).toBe('true');
    expect(elemento.querySelector('.nota:not([role])')!.textContent).toBe('El XML pesa más de 1 MB, que es lo que admite el PDF.');

    boton().click();
    await refrescar();
    expect(dialogo()).toBeNull();
  });

  it('el peso se cuenta en bytes: los acentos pesan más que las letras', () => {
    const { boton } = pintar({ xml: 'é'.repeat(LIMITES_DEL_PDF.xml / 2 + 1) });

    expect(boton().getAttribute('aria-disabled')).toBe('true');
  });

  it('al descargar avisa el nombre del archivo en la región de estado y después lo quita', async () => {
    const temporizadores = vi.spyOn(globalThis, 'setTimeout');
    const { http, boton, refrescar, hasta, estado, dialogo, botonDelDialogo } = pintar();
    boton().click();
    await refrescar();

    botonDelDialogo('Descargar PDF').click();
    const llamada = await vi.waitFor(() => {
      const encontradas = http.match((r) => r.url === '/api/representaciones/pdf');
      if (encontradas.length === 0) {
        throw new Error('todavía no hay POST del PDF');
      }

      return encontradas[0]!;
    });
    llamada.flush(new Blob(['%PDF-1.7'], { type: 'application/pdf' }), {
      headers: { 'Content-Disposition': 'attachment; filename="EKU9003173C9-A-123-prueba.pdf"' },
    });
    await hasta(() => estado() !== '');

    expect(descargas).toEqual(['EKU9003173C9-A-123-prueba.pdf']);
    expect(estado()).toBe('Se descargó EKU9003173C9-A-123-prueba.pdf.');
    expect(dialogo()).toBeNull();

    const expira = temporizadores.mock.calls.find(([, espera]) => espera === 8000)!;
    (expira[0] as () => void)();
    await refrescar();
    expect(estado()).toBe('');
  });

  it('cancelar el diálogo cierra sin descargar ni avisar nada', async () => {
    const { http, boton, refrescar, hasta, estado, dialogo, botonDelDialogo } = pintar();
    boton().click();
    await refrescar();

    botonDelDialogo('Cancelar').click();
    await hasta(() => dialogo() === null);

    expect(estado()).toBe('');
    expect(descargas).toEqual([]);
    http.expectNone('/api/representaciones/pdf');
  });
});
