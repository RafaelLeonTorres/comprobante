import { DatePipe, DecimalPipe } from '@angular/common';
import {
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  linkedSignal,
  resource,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatFormField, MatLabel, MatPrefix, MatSuffix } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { MatProgressBar } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';

import { ServicioCatalogos } from '../../core/catalogos';
import { aErrorApi } from '../../core/errores-api';
import { EstadoApi } from '../../core/estado-api';
import { PaginaCatalogo } from '../../core/modelos';
import { retrasada } from '../../core/senales';

const FILAS_POR_PAGINA = 25;

/** Lo que llegó de la API junto con lo que se preguntó, para anunciar «12 resultados para "gastos"» sin mezclar consultas. */
interface Resultado {
  catalogo: string;
  texto: string;
  pagina: PaginaCatalogo;
}

type Celda = string | number | boolean | null | undefined;

/**
 * Los catálogos del SAT que hay en la base de datos: la lista y, de cada uno, una tabla con búsqueda y páginas. Todo el
 * estado vive en la URL (<c>/catalogos?catalogo=uso-cfdi&amp;q=gastos&amp;pagina=2</c>): se puede compartir un enlace y el botón
 * de atrás hace lo que se espera.
 */
@Component({
  selector: 'app-catalogos',
  imports: [DatePipe, DecimalPipe, MatButton, MatFormField, MatIcon, MatIconButton, MatInput, MatLabel, MatPrefix, MatProgressBar, MatSuffix, RouterLink],
  template: `
    <h1 #tituloPagina tabindex="-1">Catálogos del SAT</h1>
    <p class="intro">
      Los catálogos de <code>catCFDI</code> que publica el SAT están cargados en PostgreSQL. De ahí salen las validaciones del
      validador y las sugerencias de los campos con clave.
      @if (version(); as publicado) {
        Versión publicada el {{ publicado.publicacion | date: 'longDate' }} (<code>{{ publicado.archivo }}</code>).
      }
    </p>

    @if (resumen.error()) {
      <p class="falla" role="alert">
        No se pudo leer la lista de catálogos. Revisa que la API esté corriendo (<code>docker compose ps</code>) y vuelve a cargar la página.
      </p>
    } @else if (!resumen.hasValue()) {
      <p class="nota" role="status">Cargando catálogos…</p>
    } @else if (!catalogo()) {
      <section aria-labelledby="lista-titulo">
        <h2 id="lista-titulo">
          {{ lista().length }} catálogos y {{ totalDeFilas() | number }} filas
        </h2>
        <ul class="tarjetas">
          @for (entrada of lista(); track entrada.nombre) {
            <li>
              <a class="tarjeta" routerLink="/catalogos" [queryParams]="{ catalogo: entrada.nombre }">
                <span class="nombre">{{ entrada.titulo }}</span>{{ ' ' }}
                <span class="filas">{{ entrada.filas | number }} {{ entrada.filas === 1 ? 'fila' : 'filas' }}</span>
              </a>
            </li>
          }
        </ul>
      </section>
    } @else if (!elegido()) {
      <section class="falla" role="alert">
        <p>No existe el catálogo «{{ catalogo() }}».</p>
        <a matButton routerLink="/catalogos">Ver todos los catálogos</a>
      </section>
    } @else {
      @let actual = elegido()!;
      <section class="detalle" aria-labelledby="titulo-catalogo">
        <a matButton class="volver" routerLink="/catalogos">
          <mat-icon svgIcon="siguiente" class="atras" aria-hidden="true" />
          Todos los catálogos
        </a>
        <h2 id="titulo-catalogo" #tituloCatalogo tabindex="-1">{{ actual.titulo }}</h2>
        <p class="nota">{{ actual.filas | number }} {{ actual.filas === 1 ? 'fila' : 'filas' }}.</p>

        <form class="busqueda" role="search" aria-label="Buscar en el catálogo" (submit)="alEnviar($event)">
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>Buscar por clave o descripción</mat-label>
            <mat-icon matPrefix svgIcon="buscar" aria-hidden="true" />
            <input #caja matInput type="search" autocomplete="off" spellcheck="false" [value]="texto()" (input)="texto.set(caja.value)" />
            @if (texto()) {
              <button matIconButton matSuffix type="button" aria-label="Borrar la búsqueda" (click)="borrarBusqueda(caja)">
                <mat-icon svgIcon="cerrar" aria-hidden="true" />
              </button>
            }
          </mat-form-field>
        </form>

        <p class="estado-busqueda" role="status">
          @if (resultado(); as r) {
            {{ r.pagina.total | number }} {{ r.pagina.total === 1 ? 'resultado' : 'resultados' }}
            @if (r.texto) {
              para «{{ r.texto }}»
            }
            @if (totalDePaginas() > 1) {
              · página {{ r.pagina.pagina | number }} de {{ totalDePaginas() | number }}
            }
          }
        </p>

        @if (cargando()) {
          <mat-progress-bar mode="indeterminate" aria-label="Cargando filas" />
        }

        @if (falla(); as error) {
          <p class="falla" role="alert">{{ error.titulo }}@if (error.detalle) {: {{ error.detalle }}}</p>
        } @else if (resultado(); as r) {
          @if (r.pagina.filas.length > 0) {
            <div class="tabla" [class.atenuada]="cargando()" role="region" tabindex="0" [attr.aria-label]="'Filas de ' + actual.titulo">
              <table aria-labelledby="titulo-catalogo">
                <thead>
                  <tr>
                    @for (columna of r.pagina.columnas; track columna.clave) {
                      <th scope="col">{{ columna.titulo }}</th>
                    }
                  </tr>
                </thead>
                <tbody>
                  @for (fila of r.pagina.filas; track $index) {
                    <tr>
                      @for (columna of r.pagina.columnas; track columna.clave) {
                        <td [class.mono]="columna.clave === 'clave'">{{ mostrar(fila[columna.clave]) }}</td>
                      }
                    </tr>
                  }
                </tbody>
              </table>
            </div>

            @if (totalDePaginas() > 1) {
              <nav class="paginacion" aria-label="Páginas de resultados">
                <button matButton type="button" [disabled]="paginaActual() <= 1" disabledInteractive (click)="irAPagina(paginaActual() - 1)">
                  Anterior
                </button>
                <span>Página {{ paginaActual() | number }} de {{ totalDePaginas() | number }}</span>
                <button
                  matButton
                  type="button"
                  [disabled]="paginaActual() >= totalDePaginas()"
                  disabledInteractive
                  (click)="irAPagina(paginaActual() + 1)"
                >
                  Siguiente
                </button>
              </nav>
            }
          } @else if (r.pagina.total > 0) {
            <p class="nota">
              Esta página no existe: el catálogo tiene {{ totalDePaginas() | number }}
              {{ totalDePaginas() === 1 ? 'página' : 'páginas' }} con esta búsqueda.
              <button matButton type="button" (click)="irAPagina(1)">Ir a la primera</button>
            </p>
          } @else {
            <p class="nota">
              Ninguna fila coincide con «{{ r.texto }}».
              <button matButton type="button" (click)="borrarBusqueda(caja)">Borrar la búsqueda</button>
            </p>
          }
        }
      </section>
    }
  `,
  styles: `
    :host {
      display: block;
      box-sizing: border-box;
      max-width: 72rem;
      margin: 0 auto;
      padding: 32px 24px 48px;
    }

    h1 {
      margin: 0 0 8px;
      font: var(--mat-sys-headline-medium);
      outline: none;
    }

    h2 {
      margin: 0 0 12px;
      font: var(--mat-sys-title-large);
      outline: none;
    }

    .intro {
      max-width: 52rem;
      margin: 0 0 24px;
      color: var(--mat-sys-on-surface-variant);
    }

    .nota {
      margin: 0 0 12px;
      color: var(--mat-sys-on-surface-variant);
    }

    .falla {
      margin: 0 0 16px;
      padding: 12px 16px;
      border-radius: var(--mat-sys-corner-medium);
      background-color: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);

      p {
        margin: 0 0 8px;
      }
    }

    .tarjetas {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(16rem, 1fr));
      gap: 12px;
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .tarjeta {
      display: flex;
      flex-direction: column;
      gap: 4px;
      box-sizing: border-box;
      height: 100%;
      padding: 16px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--mat-sys-corner-medium);
      background-color: var(--mat-sys-surface-container-lowest);
      color: inherit;
      text-decoration: none;

      &:hover {
        background-color: var(--mat-sys-surface-container-low);
      }

      .nombre {
        font: var(--mat-sys-title-medium);
        color: var(--mat-sys-primary);
      }

      .filas {
        font: var(--mat-sys-body-small);
        color: var(--mat-sys-on-surface-variant);
      }
    }

    .volver {
      margin: 0 0 8px -12px;

      .atras {
        transform: rotate(180deg);
      }
    }

    .busqueda mat-form-field {
      width: 100%;
      max-width: 32rem;
    }

    input[type='search']::-webkit-search-cancel-button {
      display: none;
    }

    .estado-busqueda {
      min-height: 1.5em;
      margin: 8px 0;
      color: var(--mat-sys-on-surface-variant);
    }

    .tabla {
      overflow-x: auto;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--mat-sys-corner-medium);
      transition: opacity 120ms;

      &.atenuada {
        opacity: 0.55;
      }

      &:focus-visible {
        outline: 2px solid var(--mat-sys-primary);
        outline-offset: 2px;
      }
    }

    table {
      width: 100%;
      border-collapse: collapse;
    }

    th {
      padding: 12px 16px;
      background-color: var(--mat-sys-surface-container);
      font: var(--mat-sys-title-small);
      text-align: start;
      white-space: nowrap;
    }

    td {
      padding: 10px 16px;
      border-top: 1px solid var(--mat-sys-outline-variant);
      vertical-align: top;
    }

    td.mono {
      white-space: nowrap;
    }

    tbody tr:hover {
      background-color: var(--mat-sys-surface-container-low);
    }

    .paginacion {
      display: flex;
      flex-wrap: wrap;
      gap: 8px 16px;
      align-items: center;
      justify-content: center;
      margin-top: 16px;
    }
  `,
})
export class Catalogos {
  private readonly router = inject(Router);
  private readonly servicio = inject(ServicioCatalogos);
  private readonly injector = inject(Injector);

  // Los tres llegan de la URL por el enlace de rutas a entradas: /catalogos?catalogo=uso-cfdi&q=gastos&pagina=2
  readonly catalogo = input<string | undefined>(undefined);
  readonly q = input<string | undefined>(undefined);
  readonly pagina = input<string | undefined>(undefined);

  protected readonly tituloPagina = viewChild<ElementRef<HTMLElement>>('tituloPagina');
  protected readonly tituloCatalogo = viewChild<ElementRef<HTMLElement>>('tituloCatalogo');

  protected readonly resumen = inject(EstadoApi).catalogos();
  protected readonly lista = computed(() => (this.resumen.hasValue() ? this.resumen.value().catalogos : []));
  protected readonly version = computed(() => (this.resumen.hasValue() ? this.resumen.value().version : null));
  protected readonly totalDeFilas = computed(() => this.lista().reduce((suma, c) => suma + c.filas, 0));
  protected readonly elegido = computed(() => this.lista().find((c) => c.nombre === this.catalogo()));

  /** Lo que dice la caja de búsqueda: va por delante de la URL hasta que el usuario deja de teclear. */
  protected readonly texto = signal('');
  /** Lo último que esta pantalla puso en la URL: distingue un cambio propio (teclear) de uno ajeno (el botón de atrás). */
  private ultimoEnviado = '';

  private readonly numeroDePagina = computed(() => {
    const numero = Number.parseInt(this.pagina() ?? '', 10);
    return Number.isFinite(numero) && numero >= 1 ? numero : 1;
  });

  private readonly respuesta = resource({
    params: () => {
      const elegido = this.elegido();
      return elegido ? { catalogo: elegido.nombre, texto: (this.q() ?? '').trim(), pagina: this.numeroDePagina() } : undefined;
    },
    loader: async ({ params }): Promise<Resultado> => ({
      catalogo: params.catalogo,
      texto: params.texto,
      pagina: await this.servicio.buscar(params.catalogo, { texto: params.texto, pagina: params.pagina, tamano: FILAS_POR_PAGINA }),
    }),
  });

  protected readonly cargando = this.respuesta.isLoading;
  protected readonly falla = computed(() => (this.respuesta.error() ? aErrorApi(this.respuesta.error()) : null));
  /** Lo último que llegó del mismo catálogo: mientras llega la nueva página, la anterior sigue a la vista, atenuada. */
  protected readonly resultado = linkedSignal<{ catalogo: string | undefined; valor: Resultado | undefined }, Resultado | undefined>({
    source: () => ({ catalogo: this.elegido()?.nombre, valor: this.respuesta.hasValue() ? this.respuesta.value() : undefined }),
    computation: (fuente, anterior) => fuente.valor ?? (anterior && anterior.source.catalogo === fuente.catalogo ? anterior.value : undefined),
  });

  protected readonly paginaActual = computed(() => this.resultado()?.pagina.pagina ?? this.numeroDePagina());
  protected readonly totalDePaginas = computed(() => {
    const pagina = this.resultado()?.pagina;
    return pagina ? Math.max(1, Math.ceil(pagina.total / pagina.tamano)) : 1;
  });

  constructor() {
    // URL → caja: al abrir un enlace con ?q=... o al volver atrás, la caja dice lo que dice la URL.
    effect(() => {
      const deLaUrl = this.q() ?? '';
      if (deLaUrl !== this.ultimoEnviado) {
        this.ultimoEnviado = deLaUrl;
        this.texto.set(deLaUrl);
      }
    });

    // Caja → URL: cuando se deja de teclear, la búsqueda pasa a la URL y se vuelve a la primera página.
    const tecleado = retrasada(this.texto, 300);
    effect(() => {
      const valor = tecleado();
      untracked(() => this.buscar(valor));
    });

    // Al elegir un catálogo (o volver a la lista) el contenido cambia de lugar: el foco va al título nuevo. Al abrir la
    // pantalla, directo o con un enlace, no se toca.
    let anterior: string | null | undefined;
    effect(() => {
      const actual = this.catalogo() || null;
      const cambio = anterior !== undefined && anterior !== actual;
      anterior = actual;
      if (cambio) {
        afterNextRender(
          () => (actual ? this.tituloCatalogo() : this.tituloPagina())?.nativeElement.focus(),
          { injector: this.injector },
        );
      }
    });
  }

  protected mostrar(celda: Celda): string {
    if (typeof celda === 'boolean') {
      return celda ? 'Sí' : 'No';
    }

    return celda === null || celda === undefined ? '' : String(celda);
  }

  protected alEnviar(evento: Event): void {
    evento.preventDefault();
    this.buscar(this.texto());
  }

  protected borrarBusqueda(caja: HTMLInputElement | undefined): void {
    this.texto.set('');
    this.buscar('');
    caja?.focus();
  }

  protected irAPagina(pagina: number): void {
    void this.router.navigate([], { queryParams: { pagina: pagina > 1 ? pagina : null }, queryParamsHandling: 'merge' });
  }

  private buscar(valor: string): void {
    const limpio = valor.trim();
    if (limpio === this.ultimoEnviado) {
      return;
    }

    this.ultimoEnviado = limpio;
    void this.router.navigate([], {
      queryParams: { q: limpio || null, pagina: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }
}
