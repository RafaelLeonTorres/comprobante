import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';

import { App } from './app';
import { registrarIconos } from './core/iconos';

describe('App', () => {
  function pintar() {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    registrarIconos(TestBed.inject(MatIconRegistry), TestBed.inject(DomSanitizer));
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('muestra la marca, la navegación y el aviso de que no timbra', () => {
    const elemento = pintar();

    expect(elemento.querySelector('.marca')!.textContent).toContain('Comprobante');
    expect(elemento.querySelector('nav[aria-label="Principal"]')).not.toBeNull();
    expect(elemento.querySelector('.pie')!.textContent).toContain('Sin validez fiscal');
    expect(elemento.querySelector('router-outlet')).not.toBeNull();
  });

  it('el enlace de saltar al contenido mueve el foco al contenido principal', () => {
    const elemento = pintar();
    const principal = elemento.querySelector<HTMLElement>('main#contenido')!;

    elemento.querySelector<HTMLAnchorElement>('.saltar')!.click();

    expect(document.activeElement).toBe(principal);
  });
});
