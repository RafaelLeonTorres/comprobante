/**
 * Pasa el foco a un título y deja arriba de la ventana el bloque donde está (el aviso, el resultado).
 *
 * Con solo `focus()` el navegador desplaza lo mínimo para que el título se vea, y un resultado largo queda con su título
 * pegado al borde de abajo: quien llega al resultado tendría que bajar para leerlo. Se lleva arriba el bloque completo
 * y no solo el título, porque el título está dentro con su relleno y el borde del bloque quedaría fuera de la ventana.
 */
export function enfocarArriba(titulo: HTMLElement | null | undefined): void {
  if (!titulo) {
    return;
  }
  titulo.focus({ preventScroll: true });
  (titulo.closest('section') ?? titulo).scrollIntoView({ block: 'start' });
}
