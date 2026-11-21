import { Signal, computed, effect, linkedSignal, signal, untracked } from '@angular/core';

/**
 * Una señal que sigue a otra con retraso: solo cambia cuando la fuente lleva <c>milisegundos</c> sin cambiar. Sirve para no
 * llamar a la API en cada tecla. Se crea en un contexto de inyección (el constructor o un campo de un componente), incluso
 * antes de que lleguen los inputs de los que dependa la fuente: arranca con el valor que tenga cuando corre por primera vez.
 */
export function retrasada<T>(fuente: Signal<T>, milisegundos: number): Signal<T> {
  const alcanzado = signal<{ valor: T } | undefined>(undefined);
  let primera = true;
  effect((alLimpiar) => {
    const valor = fuente();
    if (primera) {
      // La primera vez no hay nada que esperar: es el valor con el que arranca.
      primera = false;
      alcanzado.set({ valor });
      return;
    }

    const temporizador = setTimeout(() => alcanzado.set({ valor }), milisegundos);
    alLimpiar(() => clearTimeout(temporizador));
  });

  // Antes de que corra el efecto por primera vez (en el constructor, por ejemplo) vale lo que valga la fuente.
  return computed(() => (alcanzado() ?? { valor: untracked(fuente) }).valor);
}

/** Una señal que vale lo mismo que la fuente, pero que cuando la fuente se queda sin valor conserva el último que tuvo. */
export function conUltimoValor<T>(fuente: () => T | undefined): Signal<T | undefined> {
  return linkedSignal<T | undefined, T | undefined>({
    source: fuente,
    computation: (nuevo, anterior) => nuevo ?? anterior?.value,
  });
}
