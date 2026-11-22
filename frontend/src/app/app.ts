import { Component, inject } from '@angular/core';
import { MatAnchor } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { TemaColor } from './core/tema';

@Component({
  selector: 'app-root',
  imports: [MatAnchor, MatIcon, RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <!-- Con <base href="/"> un href="#contenido" saldría de la ruta actual: por eso el foco se mueve a mano. -->
    <a class="saltar" href="#contenido" (click)="saltar($event, principal)">Saltar al contenido</a>

    <header class="barra">
      <a class="marca" routerLink="/" aria-label="Comprobante, ir al inicio">
        <span class="insignia" aria-hidden="true">CF</span>
        <span>Comprobante</span>
      </a>
      <nav aria-label="Principal">
        <a
          matButton
          routerLink="/"
          routerLinkActive="activo"
          ariaCurrentWhenActive="page"
          [routerLinkActiveOptions]="{ exact: true }"
        >
          Inicio
        </a>
        <a matButton routerLink="/generar" routerLinkActive="activo" ariaCurrentWhenActive="page">Generar</a>
        <a matButton routerLink="/validar" routerLinkActive="activo" ariaCurrentWhenActive="page">Validar</a>
        <a matButton routerLink="/catalogos" routerLinkActive="activo" ariaCurrentWhenActive="page">Catálogos</a>
      </nav>
      <button
        class="tema"
        type="button"
        [attr.aria-label]="tema.actual() === 'oscuro' ? 'Cambiar al tema claro' : 'Cambiar al tema oscuro'"
        (click)="tema.alternar()"
      >
        <mat-icon [svgIcon]="tema.actual() === 'oscuro' ? 'sol' : 'luna'" aria-hidden="true" />
      </button>
    </header>

    <main #principal id="contenido" tabindex="-1">
      <router-outlet />
    </main>

    <footer class="pie">
      <p>Sin validez fiscal: esta app no timbra ni envía nada al SAT ni a un PAC.</p>
      <a
        href="https://github.com/RafaelLeonTorres/comprobante"
        target="_blank"
        rel="noopener noreferrer"
        aria-label="Código en GitHub (se abre en otra pestaña)"
      >
        Código en GitHub
      </a>
    </footer>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      min-height: 100%;
    }

    .saltar {
      position: absolute;
      top: -48px;
      left: 8px;
      z-index: 10;
      padding: 8px 16px;
      border-radius: var(--mat-sys-corner-medium);
      background-color: var(--mat-sys-primary);
      color: var(--mat-sys-on-primary);
      font-weight: 700;

      &:focus {
        top: 8px;
      }
    }

    /* El encabezado del sitio: insignia morada y nombre en negritas, la navegación a la derecha y una línea por debajo. */
    .barra {
      display: flex;
      flex-wrap: wrap;
      gap: 8px 16px;
      align-items: center;
      padding: 10px clamp(1.25rem, 4vw, 2.5rem);
      border-bottom: 1px solid var(--sitio-line);
      background-color: var(--sitio-paper);
    }

    .marca {
      display: inline-flex;
      gap: 0.6rem;
      align-items: center;
      color: var(--sitio-ink);
      font-weight: 800;
      text-decoration: none;
    }

    .insignia {
      display: grid;
      place-items: center;
      width: 2.1rem;
      height: 2.1rem;
      border-radius: 9px;
      background-color: var(--sitio-accent);
      color: var(--sitio-accent-ink);
      font-size: 0.85rem;
      letter-spacing: 0.02em;
    }

    nav {
      display: flex;
      flex-wrap: wrap;
      gap: 2px;
      margin-left: auto;
    }

    nav a {
      color: var(--sitio-ink-2);
    }

    nav a.activo {
      background-color: var(--mat-sys-secondary-container);
      color: var(--mat-sys-primary);
    }

    /* En el celular la navegación y el botón del tema caben en una sola fila bajo la marca. */
    @media (max-width: 520px) {
      .barra {
        column-gap: 8px;
      }

      nav a {
        padding-inline: 6px;
      }
    }

    .tema {
      display: grid;
      place-items: center;
      width: 2.5rem;
      height: 2.5rem;
      padding: 0;
      border: 1px solid var(--sitio-line);
      border-radius: var(--mat-sys-corner-small);
      background: transparent;
      color: var(--sitio-ink);
      cursor: pointer;

      &:hover {
        border-color: var(--sitio-accent);
        color: var(--sitio-accent);
      }

      &:focus-visible {
        outline: 3px solid var(--sitio-accent);
        outline-offset: 3px;
      }
    }

    main {
      flex: 1;
      outline: none;
    }

    .pie {
      display: flex;
      flex-wrap: wrap;
      gap: 8px 24px;
      justify-content: space-between;
      padding: 28px clamp(1.25rem, 4vw, 2.5rem);
      border-top: 1px solid var(--sitio-line);
      font-size: 0.93rem;
      color: var(--sitio-ink-2);

      p {
        margin: 0;
      }

      /* El relleno da un blanco de pulsación de al menos 24 px de alto (WCAG 2.5.8). */
      a {
        display: inline-block;
        padding-block: 4px;
        color: inherit;
      }
    }
  `,
})
export class App {
  protected readonly tema = inject(TemaColor);

  protected saltar(evento: Event, principal: HTMLElement): void {
    evento.preventDefault();
    principal.focus();
  }
}
