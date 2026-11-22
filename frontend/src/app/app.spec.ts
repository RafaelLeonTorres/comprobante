import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';

import { App } from './app';
import { registrarIconos } from './core/iconos';

describe('App', () => {
  beforeEach(() => {
    document.defaultView!.localStorage.clear();
    delete document.documentElement.dataset['tema'];
  });

  function pintarFixture() {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    registrarIconos(TestBed.inject(MatIconRegistry), TestBed.inject(DomSanitizer));
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    return fixture;
  }

  function pintar() {
    return pintarFixture().nativeElement as HTMLElement;
  }

  it('muestra la marca, la navegación y el aviso de que no timbra', () => {
    const elemento = pintar();

    expect(elemento.querySelector('.marca')!.textContent).toContain('Comprobante');
    expect(elemento.querySelector('nav[aria-label="Principal"]')).not.toBeNull();
    expect(elemento.querySelector('.pie')!.textContent).toContain('Sin validez fiscal');
    expect(elemento.querySelector('router-outlet')).not.toBeNull();
  });

  it('el enlace al código se abre en otra pestaña sin dar acceso a esta ventana y lo avisa', () => {
    const enlace = pintar().querySelector<HTMLAnchorElement>('.pie a')!;

    expect(enlace.target).toBe('_blank');
    expect(enlace.rel).toContain('noopener');
    // El nombre accesible conserva el texto visible y suma el aviso.
    expect(enlace.getAttribute('aria-label')).toContain(enlace.textContent!.trim());
    expect(enlace.getAttribute('aria-label')).toContain('otra pestaña');
  });

  it('la marca lleva la insignia del sitio, oculta a los lectores de pantalla, y su nombre accesible es el de la app', () => {
    const marca = pintar().querySelector<HTMLAnchorElement>('.marca')!;

    expect(marca.querySelector('.insignia')!.getAttribute('aria-hidden')).toBe('true');
    expect(marca.getAttribute('aria-label')).toBe('Comprobante, ir al inicio');
  });

  it('el botón de tema dice a qué tema cambia, cambia el icono y alterna el tema de la página', () => {
    const fixture = pintarFixture();
    const elemento = fixture.nativeElement as HTMLElement;
    const boton = elemento.querySelector<HTMLButtonElement>('button.tema')!;

    expect(boton.getAttribute('aria-label')).toBe('Cambiar al tema oscuro');

    boton.click();
    fixture.detectChanges();

    expect(boton.getAttribute('aria-label')).toBe('Cambiar al tema claro');
    expect(document.documentElement.dataset['tema']).toBe('oscuro');

    boton.click();
    fixture.detectChanges();

    expect(boton.getAttribute('aria-label')).toBe('Cambiar al tema oscuro');
    expect(document.documentElement.dataset['tema']).toBe('claro');
  });

  it('el enlace de saltar al contenido mueve el foco al contenido principal', () => {
    const elemento = pintar();
    const principal = elemento.querySelector<HTMLElement>('main#contenido')!;

    elemento.querySelector<HTMLAnchorElement>('.saltar')!.click();

    expect(document.activeElement).toBe(principal);
  });
});
