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
- `Components/CellInfo.razor`: ficha de solo lectura de una celda (terreno, altura, temperatura, humedad, río).
- `Components/CellPanel.razor`: panel de las celdas seleccionadas (una o varias): resumen y herramientas (terreno, subir, bajar, hacer tierra o agua, restaurar).
- `Models/Biome.cs`: enum `Biome` y `BiomeNames` (nombres en español, lista `Land` de biomas editables). Está en Models porque el proyecto guarda biomas elegidos a mano.
- `Models/CellEdit.cs`: un cambio a mano sobre una celda (`Cell`, `ElevationDelta`, `Biome?`). Se guardan en `MapProject.CellEdits`.
- `Models/Region.cs`: reino con `Id`, `Name`, `Color`, `Cells` (lista de índices), `CapitalPinId` y paleta. Se guardan en `MapProject.Regions`.
- `Models/MapProject.cs` también tiene `MapPin.KindGroups` (tipos de pin agrupados; `Kinds` es la lista plana) y `MapLayer.ContrastingInk` (tinta o papel según contraste WCAG con un color de fondo).
- `Components/PinIcon.razor`: íconos de los tipos de pin y de la capital (estrella), como trazos de path en un diccionario.
- `Components/RegionPanel.razor`: sección "Reinos" del panel izquierdo (crear, renombrar, color, borrar). Las celdas se asignan desde `CellPanel` ("Reino" + "Asignar").
- `Models/GeneratorSettings.cs`: las cuatro semillas, porcentaje de tierra, tamaño de celda y estilo (`MapStyle`: Pintado o Celdas). Se guarda en `MapProject.Generator` (null si la imagen vino de un archivo).
- `Generation/`: el generador, en orden de ejecución:
  - `Rng.cs` (Mulberry32 con semilla), `PerlinNoise.cs` (ruido y fBm).
  - `Mesh.cs`: grilla hexagonal con puntos desplazados, vecinas fijas por fila y columna, polígonos con baricentros, BFS `Distances`, `FindCell` (centro más cercano y prueba del rayo).
  - `World.cs`: un arreglo por dato y por celda (altura, agua, lago, clima, drenaje, caudal, bioma), `RiverSize` y conversión a metros y grados.
  - `ReliefStage.cs`, `ClimateStage.cs`, `RiverStage.cs`, `BiomeStage.cs`: una etapa por archivo, cada una con su semilla. `ReliefStage.FindWater` (mar, lagos, distancia a la costa) es público para recalcularlo tras editar alturas.
  - `EditStage.cs`: aplica las ediciones a mano (alturas después del continente, biomas al final).
  - `SvgMapRenderer.cs`: dibujo en SVG (sus ayudas de costa y color son `internal` para reutilizarlas). `MapGenerator.cs`: orquesta todo, `Draw` y `ToDataUrl`.
  - `ThematicRenderer.cs`: vistas (`MapView`: Terreno, Reinos, Altura, Temperatura, Humedad), leyendas (`ColorScale`), dueño de cada celda (`Owners`) y ubicación de nombres de reino (`Labels`).
- `wwwroot/js/mapview.js`: zoom, pan, clics y arrastre de pines.
- `wwwroot/js/storage.js`: IndexedDB como clave/valor (`project`, `image`).
- `wwwroot/js/files.js`: selector de archivos y descarga.
- `wwwroot/css/app.css`: estilos.

## Decisiones

- Blazor WebAssembly standalone, sin backend. Todo se guarda en el navegador.
- Pines con coordenadas normalizadas (0 a 1), independientes de la resolución de la imagen.
- Zoom y pan viven en JS. C# solo recibe `OnMapClick`, `OnPinMoved` y `OnCellPaint` (pincel de selección: Shift + arrastrar con el visor en clase `inspecting`).
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
  - Estilo "Celdas": sin suavizado de costas, sin desenfoque, sin celdas de agua pintadas, y cada celda con un borde 18 % más oscuro que su relleno. Mismo mundo que "Pintado"; solo cambia el dibujo.
  - El mundo (`World`) vive en memoria en `ProjectService` y no se guarda. `GetWorld()` lo rearma desde `Project.Generator` si falta (tarda menos de 1 s); se descarta al cargar imagen, importar o empezar proyecto nuevo.
  - Modo "Ver celdas" (`Mode.Inspect`): el clic normalizado se pasa a píxeles del mapa (2000 x 1250) y `FindCell` devuelve la celda. El contorno se dibuja con un SVG en línea dentro de `.world` (contenido propio, no del usuario) con grosor dividido por `--inv-scale`.
  - Unidades mostradas: altura 0 a 1 = 0 a 4500 m, profundidad hasta 5000 m, temperatura 0 a 1 = -15 a 35 °C (elegida para que los límites de biomas caigan cerca de los reales).
  - Ediciones a mano: se guardan como diferencias por celda (`CellEdit`), no el mundo entero. Orden en `MapGenerator.Build`: continente, alturas editadas (y `FindWater` si cambió alguna), clima, ríos, biomas, biomas editados. Editar un bioma solo redibuja; editar alturas rearma el mundo (cambian costa, clima y ríos). Cada edición tarda 2 a 3 s en el navegador; lo más pesado es redibujar el SVG.
  - Las ediciones se conservan al generar salvo que cambie la malla (`MapGenerator.ChangesMesh`: semilla de continente o detalle); entonces se avisa y se borran. Cargar una imagen de archivo también las borra.
  - Selección: `HashSet<int>` en `MapEditor`. Clic elige una; Shift + clic agrega o quita; Shift + arrastrar pinta. Si el pincel empieza sobre una celda elegida, borra. El pincel recorre el tramo entre avisos del mouse en pasos de media celda y usa `FindCell` con `hint` (caminata desde la última celda).
  - Reinos: cada celda en un reino como máximo (si se repite, gana el primero de la lista). Solo se asignan celdas de tierra. Igual que las ediciones, se vacían (sin borrar nombre ni color) si cambia la malla o se carga una imagen de archivo; `ClearCellData` hace las dos cosas.
  - Vistas: una a la vez, elegida en la barra ("Vista"). Son otro SVG del mismo tamaño en un `<img class="view-layer">` encima del terreno. Las de datos tapan todo (celdas por escalones de color, 12 escalones, costa sin suavizar); la de reinos es semitransparente (opacidad en el grupo, no por celda, para que no se marque la grilla) y recortada con la costa. Fronteras sobre los lados de las celdas, en tinta.
  - Las vistas se dibujan al pedirlas y se guardan en memoria (`ProjectService._viewCache`); `SetWorld` vacía el caché y `RegionsChanged` descarta solo la de reinos. La primera vez tarda unos 3,5 s en el navegador ("Preparando la vista..."). Si se elige otra vista mientras una se prepara, gana la última elegida (`_requestedView`).
  - Nombres de reino: texto HTML como las etiquetas de pines (tipografía de la app, escala inversa), en la celda del reino más cercana al promedio de sus celdas. Los SVG en `<img>` no pueden usar las fuentes web de la página, por eso no van dentro del SVG.
  - Los paneles hijos con datos que cambian desde otro panel (`GeneratorPanel`, `RegionPanel`) se suscriben a `ProjectService.StatusChanged` para redibujarse; si no, Blazor no los vuelve a dibujar.
  - Capitales: el reino guarda `CapitalPinId`; el reino de un pin no se guarda, se calcula por la celda bajo su posición (`ProjectService.RegionAt`), así sigue al pin si se lo mueve. Un reino tiene una capital como máximo y un pin es capital de uno como máximo. Si la capital queda fuera de su reino, la ficha avisa y no se borra el vínculo; si se borra el pin, el reino queda sin capital. `SelectPin` rearma el mundo si hace falta, para no hacerlo en medio del dibujo de la ficha.
  - Colores del mapa fijos (no dependen del tema), apagados para que resalten pines y etiquetas.

- Pines e íconos:
  - 15 tipos en 5 grupos (`optgroup` en el selector). Agregar un tipo = sumarlo a `KindGroups` y dibujarle el ícono en `PinIcon`; un tipo desconocido usa el ícono de "Punto de interés".
  - Íconos: grilla de 24 x 24 con 3 de margen, trazo 2, puntas y uniones redondeadas, sin relleno, `currentColor`. Se dibujan a 16 px dentro de una insignia de 28 px (`--pin`). Con detalles chicos (más de dos almenas, columnas sueltas) se empastan o se confunden: revisarlos agrandados (hoja temporal a 72 px) antes de dar uno por bueno.
  - El ícono es tinta o papel según el color de la capa (`--pin-ink`, calculado con `ContrastingInk`). Con estos dos colores, ningún fondo baja de 3,77:1 (mínimo WCAG para gráficos: 3:1). Paleta de capas verificada: el peor caso es #2980b9 con 3,78:1.
  - Capital: estrella en lugar del ícono del tipo y un segundo anillo de tinta (`outline`). La leyenda de la vista de reinos dice "capital: X".
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

- 2026-10-19: estilo "Celdas" y modo "Ver celdas" con ficha de celda (pedido por el autor). Probado en vista previa: ficha correcta en tierra y mar, contorno visible, rearmado del mundo tras recargar en 0.76 s.

- 2026-10-19: edición a mano de celdas con selección múltiple (pedido por el autor). Probado: el pincel agrega y borra sin mover el mapa; terreno, subir y hacer agua cambian la imagen; las 24 ediciones se guardan, sobreviven a recargar y "Restaurar" vuelve la celda a lo generado. Prueba fuera de la app: hacer tierra un bloque de mar recalcula la costa y rearmar con las ediciones da el mismo mundo.

- 2026-10-19: reinos y vistas del mapa (pedido por el autor). Probado: crear dos reinos, pintar y asignar celdas, cantidades al día tras recargar, vista de reinos con nombres y leyenda, las tres vistas de datos con su leyenda, regenerar con las mismas semillas en otro estilo conserva los reinos sin preguntar, y la carrera entre vistas resuelta. Una carga lenta (30 s) durante la prueba resultó ser el servidor compilado en caliente, no el código: con el servidor reiniciado la app abre en 1,9 s.

- 2026-10-19: capitales vinculadas a pines y 15 tipos de pin con íconos SVG propios (pedido por el autor). Probado: el reino del pin se detecta por su posición, la casilla de capital funciona y sobrevive a recargar, al sacar la capital de su reino aparece el aviso, al borrar el pin el reino queda sin capital, la leyenda muestra la capital, ficha en claro y oscuro. Íconos revisados a 72 px: se rediseñaron Ruina (parecía un gráfico de barras), Cueva (parecía un arcoíris) y Torre (almenas empastadas).

Pendiente:

- Próximo paso posible: deshacer la última edición; fronteras de reino suavizadas en el estilo Pintado; mostrar en la lista de reinos su capital y cantidad de pines.
- Íconos sin tipo propio todavía: "Facción" y "Evento" son abstractos (estandarte y reloj de arena); revisar con el autor si le sirven.
- Ejercicios del autor: "Distancia a la costa" en `CellInfo.razor`; botón "Seleccionar todo este terreno" en `CellPanel.razor` (necesita un `EventCallback` nuevo hacia `MapEditor`); vista "Ríos" o "Distancia a la costa" en `ThematicRenderer` (enum, nombre en `Views`, color en `RenderData`, leyenda en `Legend`); tipo de pin nuevo con ícono propio (por ejemplo "Faro") en `KindGroups` y `PinIcon`.
- Edición: cada cambio redibuja el SVG completo (2 a 3 s). Si molesta, dibujar solo lo que cambió o mostrar la selección mientras se redibuja.
- Generador: mientras genera, la interfaz se congela unos segundos (C# en WebAssembly corre en el mismo hilo). Opciones: compilación AOT o generar por partes cediendo el control.
- Generador: sombra de lluvia, nombres de lugares, símbolos de montaña. Ejercicio del autor: bioma "Pantano" en `BiomeStage.Classify`.
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

0. Corregir los problemas de la revisión inicial. Falta: decidir lo del Blob (los íconos de pin se hicieron el 2026-10-19).
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
