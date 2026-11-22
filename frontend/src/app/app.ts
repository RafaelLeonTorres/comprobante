import { Component } from '@angular/core';
import { MatAnchor } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [MatAnchor, MatIcon, RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <!-- Con <base href="/"> un href="#contenido" saldría de la ruta actual: por eso el foco se mueve a mano. -->
    <a class="saltar" href="#contenido" (click)="saltar($event, principal)">Saltar al contenido</a>

    <header class="barra">
      <a class="marca" routerLink="/" aria-label="Comprobante, ir al inicio">
        <mat-icon svgIcon="documento" aria-hidden="true" />
        <span>Comprobante</span>
      </a>
      <nav aria-label="Principal">
        <a matButton routerLink="/" routerLinkActive="activo" [routerLinkActiveOptions]="{ exact: true }">
          Inicio
        </a>
        <a matButton routerLink="/generar" routerLinkActive="activo">Generar</a>
        <a matButton routerLink="/validar" routerLinkActive="activo">Validar</a>
        <a matButton routerLink="/catalogos" routerLinkActive="activo">Catálogos</a>
      </nav>
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
      border-radius: var(--mat-sys-corner-small);
      background-color: var(--mat-sys-primary);
      color: var(--mat-sys-on-primary);

      &:focus {
        top: 8px;
      }
    }

    .barra {
      display: flex;
      flex-wrap: wrap;
      gap: 8px 24px;
      align-items: center;
      justify-content: space-between;
      padding: 8px 24px;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
      background-color: var(--mat-sys-surface-container);
    }

    .marca {
      display: flex;
      gap: 8px;
      align-items: center;
      font: var(--mat-sys-title-large);
      color: var(--mat-sys-primary);
      text-decoration: none;
    }

    nav a.activo {
      background-color: var(--mat-sys-secondary-container);
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
      padding: 16px 24px;
      border-top: 1px solid var(--mat-sys-outline-variant);
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);

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
  protected saltar(evento: Event, principal: HTMLElement): void {
    evento.preventDefault();
    principal.focus();
  }
}
