# Mapa de Mundos

Herramienta de worldbuilding para novelas de fantasía y pieza de portfolio. El autor es estudiante de desarrollo (C#, SQL, algo de Python, web básica), busca su primer trabajo como junior y quiere entender cada parte del código.

## Comandos

- `dotnet build`: compilar (verificar después de cada cambio).
- `dotnet run`: ejecutar en http://localhost:5000 (no hay launchSettings.json, es el puerto por defecto).
- SDK instalado: 10.0.400. Destino: net10.0, paquetes 10.0.*.
- Vista previa de Claude: configuración `pruebas` en `.claude/launch.json`, puerto 5080. Es otro origen, así que su IndexedDB no pisa los datos reales del puerto 5000. Si el panel está oculto no dibuja cuadros: no sirven capturas ni ResizeObserver, hay que probar con scripts o pedirle al autor que mire.
- Detener el servidor antes de `dotnet build`. Si se compila con el servidor corriendo, la app puede quedar en "Cargando..." con errores de `integrity` en la consola: se arregla con `dotnet clean`, `dotnet build` y recarga forzada (Ctrl+F5).

## Estructura

- `Models/`: `MapProject`, `MapLayer`, `MapPin` (MapProject.cs) y `ProjectFile`, `PickedFile` (ProjectFile.cs).
- `Services/ProjectService.cs`: estado del proyecto abierto, guardado con debounce de 400 ms, import/export.
- `Components/MapEditor.razor`: interfaz principal (panel de capas, barra, mapa, ficha del pin).
- `Components/GeneratorPanel.razor`: panel "Generar mapa" (semillas, tierra, detalle). Edita una copia de `GeneratorSettings` y la guarda al generar.
- `Models/GeneratorSettings.cs`: las cuatro semillas, porcentaje de tierra y tamaño de celda. Se guarda en `MapProject.Generator` (null si la imagen vino de un archivo).
- `Generation/`: el generador, en orden de ejecución:
  - `Rng.cs` (Mulberry32 con semilla), `PerlinNoise.cs` (ruido y fBm).
  - `Mesh.cs`: grilla hexagonal con puntos desplazados, vecinas fijas por fila y columna, polígonos con baricentros, BFS `Distances`.
  - `World.cs`: un arreglo por dato y por celda (altura, agua, lago, clima, drenaje, caudal, bioma).
  - `ReliefStage.cs`, `ClimateStage.cs`, `RiverStage.cs`, `BiomeStage.cs`: una etapa por archivo, cada una con su semilla.
  - `SvgMapRenderer.cs`: dibujo en SVG. `MapGenerator.cs`: orquesta todo y devuelve el data URL.
- `wwwroot/js/mapview.js`: zoom, pan, clics y arrastre de pines.
- `wwwroot/js/storage.js`: IndexedDB como clave/valor (`project`, `image`).
- `wwwroot/js/files.js`: selector de archivos y descarga.
- `wwwroot/css/app.css`: estilos.

## Decisiones

- Blazor WebAssembly standalone, sin backend. Todo se guarda en el navegador.
- Pines con coordenadas normalizadas (0 a 1), independientes de la resolución de la imagen.
- Zoom y pan viven en JS. C# solo recibe `OnMapClick` y `OnPinMoved`.
- La imagen se guarda aparte del proyecto (clave `image`) y hoy es un data URL.
- `.world` y `.viewport` no llevan atributo `style` en Razor: los maneja mapview.js.
- Los números que van a CSS se formatean con `InvariantCulture` (en es-AR el decimal es coma).
- Los SVG se muestran con `<img>`, nunca incrustados en el DOM (así no ejecutan scripts).
- Zoom (decidido): tope de zoom calculado según la imagen cargada, y probar el SVG de Azgaar como base. Tiles descartados por ahora.
- Generador (decidido): malla de celdas irregulares, salida vectorial, relieve por ruido, una semilla por etapa. Dibujo a mano de la silueta como agregado posterior. Generador de números propio en vez de `System.Random` para que una semilla dé siempre el mismo mapa.
- Generador (implementado así):
  - Malla hexagonal desplazada (máximo 0.25 de la separación) en vez de Delaunay: vecinas conocidas sin algoritmo de triangulación. Polígono de celda = baricentros de sus 6 triángulos. Con desplazamiento mayor a 0.3 los triángulos se pueden dar vuelta.
  - El mapa generado es un SVG de 2000 x 1250 que se usa como imagen base (data URL), así reutiliza zoom, guardado y exportación.
  - Nivel del mar por cuantil: el porcentaje de tierra se cumple siempre. Caída extra con potencia 8 cerca de los bordes para que la tierra no quede cortada.
  - Ríos con "priority flood": cada celda drena hacia la celda desde la que la alcanzó la inundación; caudal acumulado en orden inverso. Umbral de río proporcional a la tierra (`CatchmentFraction`).
  - Dibujo: costa y orillas encadenadas en contornos y suavizadas con Chaikin (2 pasadas); la tierra se recorta con ese contorno (`clipPath`, `evenodd` para los lagos) y se desenfoca (`feGaussianBlur`) para ocultar los hexágonos. El mar costero son trazos anchos de la costa. Los afluentes terminan en el punto medio de un tramo del río principal porque Chaikin conserva esos puntos.
  - Colores del mapa fijos (no dependen del tema), apagados para que resalten pines y etiquetas.
  - Mismas semillas, mismo mapa: cualquier cambio en las constantes de las etapas cambia los mapas ya generados (los mundos guardados conservan su imagen, pero "Generar" daría otro resultado).

## Estado actual (2026-10-19)

Compila sin advertencias y funciona (pines, arrastre, zoom, guardado probados).

Resuelto:

- csproj: el autor quitó el comentario XML con `--` (error MSB4025) y pasó a net10.0 y 10.0.*.
- `mapview.js`: `doFit` ya no deja la escala en 0 si el visor o la imagen no tienen tamaño; queda `pendingFit` y lo retoma un `ResizeObserver` o el `load` de la imagen. Verificado que no queda en 0; falta que el autor confirme en su navegador que el ajuste pendiente se ejecuta.

- Tope de zoom en `doFit`: 2 para PNG/JPG, 16 para SVG, nunca menor que la escala de ajuste. Probado con ambos formatos. El autor confirmó que su SVG de Azgaar se ve perfecto: SVG es el formato recomendado para la imagen base.
- Accesibilidad sin CSS: la fila de capa se activa con `onfocusin` y lleva `aria-current`; los controles tienen `aria-label`; los pines tienen `role="button"`, `tabindex`, `aria-pressed` y se seleccionan con Enter o Espacio.
- README al día.
- 2026-10-13: el autor creó el repositorio de GitHub `didactic-maps` y subió el proyecto (commit 787fca7, "Cargar proyecto"). La carpeta anterior `Documents\MapaMundos` ya no se usa.
- Dirección visual A "Carta impresa" aplicada en `app.css` (elegida por el autor): tokens en `:root`, tema claro y oscuro por `prefers-color-scheme`, Alegreya (títulos, etiquetas del mapa) y Alegreya Sans (controles) desde Google Fonts, foco visible, estados hover y activo, paneles apilados bajo 900 px. Revisado en vista previa en claro, oscuro y 700 px.

- 2026-10-19: generador de mapas al azar (pedido por el autor antes que el resto de la hoja de ruta). Probado: diez semillas sin tierra en el borde, 40 % de tierra exacto, 16 a 34 desembocaduras, biomas variados según latitud. En el navegador (Debug) tarda unos 3 s con detalle medio y pesa unos 600 KB.

Pendiente:

- Generador: mientras genera, la interfaz se congela unos segundos (C# en WebAssembly corre en el mismo hilo). Opciones: compilación AOT o generar por partes cediendo el control.
- Generador: sombra de lluvia, nombres de lugares, símbolos de montaña. Ejercicio del autor: bioma "Pantano" en `BiomeStage.Classify`.
- Íconos SVG propios por tipo de pin (hoy todos son un círculo).
- Botón para elegir tema a mano (hoy sigue al sistema). Tipografías locales si se quiere usar sin conexión.
- Imagen como data URL: pesada en memoria y en interop para mapas grandes. Pasarla a Blob mueve parte del import/export a JS; esperar a que el autor diga si su SVG real tarda en cargar.
- El mapa no se reajusta solo al cambiar el tamaño de la ventana (hay que usar "Ajustar a pantalla").
- No existe la carpeta `/ejemplos` con el archivo de Azgaar (lo aporta el autor).
- Ejercicio del autor: mover el pin seleccionado con las flechas del teclado en `OnPinKeyDown`.

Paleta A (contrastes AA verificados; al agregar un color nuevo, calcular su ratio antes de usarlo):

- Claro: fondo #F4EFE3, panel #E9E2D0, mesa #DDD5C0, hover #DDD4BE, texto #23201A, tenue #5C5546, borde #857B66, acento #9C2F1F.
- Oscuro: fondo #1A1815, panel #25221D, mesa #121110, hover #353028, texto #ECE6D8, tenue #B0A795, borde #857C6A, acento #E58B74.
- Pines y etiquetas usan tinta #23201A y papel #F4EFE3 fijos, porque van sobre el mapa y no sobre la interfaz.

## Hoja de ruta (en orden)

0. Corregir los problemas de la revisión inicial. Falta: íconos de pin y decidir lo del Blob.
1. Importador de Azgaar (CSV/GeoJSON de ciudades a pines). Falta el archivo de ejemplo en `/ejemplos`.
2. Vincular pines con fichas de un wiki del mundo.
3. Generador de mapas por etapas (continente, clima, ríos, biomas), cada etapa con su semilla. Primera versión hecha el 2026-10-19; quedan mejoras (ver Pendiente).

## Forma de trabajo

- Antes de cambiar código, explicar el concepto y el plan en pocas líneas.
- Cambios chicos, uno por vez, verificando con `dotnet build`.
- Interfaz y comentarios en español, sin emojis y sin guiones largos.
- Siempre que se pueda, dejar una parte para que la haga el autor con una consigna, y después corregirla.
- Al terminar cada paso, actualizar este archivo y recordarle al autor que lo incluya en el commit.

## Git y entrega

El proyecto vive en el repositorio de GitHub del autor: `FrancisBujonok/didactic-maps`, carpeta local `C:\Users\franc\Documents\GitHub\didactic-maps`. Los commits y los push los hace el autor después de revisar todo. Claude no versiona nada.

- Prohibido ejecutar comandos de Git que modifiquen el repositorio o su historial (`add`, `commit`, `push`, `pull`, `merge`, `rebase`, `reset`, `stash`, `config`, `init`) y cualquier comando `gh`. Permitidos, solo para leer: `git status`, `git diff`, `git log`.
- Ninguna firma ni atribución de Claude en archivos ni mensajes: nada de "Co-Authored-By", "Generated with Claude Code" ni similares.
- Si hay que borrar o renombrar archivos, avisar antes.
- Al terminar cada tarea, entregar en este orden:
  1. Lista de archivos creados, modificados y borrados, con una línea por archivo que diga qué cambió y por qué.
  2. Solo si el autor lo pide ("pasame el archivo"): el contenido completo del archivo, sin fragmentos ni "el resto igual".
  3. Mensaje de commit sugerido en español: primera línea de hasta 72 caracteres, más un cuerpo corto si hace falta. Si la tarea mezcla cambios sin relación, proponer varios commits y qué archivos van en cada uno.
  4. Los comandos exactos `git add` (archivo por archivo) y `git commit -m` para copiar y ejecutar.
  5. Una advertencia si algún archivo no debería subirse (datos personales, mundos del autor, archivos generados, claves) y qué línea agregar al `.gitignore`.

## Reglas de diseño

La interfaz es una herramienta de trabajo, no una landing page. Inspiración: cartografía y mesa de trabajo de un autor (papel, tinta, dibujo).

- Antes de tocar el CSS: proponer dos direcciones visuales (paleta en hex, tipografías, motivo, ratios de contraste) y esperar la elección.
- Tokens en variables CSS: paleta con un acento y neutros, escala de espaciado (múltiplos de 4 u 8 px), escala tipográfica. Sin valores sueltos.
- Dos familias tipográficas como máximo, justificadas. No usar Inter, Space Grotesk ni Instrument Serif.
- Contraste WCAG AA verificado con números (4.5:1 texto, 3:1 controles y bordes) en tema claro y oscuro. Foco visible con teclado.
- Hover, foco y activo con cambios claros (color, borde, fondo), transiciones de 150 ms o menos, nada decorativo.
- Íconos de tipos de pin: SVG propios, mismo trazo y tamaño, pensados para un mapa.
- Textos en español, cortos, que digan qué hace cada cosa.
- Prohibido: degradado violeta a azul, texto con degradado, glassmorphism, grano sobre degradado, luz que sigue al cursor, tarjetas con borde izquierdo de color, tres cajas con íconos en fila, insignia sobre el título, fade al hacer scroll, emojis, cursivas serif para resaltar, íconos de Lucide, componentes de librería sin personalizar, modo oscuro de bajo contraste, botones que solo se desvanecen en hover, palabras de moda, guiones largos.
- Al terminar un cambio visual: abrir la vista previa, revisar contra esta lista y corregir antes de avisar.
