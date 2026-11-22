import { DOCUMENT, Injectable, computed, inject, signal } from '@angular/core';

export type Tema = 'claro' | 'oscuro';

const CLAVE = 'comprobante.tema';

/**
 * El tema de color de la página. Sin elección sigue al sistema (prefers-color-scheme); el botón del encabezado fija uno,
 * que se guarda en el navegador y se pone en data-tema de <html>, donde styles.scss lo traduce a color-scheme. Como el
 * sitio, la app se ve en claro u oscuro y respeta lo que el visitante pida.
 */
@Injectable({ providedIn: 'root' })
export class TemaColor {
  private readonly documento = inject(DOCUMENT);
  private readonly sistemaOscuro = this.documento.defaultView?.matchMedia?.('(prefers-color-scheme: dark)');
  private readonly elegido = signal<Tema | null>(this.leer());
  private readonly sistemaEsOscuro = signal(this.sistemaOscuro?.matches ?? false);

  /** El tema que se ve ahora: el elegido o, mientras no se elija, el del sistema. */
  readonly actual = computed<Tema>(() => this.elegido() ?? (this.sistemaEsOscuro() ? 'oscuro' : 'claro'));

  constructor() {
    this.aplicar();
    this.sistemaOscuro?.addEventListener('change', (evento) => this.sistemaEsOscuro.set(evento.matches));
  }

  /** Cambia al otro tema y lo recuerda. */
  alternar(): void {
    const nuevo: Tema = this.actual() === 'oscuro' ? 'claro' : 'oscuro';
    this.elegido.set(nuevo);
    this.guardar(nuevo);
    this.aplicar();
  }

  private aplicar(): void {
    const raiz = this.documento.documentElement;
    const tema = this.elegido();
    if (tema) {
      raiz.dataset['tema'] = tema;
    } else {
      delete raiz.dataset['tema'];
    }
  }

  private leer(): Tema | null {
    try {
      const guardado = this.documento.defaultView?.localStorage.getItem(CLAVE);
      return guardado === 'claro' || guardado === 'oscuro' ? guardado : null;
    } catch {
      return null;
    }
  }

  private guardar(tema: Tema): void {
    try {
      this.documento.defaultView?.localStorage.setItem(CLAVE, tema);
    } catch {
      // Sin almacenamiento (modo privado, bloqueado): el tema vale hasta cerrar la página.
    }
  }
}
