import { Component, computed, inject, input, linkedSignal, output, resource, signal } from '@angular/core';
import { FieldTree, FormField } from '@angular/forms/signals';
import { MatAutocomplete, MatAutocompleteSelectedEvent, MatAutocompleteTrigger } from '@angular/material/autocomplete';
import { MatOption } from '@angular/material/core';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';

import { ServicioCatalogos, descripcionDe } from '../core/catalogos';
import { FilaCatalogo } from '../core/modelos';
import { retrasada } from '../core/senales';

/**
 * Un campo de texto con la clave de un catálogo del SAT: sugiere mientras se escribe (por clave o por descripción) y, con la
 * clave puesta, dice qué significa. No obliga a elegir de la lista: una clave que no esté en el catálogo se puede escribir y
 * será el validador quien la señale con su código.
 */
@Component({
  selector: 'app-campo-catalogo',
  imports: [FormField, MatAutocomplete, MatAutocompleteTrigger, MatError, MatFormField, MatHint, MatInput, MatLabel, MatOption],
  template: `
    <mat-form-field subscriptSizing="dynamic">
      <mat-label>{{ etiqueta() }}</mat-label>
      <input
        matInput
        type="text"
        autocomplete="off"
        spellcheck="false"
        [formField]="campo()"
        [matAutocomplete]="lista"
        (focus)="activo.set(true)"
      />
      <mat-autocomplete #lista="matAutocomplete" (optionSelected)="alElegir($event)">
        @for (fila of opciones(); track fila['clave']) {
          <mat-option [value]="fila['clave']">
            <span class="clave">{{ fila['clave'] }}</span>{{ ' ' }}<span class="texto">{{ descripcionDe(fila) }}</span>
          </mat-option>
        }
      </mat-autocomplete>
      @if (nota() || ayuda()) {
        <mat-hint>{{ nota() || ayuda() }}</mat-hint>
      }
      <mat-error>{{ primerError() }}</mat-error>
    </mat-form-field>
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }

    mat-form-field {
      width: 100%;
    }

    .clave {
      display: inline-block;
      min-width: 5.5em;
      font-family: ui-monospace, 'Cascadia Mono', Consolas, monospace;
      font-weight: 500;
    }

    .texto {
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class CampoCatalogo {
  private readonly catalogos = inject(ServicioCatalogos);

  /** El campo del formulario que guarda la clave. */
  readonly campo = input.required<FieldTree<string>>();
  /** Nombre del catálogo en la API: <c>uso-cfdi</c>, <c>clave-unidad</c>... */
  readonly catalogo = input.required<string>();
  readonly etiqueta = input.required<string>();
  /** Texto de ayuda mientras no hay una descripción que mostrar. */
  readonly ayuda = input('');
  /** Cuántos caracteres hacen falta para empezar a sugerir: en catálogos enormes (productos y servicios) no sirve listar al azar. */
  readonly minimo = input(0);
  /** Cuántas sugerencias se piden: en los catálogos chicos, todas. */
  readonly tamano = input(10);

  /** Se emite con la fila completa cuando se elige una sugerencia (para completar otros campos, como la unidad). */
  readonly seleccionada = output<FilaCatalogo>();

  protected readonly descripcionDe = descripcionDe;

  /** Hasta el primer foco no se pregunta nada a la API: una pantalla llena de campos no hace decenas de consultas al abrirse. */
  protected readonly activo = signal(false);

  private readonly textoRetrasado = retrasada(computed(() => this.campo()().value()), 250);
  private readonly consultaActiva = computed(() => this.activo() && this.textoRetrasado().trim().length >= this.minimo());

  private readonly sugerencias = resource({
    params: () =>
      this.consultaActiva()
        ? { catalogo: this.catalogo(), texto: this.textoRetrasado(), tamano: this.tamano() }
        : undefined,
    loader: ({ params }) => this.catalogos.buscar(params.catalogo, { texto: params.texto, tamano: params.tamano }),
  });

  /** Las últimas sugerencias que llegaron: la lista no parpadea mientras llegan las nuevas. */
  private readonly ultimas = linkedSignal<FilaCatalogo[] | undefined, FilaCatalogo[]>({
    source: () => this.sugerencias.value()?.filas,
    computation: (nuevas, anterior) => nuevas ?? anterior?.value ?? [],
  });

  protected readonly opciones = computed(() => (this.consultaActiva() ? this.ultimas() : []));

  private readonly valorEstable = retrasada(computed(() => this.campo()().value().trim()), 400);

  private readonly significado = resource({
    params: () => {
      const clave = this.valorEstable();
      return clave ? { catalogo: this.catalogo(), clave, tamano: this.tamano() } : undefined;
    },
    loader: ({ params }) => this.catalogos.describir(params.catalogo, params.clave, params.tamano),
  });

  /** Qué significa la clave que hay en el campo; si no es de este catálogo, aviso (sin bloquear) cuando se sale del campo. */
  protected readonly nota = computed(() => {
    const clave = this.campo()().value().trim();
    if (!clave || clave !== this.valorEstable() || this.significado.status() !== 'resolved') {
      return '';
    }

    const fila = this.significado.value();
    if (fila) {
      return descripcionDe(fila);
    }

    return this.campo()().touched() ? `La clave ${clave} no aparece en este catálogo.` : '';
  });

  protected readonly primerError = computed(() => this.campo()().errors()[0]?.message ?? '');

  protected alElegir(evento: MatAutocompleteSelectedEvent): void {
    const clave = String(evento.option.value);
    const fila = this.opciones().find((candidata) => String(candidata['clave']) === clave);
    if (fila) {
      this.seleccionada.emit(fila);
    }
  }
}
