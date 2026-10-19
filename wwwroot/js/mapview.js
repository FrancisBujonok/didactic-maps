// mapview.js - zoom, arrastre y clics sobre el mapa.
//
// Lo que ocurre todo el tiempo (mover el mouse, usar la rueda) se resuelve acá, en JavaScript.
// A C# solo se le avisa de lo importante: un clic en el mapa, un pin que se movió o las
// celdas que pinta el pincel de selección (Shift + arrastrar en "Ver celdas").
//
// Idea clave: el mapa (.world) tiene el tamaño real de la imagen y se mueve/escala con una
// transformación CSS: translate(x, y) scale(escala). Los pines están DENTRO de .world, así que
// se mueven con él y no hace falta recalcular su posición.

let state = null;

const clamp = (value, min, max) => Math.min(max, Math.max(min, value));

export function init(viewport, world, dotnet) {
  dispose();

  const s = {
    viewport,
    world,
    x: 0,
    y: 0,
    scale: 1,
    minScale: 0.02,
    maxScale: 16,
    pendingFit: false,
    fitScale: 1, // escala con la que el mapa entra entero en el visor (la calcula doFit)
    cleanup: [],
  };
  state = s;

  // Registra un listener y recuerda cómo quitarlo (lo usa dispose).
  const listen = (target, type, handler, options) => {
    target.addEventListener(type, handler, options);
    s.cleanup.push(() => target.removeEventListener(type, handler, options));
  };

  const apply = () => {
    world.style.transform = `translate(${s.x}px, ${s.y}px) scale(${s.scale})`;
    // Los pines usan esta variable para escalarse al revés y verse siempre del mismo tamaño.
    viewport.style.setProperty('--inv-scale', String(1 / s.scale));
    // Generalización, como en los atlas: con el mapa alejado se ocultan los nombres de los
    // lugares chicos (ver .viewport.far en app.css). Aparecen al acercarse al doble del ajuste.
    viewport.classList.toggle('far', s.scale < s.fitScale * 2);
  };

  // Centra el mapa entero dentro del visor.
  s.doFit = () => {
    const w = world.offsetWidth;
    const h = world.offsetHeight;

    // Sin imagen, o con el visor todavía sin tamaño, la cuenta daría 0 y el zoom quedaría
    // atascado (todo zoom posterior multiplica por la escala actual). Queda pendiente.
    if (!w || !h || !viewport.clientWidth || !viewport.clientHeight) {
      s.pendingFit = true;
      return;
    }
    s.pendingFit = false;

    const scale = Math.min(viewport.clientWidth / w, viewport.clientHeight / h) * 0.96;
    s.scale = scale;
    s.minScale = scale * 0.25;
    s.fitScale = scale;

    // Tope de zoom. La escala es "píxeles de pantalla por cada píxel de imagen": un PNG o JPG
    // por encima de 2 se ve borroso; un SVG se redibuja en cada zoom y aguanta mucho más.
    // Nunca puede ser menor que la escala de ajuste (pasa con imágenes chicas en visores grandes).
    const img = world.querySelector('img');
    const isVector = !!img && img.src.startsWith('data:image/svg+xml');
    s.maxScale = Math.max(isVector ? 16 : 2, scale);
    s.x = (viewport.clientWidth - w * scale) / 2;
    s.y = (viewport.clientHeight - h * scale) / 2;
    apply();
  };

  // Posición en pantalla -> posición normalizada del mapa (0 a 1 en cada eje).
  const toMap = (clientX, clientY) => {
    const rect = viewport.getBoundingClientRect();
    return {
      x: (clientX - rect.left - s.x) / s.scale / world.offsetWidth,
      y: (clientY - rect.top - s.y) / s.scale / world.offsetHeight,
    };
  };

  // Si pedimos ajustar mientras la imagen todavía carga, se ajusta cuando termine de cargar.
  // ('load' no burbujea, por eso se escucha en la fase de captura.)
  listen(world, 'load', () => {
    if (s.pendingFit) {
      s.pendingFit = false;
      s.doFit();
    }
  }, true);

  // Lo mismo si el visor no tenía tamaño: ResizeObserver avisa cuando lo consigue o cambia.
  const observer = new ResizeObserver(() => {
    if (s.pendingFit) s.doFit();
  });
  observer.observe(viewport);
  s.cleanup.push(() => observer.disconnect());

  // --- Zoom con la rueda, manteniendo fijo el punto que está bajo el cursor ---
  listen(viewport, 'wheel', (e) => {
    e.preventDefault();
    const rect = viewport.getBoundingClientRect();
    const px = e.clientX - rect.left;
    const py = e.clientY - rect.top;

    const delta = e.deltaMode === 1 ? e.deltaY * 16 : e.deltaY;
    const newScale = clamp(s.scale * Math.exp(-delta * 0.0015), s.minScale, s.maxScale);
    const ratio = newScale / s.scale;

    s.x = px - (px - s.x) * ratio;
    s.y = py - (py - s.y) * ratio;
    s.scale = newScale;
    apply();
  }, { passive: false });

  // --- Arrastre: mover el mapa, mover un pin, o clic simple ---
  let drag = null;

  listen(viewport, 'pointerdown', (e) => {
    if (e.button !== 0 && e.button !== 1) return; // izquierdo o rueda
    const pinEl = e.button === 0 ? e.target.closest('.pin') : null;

    // Shift + arrastrar en el modo "Ver celdas" pinta la selección en vez de mover el mapa.
    // El modo se sabe por la clase que Blazor le pone al visor.
    const painting = e.button === 0 && !pinEl && e.shiftKey && viewport.classList.contains('inspecting');
    if (painting) {
      const p = toMap(e.clientX, e.clientY);
      dotnet.invokeMethodAsync('OnCellPaint', clamp(p.x, 0, 1), clamp(p.y, 0, 1), true);
    }

    drag = {
      kind: pinEl ? 'pin' : painting ? 'paint' : 'pan',
      button: e.button,
      pinEl,
      startX: e.clientX,
      startY: e.clientY,
      originX: s.x,
      originY: s.y,
      moved: false,
      pos: null,
    };
    if (e.button === 1) e.preventDefault();
  });

  listen(window, 'pointermove', (e) => {
    if (!drag) return;

    const dx = e.clientX - drag.startX;
    const dy = e.clientY - drag.startY;
    if (!drag.moved && Math.hypot(dx, dy) < 4) return; // todavía es un clic, no un arrastre
    drag.moved = true;

    if (drag.kind === 'pan') {
      s.x = drag.originX + dx;
      s.y = drag.originY + dy;
      apply();
    } else if (drag.kind === 'paint') {
      const p = toMap(e.clientX, e.clientY);
      dotnet.invokeMethodAsync('OnCellPaint', clamp(p.x, 0, 1), clamp(p.y, 0, 1), false);
    } else {
      const p = toMap(e.clientX, e.clientY);
      drag.pos = { x: clamp(p.x, 0, 1), y: clamp(p.y, 0, 1) };
      drag.pinEl.style.left = `${drag.pos.x * 100}%`;
      drag.pinEl.style.top = `${drag.pos.y * 100}%`;
    }
  });

  listen(window, 'pointerup', (e) => {
    if (!drag) return;
    const d = drag;
    drag = null;

    if (d.kind === 'pin') {
      if (d.moved && d.pos) {
        dotnet.invokeMethodAsync('OnPinMoved', d.pinEl.dataset.pinId, d.pos.x, d.pos.y);
      }
      return; // el clic simple sobre un pin lo maneja Blazor (selecciona el pin)
    }

    if (d.kind === 'paint') return; // la celda del clic ya se pintó al apretar

    if (!d.moved && d.button === 0) {
      const p = toMap(e.clientX, e.clientY);
      const inside = p.x >= 0 && p.x <= 1 && p.y >= 0 && p.y <= 1;
      if (inside) dotnet.invokeMethodAsync('OnMapClick', p.x, p.y);
    }
  });

  listen(window, 'pointercancel', () => {
    drag = null;
  });
}

// Ajusta el mapa al tamaño del visor (o lo deja pendiente hasta que cargue la imagen).
export function fit() {
  if (!state) return;
  const img = state.world.querySelector('img');
  if (img && img.complete && img.naturalWidth > 0) {
    state.pendingFit = false;
    state.doFit();
  } else {
    state.pendingFit = true;
  }
}

export function dispose() {
  if (!state) return;
  state.cleanup.forEach((undo) => undo());
  state = null;
}
