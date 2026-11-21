# Comprobante: interfaz web

Angular 22 con Angular Material (Material 3, claro u oscuro según el sistema), sin zone.js y con Vitest.

- `npm start` sirve la app en <http://localhost:4200> y manda `/api` a la API en <http://localhost:5081>
  (`proxy.conf.json`). Antes, en la raíz del repositorio: `docker compose up -d --wait db api`.
- `npm test` corre las pruebas con Vitest y `npm run build` compila a `dist/comprobante-web`.
- En Docker, nginx (`nginx.conf`) sirve el build y pasa `/api` a la API: la app y la API comparten origen.
