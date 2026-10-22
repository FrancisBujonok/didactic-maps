# Mapa de Mundos

Herramienta de worldbuilding para novelas de fantasía y pieza de portfolio. El autor es estudiante de desarrollo (C#, SQL, algo de Python, web básica), busca su primer trabajo como junior y quiere entender cada parte del código.

## Comandos

- `dotnet build`: compilar (verificar después de cada cambio).
- `dotnet run`: ejecutar en http://localhost:5000 (no hay launchSettings.json, es el puerto por defecto).
- `dotnet test Tests/MapaMundos.Tests`: correr las pruebas (unos 2 s). No hay archivo de solución a propósito: con un `.sln` o `.slnx` en la raíz, `dotnet build` y `dotnet run` sin argumentos fallan por haber dos archivos de proyecto o solución.
- SDK instalado: 10.0.400. Destino: net10.0, paquetes 10.0.*.
- Vista previa de Claude: configuración `pruebas` en `.claude/launch.json`, puerto 5080. Es otro origen, así que su IndexedDB no pisa los datos reales del puerto 5000. Si el panel está oculto no dibuja cuadros: no sirven capturas ni ResizeObserver; se puede mostrar con `show_pane` (preview) o probar con scripts. Para capturas de escritorio, emular 1024 x 680 (a 1440 la imagen sale ilegible). A veces la captura toma un cuadro a mitad de una transición de color: repetirla antes de sacar conclusiones.
- Detener el servidor antes de `dotnet build`. Si se compila con el servidor corriendo, la app puede quedar en "Cargando..." con errores de `integrity` en la consola: se arregla con `dotnet clean`, `dotnet build` y recarga forzada (Ctrl+F5).

## Estructura

- `Models/`: `MapProject`, `MapLayer`, `MapPin` (MapProject.cs) y `ProjectFile`, `PickedFile` (ProjectFile.cs).
- `Services/ProjectService.cs`: estado del proyecto abierto, guardado con debounce de 400 ms, import/export.
- `Components/MapEditor.razor`: interfaz principal: lomo (`.spine`: modos, proyecto, generador, reinos, fichas, capas), mesa (`.desk`: el mapa entero, con la tira de abajo `.map-bar` y la barra de colocación o dibujo `.placebar` flotando) y la hoja flotante de la ficha (`.sheet`, solo cuando hay algo que mostrar).
- `Components/UiIcon.razor`: íconos de la interfaz (modos `move`, `pin`, `cells`, `draw`; `close`, `up`, `down`), mismas reglas que `PinIcon`.
- `Components/GeneratorPanel.razor`: panel "Generar mapa" (semillas, tierra, detalle). Edita una copia de `GeneratorSettings` y la guarda al generar.
- `Components/CellInfo.razor`: ficha de solo lectura de una celda (terreno, altura, temperatura, humedad, río).
- `Components/CellPanel.razor`: panel de las celdas seleccionadas (una o varias): resumen y herramientas (terreno, subir, bajar, hacer tierra o agua, restaurar).
- `Models/Biome.cs`: enum `Biome` y `BiomeNames` (nombres en español, lista `Land` de biomas editables). Está en Models porque el proyecto guarda biomas elegidos a mano.
- `Models/CellEdit.cs`: un cambio a mano sobre una celda (`Cell`, `ElevationDelta`, `Biome?`). Se guardan en `MapProject.CellEdits`.
- `Models/Region.cs`: reino con `Id`, `Name`, `Color`, `Cells` (lista de índices), `CapitalPinId` y paleta. Se guardan en `MapProject.Regions`.
- `Models/MapProject.cs` también tiene `MapPin.KindGroups` (tipos de pin agrupados, con la forma de cada grupo; `Kinds` es la lista plana), `PinShape` (Circle, Square, Plain), `PinSize` (Small, Medium, Large), `MapPin.DefaultSize`, `MapPin.Size`, `MapPin.Generated`, `MapProject.SettlementLayerId` y `MapLayer.ContrastingInk` (tinta o papel según contraste WCAG con un color de fondo).
- `Generation/SettlementStage.cs`: etapa de poblados ("Poblar mapa"): puntaje de cada celda, ciudades y aldeas separadas, capitales y reinos (Dijkstra), fortalezas en fronteras, cuevas, ruinas y bosques sagrados. Devuelve un `SettlementPlan`; `ProjectService.Populate` lo aplica.
- `Generation/NameGenerator.cs`: nombres de lugares y reinos uniendo sílabas, con semilla, sin repetir.
- `Components/PinIcon.razor`: íconos de los tipos de pin y de la capital (estrella), como trazos de path en un diccionario.
- `Models/Sketches.cs`: dibujos (islas: lista de listas planas normalizadas [x0, y0, x1, y1, ...]; zonas: `SketchFeature` con `Kind` Water, Mountains o Biome, `Biome?` y `Points`), `SketchTool` (herramienta de la barra: Land, Water, Mountains, Biome, Eraser), y ayudas: `Clean`, `CleanFeatures`, `Copy`, `CopyFeatures`, `SameAs`, `SameFeatures`, `Simplify` (Ramer-Douglas-Peucker con pila), `Area` (cordón de zapato), `Contains` (prueba del rayo), `DistanceToSegment`, `DistanceToPolyline`, `Length`, `ToPixels`. En curso: `MapProject.SketchDraft` (islas) y `SketchFeatureDraft` (zonas); usados en el mapa actual: `GeneratorSettings.Sketch`, `SketchFeatures` y `SketchFidelity`.
- `Generation/SketchStage.cs`: zonas de bioma dibujadas: su clima típico antes de los ríos (`ApplyClimate`) y su bioma al final (`ApplyBiomes`), antes de las ediciones a mano.
- `Components/TemplatePicker.razor`: grilla de plantillas de relieve con miniaturas (en el panel del generador, "Relieve" + "Cambiar"). Arma las miniaturas de a una, cediendo el control, y las rehace si cambia la semilla del continente.
- `wwwroot/js/preview.js`: `toDataUrl(ancho, alto, rgba)`: pinta en un canvas los píxeles que calcula C# y devuelve la imagen.
- `Components/KindPicker.razor`: selector visual de tipo, con `Value` / `ValueChanged` (sirve con `@bind-Value`). `Compact` = solo íconos en fila (barra de colocación); sin `Compact` = grilla de dos columnas con ícono y nombre (ficha).
- `Components/RegionPanel.razor`: sección "Reinos" del panel izquierdo (crear, renombrar, color, borrar). Las celdas se asignan desde `CellPanel` ("Reino" + "Asignar").
- `Models/WikiEntry.cs`: ficha del mundo (`Title`, `Category`, `Text`, `PinId?`, `RegionId?`, lista `Categories`). Se guardan en `MapProject.Entries`.
- `Wiki/WikiMarkup.cs`: traductor del formato simple (`Parse` en bloques `WikiBlock` con líneas de trozos `WikiInline`, `ParseLine`, `LinksIn`). `Wiki/WikiLinks.cs`: `WikiTarget` (Pin, Region o Entry + Id), `EntryOf`, `TargetOf`, `Exists`, `NameOf`, `Resolve`, `MentionsOf`, `RenameLinks`, `CountLinksTo`.
- `Components/WikiText.razor` (dibuja el texto con enlaces), `EntryText.razor` (leer, editar, "Mencionada en"), `EntrySheet.razor` (ficha de reino o suelta en el panel derecho), `WikiList.razor` (sección "Fichas" del panel izquierdo: búsqueda, "+ Ficha", grupos por categoría).
- `Components/NameInput.razor`: campo de nombre (pin, reino en la lista y en su ficha, ficha suelta) que se usa con `@bind-Value` y, al salir del campo, ofrece cambiar los [[enlaces]] rotos al nombre viejo. Los demás atributos pasan al input (`CaptureUnmatchedValues`).
- `Models/GeneratorSettings.cs`: las cuatro semillas, porcentaje de tierra, tamaño de celda, estilo (`MapStyle`: Pintado o Celdas) y `Retouches` (los dibujos valen como retoques sobre otras plantillas). Se guarda en `MapProject.Generator` (null si la imagen vino de un archivo).
- `Generation/`: el generador, en orden de ejecución:
  - `Rng.cs` (Mulberry32 con semilla), `PerlinNoise.cs` (ruido y fBm).
  - `Mesh.cs`: grilla hexagonal con puntos desplazados, vecinas fijas por fila y columna, polígonos con baricentros, BFS `Distances`, `FindCell` (centro más cercano y prueba del rayo).
  - `World.cs`: un arreglo por dato y por celda (altura, agua, lago, clima, drenaje, caudal, bioma), `RiverSize` y conversión a metros y grados.
  - `ReliefShape.cs`: plantillas de relieve (`ReliefTemplates`: nombre, descripción, tierra sugerida y miniaturas con `Preview`) y la forma de cada una (`ReliefShape.Create` y `Height`), armada con manchas, cordilleras y anillos.
  - `ReliefStage.cs`, `ClimateStage.cs`, `RiverStage.cs`, `BiomeStage.cs`: una etapa por archivo, cada una con su semilla. `ReliefStage.FindWater` (mar, lagos, distancia a la costa) es público para recalcularlo tras editar alturas.
  - `EditStage.cs`: aplica las ediciones a mano (alturas después del continente, biomas al final).
  - `SvgMapRenderer.cs`: dibujo en SVG (sus ayudas de costa y color son `internal` para reutilizarlas). `MapGenerator.cs`: orquesta todo, `Draw` y `ToDataUrl`.
  - `ThematicRenderer.cs`: vistas (`MapView`: Terreno, Reinos, Altura, Temperatura, Humedad), leyendas (`ColorScale`), dueño de cada celda (`Owners`) y ubicación de nombres de reino (`Labels`).
- `wwwroot/js/mapview.js`: zoom, pan, clics y arrastre de pines; `centerOn(x, y)` centra el mapa en un punto normalizado (al abrir la ficha de un lugar o reino).
- `wwwroot/js/storage.js`: IndexedDB como clave/valor (`project`, `image`).
- `wwwroot/js/files.js`: selector de archivos y descarga.
- `wwwroot/css/app.css`: estilos.
- `Tests/MapaMundos.Tests/`: pruebas xUnit, proyecto aparte que referencia a la app (`MapaMundos.csproj` excluye la carpeta `Tests` con `Compile Remove`). `TestWorlds` (parámetros fijos, `Ellipse`, `Fingerprint`), `GeneratorTests`, `SketchesTests`, `RetouchTests`, `GeneratorSettingsTests`, `WikiMarkupTests`, `WikiLinksTests`, `RenameLinksTests`.

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
  - Plantillas de relieve (`GeneratorSettings.Template`): altura = ruido + piezas de la plantilla - caída en los bordes cerrados. 10 plantillas; cada una sugiere su tierra al elegirla (atolón 5 %, mediterráneo 60 %), y el deslizador ahora va de 5 a 70 %. "Continente" conserva la fórmula original con el mismo orden de números al azar: verificado con diferencia 0 contra la fórmula vieja, así los mapas guardados no cambian. Península y Mediterráneo tienen bordes abiertos (la tierra toca el borde). El Mediterráneo lleva un estrecho hasta un borde: sin salida al mar abierto, `FindWater` lo clasificaría como lago. "Continentes" lleva canales de mar entre las masas: sin ellos se tocaban y quedaba una sola.
  - Las miniaturas usan la misma `ReliefShape` en una grilla de 96 x 60, con los colores de la vista "Altura" (`ThematicRenderer.ElevationColor`). Las 10 tardan unos 5,6 s en el navegador (Debug).
  - Plantilla Dibujo (`ReliefTemplate.Sketch`): altura = fuerza × distancia con signo al dibujo (prueba del rayo + distancia al lado más cercano, con cajas por isla para saltear las lejanas) + ruido. Fuerza = 0,02 + 2 × fidelidad² (al 100 % el ruido corre la costa unos 15 px). La fuerza se satura adentro (0,4 de escala y nunca más de 0,6, "la meseta") y afuera baja hasta -3. Nivel del mar fijo en 0 (`ReliefShape.FixedSeaLevel`): la tierra sale del dibujo y el deslizador de tierra no se muestra. El relieve de la tierra se mide desde la meseta (`LandBase`); sin eso, la mitad de la tierra salía montaña al 100 %.
  - Valores de la prueba (estrella dibujada): coincidencia con el dibujo 99,3 % (fidelidad 100 %), 97 % (50 %), 84 % (10 %); montaña entre 9 % y 13 % de la tierra en todas las fidelidades.
  - Dibujo en el editor: modo `Mode.Sketch` (clase `sketching`), que se abre con "Dibujar en el mapa" del panel. Arrastrar con el botón izquierdo traza; el botón del medio mueve y la rueda hace zoom. `mapview.js` junta los puntos (uno cada 3 px de pantalla), dibuja el trazo en vivo en `.sketch-live path.live` y al soltar llama a `OnSketchStroke`. `ProjectService.AddSketchStroke` simplifica (tolerancia 3 px) y descarta islas de menos de 400 px². Sin imagen se muestra una hoja en blanco de 2000 x 1250 (`.sketch-sheet`) y `fit` se ajusta a ella. Al generar se sale del modo dibujo.
  - El dibujo en curso se guarda en el proyecto (`SketchDraft`) y al generar se copia en los parámetros (`Sketch`), así el mismo mapa se rearma aunque se siga dibujando. Cambiar el dibujo o la fidelidad cuenta como cambio de costa (`ChangesCoast`).
  - Herramientas de dibujo (barra de "Dibujar"): Tierra (isla), Agua (lago o bahía), Montañas (línea abierta, clase `sketch-open` en el visor para que el trazo en vivo no se cierre) y Bioma (zona con el bioma elegido en un selector). Las islas siguen en la lista vieja y las zonas van en una lista aparte, así los proyectos guardados antes siguen abriendo.
  - Agua = "isla negativa": `pull = min(pull, -fuerza_agua × distancia_agua)`, con el doble de fuerza que la costa (los lagos son chicos: con la misma fuerza, un cuarto del lago salía tierra; con el doble, 89 % agua al 100 %). Agua encerrada en tierra queda como lago (`FindWater`); agua que toca el mar, como bahía.
  - Cordillera dibujada: loma de altura 0,9 y ancho 40 px a lo largo de la línea; en la prueba, 93 % de montaña o nieve junto a la línea contra 4 % en el resto.
  - Zona de bioma: el clima de la zona pasa al típico del bioma antes de los ríos (cambia la lluvia que cae ahí) y el bioma se fuerza al final. Los ríos que vienen de afuera igual cruzan un desierto dibujado (como el Nilo): en la prueba, desierto con ríos en 16 % de sus celdas y selva en 13 %. Si dos zonas se pisan, gana la última dibujada.
  - "Deshacer" revierte la última acción del dibujo: quita un trazo nuevo o devuelve uno borrado con la goma. Es una pila de acciones (`Stack<Action>` en `ProjectService`): cada cambio guarda cómo revertirse. Vive en memoria; tras recargar quita primero zonas y después islas. Se vacía al importar o empezar un proyecto.
  - Goma (`SketchTool.Eraser`, clase `sketch-eraser` en el visor): un clic borra el trazo de encima (`EraseSketchAt`: zonas de la última a la primera, después islas; las cordilleras a 15 px de la línea). En ese modo los trazos reciben el mouse y el de abajo del cursor se marca con el acento.
  - Retoques (dibujar sobre las otras plantillas): las mismas herramientas valen con cualquier plantilla ("Retoques" en el panel, botón "Retocar en el mapa"). Dos pasos en `ReliefStage.Elevations` (lo usan la etapa y las miniaturas): primero el nivel del mar con la plantilla sola, después `ReliefShape.Overlay` mide los retoques desde ese nivel. Así una isla agregada se suma en vez de que el porcentaje de tierra hunda tierra en otro lado. Tierra = máximo con la isla, agua = mínimo (borra tierra, hace lagos y bahías), cordillera = se suma.
  - Retoques, calibración: la costa de la isla se calcula como en Dibujo, pero su altura se lleva a la escala de la plantilla (cima de la isla = 0,55 de la cima del mapa; sin esto el centro de una isla chica salía con nieves eternas). La cordillera se mide con la cima de la plantilla. La escala final usa el fondo y la cima de la plantilla sola y recorta en -1 a 1 (con la cima nueva, todas las montañas del resto bajaban). La isla pesa entera hasta 60 px afuera de la línea y se desvanece hasta 240 px (sin eso, a fidelidad baja aparecían islotes lejos). El ruido de los retoques es propio (semilla del continente + 3) y no toca los números al azar de la plantilla.
  - Retoques, valores de la prueba (continente, archipiélago y pangea): isla agregada 86 % a 100 % tierra a fidelidad 70 % a 100 % (45 % a 30 %: sugerencia), sin montañas y con altura media parecida a la del mapa; agua dibujada 96 % a 100 % agua; cordillera 93 % a 100 % de montaña junto a la línea; lejos de los dibujos, 0 celdas cambian de tierra a agua a fidelidad 70 % o más. Sin dibujos, las 10 plantillas, Dibujo y las miniaturas dan exactamente lo mismo que antes (comparado celda por celda).
  - Compatibilidad: `GeneratorSettings.Retouches` marca los mapas generados con retoques. Un mapa guardado antes, con otra plantilla, podía tener un dibujo viejo sin usar: `Normalize` lo descarta si falta la marca, así el mundo se rearma igual que su imagen. Al cambiar de Dibujo a otra plantilla, lo dibujado pasa a ser retoque (y al revés). "Todo al azar" conserva los retoques.
  - Vista previa del dibujo en el panel: misma `ReliefShape` en 96 x 60; se rehace al terminar un trazo o al soltar el deslizador (no mientras se arrastra).
  - Cambiar plantilla o tierra no cambia la malla (las ediciones y los reinos siguen valiendo) pero sí la costa (`MapGenerator.ChangesCoast`): al generar se avisa y se borran los lugares generados sin tocar.
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
  - 18 tipos en 6 grupos: Asentamientos (Ciudad, Aldea, Puerto, Posada), Defensa (Fortaleza, Torre), Fe (Templo, Bosque sagrado), Lugares (Cueva, Mazmorra, Mina, Ruina, Sitio abandonado), Historia (Campo de batalla, Evento), Otros (Punto de interés, Facción). Agregar un tipo = sumarlo a `KindGroups` y dibujarle el ícono en `PinIcon`; un tipo desconocido usa el ícono de "Punto de interés".
  - Cómo se eligen: en "Colocar pines" aparece la barra de colocación (`.placebar`) con los íconos; los pines nuevos nacen con ese tipo y con el nombre del tipo como título. En la ficha, el tipo actual con "Cambiar", que abre la grilla. Si el título todavía es el nombre del tipo, cambiar el tipo cambia también el título; si el autor le puso nombre propio, se respeta.
  - Evitar íconos que se confundan con signos de interfaz: dos líneas cruzadas se leen como "X" de cerrar; por eso las espadas llevan guardas y pomos.
  - Íconos: grilla de 24 x 24 con 3 de margen, trazo 2, puntas y uniones redondeadas, sin relleno, `currentColor`. Se dibujan a 16 px dentro de una insignia de 28 px (`--pin`). Con detalles chicos (más de dos almenas, columnas sueltas) se empastan o se confunden: revisarlos agrandados (hoja temporal a 72 px) antes de dar uno por bueno.
  - El ícono es tinta o papel según el color de la capa (`--pin-ink`, calculado con `ContrastingInk`). Con estos dos colores, ningún fondo baja de 3,77:1 (mínimo WCAG para gráficos: 3:1). Paleta de capas verificada: el peor caso es #2980b9 con 3,78:1.
  - Capital: estrella en lugar del ícono del tipo y un segundo anillo de tinta (`outline`). La leyenda de la vista de reinos dice "capital: X". Marcar una capital la pasa a tamaño grande y su nombre va en mayúsculas.
  - Jerarquía visual (pedido del autor: que una capital no se vea igual que una aldea). Tamaño por pin (`Size`, chico 20 px, mediano 28, grande 36; ícono 12, 16, 20; tokens `--pin-*` e `--icon-*`), con nombre más chico o más grande y en negrita. Forma por grupo: Circle (asentamientos, fe), Square (defensa), Plain (lugares, historia, otros: sin insignia, ícono en el color de la capa con halo de papel y un escalón más grande, porque sin círculo se pierde). Los pines viejos sin `Size` quedan medianos, como se veían.
  - Cambiar el tipo cambia también el tamaño si todavía era el del tipo anterior (mismo criterio que el título).
  - Generalización: con el mapa alejado (menos del doble de la escala de ajuste) se ocultan los nombres de los lugares chicos (`.viewport.far`, clase que pone `mapview.js`); aparecen al acercarse.

- Fichas (wiki del mundo, decidido por el autor: mapa y entradas sueltas, enlaces en el texto, panel junto al mapa, formato simple):
  - Una ficha puede ser de un pin, de un reino o suelta (personaje, facción, evento...). La vinculada no tiene nombre propio: usa el del pin o el reino (`WikiLinks.NameOf`), así renombrar el lugar renombra la ficha. `Title` guarda el último nombre conocido por si el lugar desaparece.
  - Un pin o un reino reciben su `WikiEntry` recién cuando se les escribe algo, y la pierden si el texto queda vacío (`ProjectService.SetEntryText`). Escribir en la ficha de un lugar generado lo adopta (`Generated = false`), como editarlo o moverlo.
  - Borrar un pin, una capa con pines, un reino, los lugares generados o reemplazar los reinos al poblar: sus fichas pasan a ser sueltas con su nombre (`ReleaseEntries`). El texto del autor nunca se pierde por borrar un lugar; el aviso de borrar un pin lo dice.
  - Formato: `# ## ###` títulos, `**negrita**`, `*cursiva*`, `- ` o `* ` lista, `>` cita, `[[Nombre]]` y `[[Nombre|texto]]`. Un signo abre formato solo pegado a la palabra siguiente y cierra pegado a la anterior, con pareja en la misma línea ("5 * 3" queda como texto; lo encontró una prueba). Las líneas seguidas de un párrafo van con salto de línea (no se juntan como en Markdown).
  - Seguridad: el traductor devuelve bloques y trozos, no HTML; `WikiText` los dibuja con Razor, que escapa todo. Probado: `<img onerror>` en una ficha se muestra como texto y no se ejecuta. No usar `MarkupString` con texto del autor.
  - Templates de Razor de una sola línea en `WikiText`: con saltos de línea dentro del template, el espacio en blanco del código aparecía en el texto ("Corlis ." en lugar de "Corlis.").
  - Enlaces: se resuelven por nombre sin distinguir mayúsculas ni espacios de más; con nombres repetidos gana ficha suelta, después reino, después pin. Un nombre sin ficha se muestra punteado y tenue; al tocarlo se crea una ficha suelta (categoría "Otro") y se abre para escribir. "Mencionada en" no se guarda: se calcula leyendo los enlaces de todas las fichas.
  - Abrir la ficha de un lugar lo selecciona como un clic en su pin (y muestra su capa si estaba oculta); abrir un reino o una ficha suelta usa `_sheet` en `MapEditor`. Lugares y reinos centran el mapa (`centerOn`, con zoom al doble del ajuste); el reino se centra en su capital o en el punto de su nombre. Un clic en el mapa cierra la ficha abierta.
  - La ficha va en la hoja flotante de la derecha (360 px). `centerOn` centra en la parte del mapa que la hoja no tapa (lee `--sheet` y `--space-4` del CSS); bajo 900 px la hoja va debajo del mapa.
  - La lista de fichas muestra las que existen; al buscar, también lugares y reinos sin texto ("(sin texto)") para poder empezarlos. Se suscribe a `WikiChanged` y `StatusChanged`.
  - Proyectos viejos: la `Description` de cada pin pasa a su ficha en `NormalizeEntries` (si ya tenía ficha, va arriba del texto) y queda vacía. El dato guardado se actualiza en el próximo guardado; hasta entonces cada carga repite la migración sin duplicar.
  - Renombrar: `NameInput` recuerda el nombre al entrar al campo (`focus`) y al salir (`change`, que llega una vez, al salir o con Enter) pregunta si hay fichas con enlaces al nombre viejo que quedaron sin destino (`EntriesWithBrokenLinksTo`). Si otra cosa todavía se llama así, sus enlaces siguen andando y no se pregunta; tampoco si solo cambian mayúsculas o si el nombre nuevo está vacío. `RenameLinks` cambia el nombre y conserva el texto propio (`[[viejo|texto]]` pasa a `[[nuevo|texto]]`), con las mismas reglas que el traductor (no cruza líneas, nombre no vacío). Nombres parecidos ("Corthal Viejo") no se tocan.

- Poblados ("Poblar mapa", sección del panel del generador):
  - Semilla propia (`SettlementSeed`), cantidad (`SettlementDensity`: pocos, normal, muchos) y reinos (`KingdomCount`: 0 = usar los actuales, 2 a 8 = crear nuevos). No cambian la imagen: se aplican sobre el mundo en memoria.
  - Puntaje de celda: fertilidad del bioma + río (más si es grande) + lago + costa - altura, más una variación de la semilla de hasta 0,35. Ciudades con puntaje 0,9 o más, separadas 230/175/135 px según la cantidad; una aldea por ciudad (15 % posadas); ciudades costeras son puertos.
  - Reinos nuevos: capitales por "punto más lejano" entre las ciudades; cada celda es del reino que llega más barato (Dijkstra; montaña +5, río ancho +3, el mar no se cruza: islas sin capital quedan sin reino). Reinos existentes: su mejor ciudad pasa a capital si no tenían una elegida por el autor.
  - Extras: fortalezas en fronteras, cuevas en montañas que tocan tierra baja, ruinas y bosques sagrados en las celdas más alejadas de los poblados. Separación propia más chica respecto de los poblados (`takenGap`); sin eso no cabían.
  - Valores de la prueba (densidad normal): unos 70 lugares, 9 a 13 ciudades, 14 o 15 puertos, 5 o 6 fortalezas, 4 reinos de 12 % a 37 % de la tierra; 10 a 45 ms en PC, 1 s en el navegador.
  - Los pines generados van a la capa "Poblados" (sepia #6B4E3D) con `Generated = true`. Editarlos en la ficha, moverlos o hacerlos capital los "adopta" (`Generated = false`). Volver a poblar borra solo los generados; generar un mapa con otra malla también.
  - Mismas semillas, mismo mapa: cualquier cambio en las constantes de las etapas cambia los mapas ya generados (los mundos guardados conservan su imagen, pero "Generar" daría otro resultado). `GeneratorTests.KnownMaps_DoNotChange` lo detecta con huellas (SHA-256 de altura redondeada a 9 decimales, bioma y río de cada celda) de tres mapas. Si el cambio es a propósito, se actualiza la huella y se anota acá.

## Estado actual (2026-10-22)

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

- 2026-10-19: tipos Posada, Bosque sagrado y Campo de batalla (grupo nuevo "Historia"), barra de colocación con íconos y selector visual de tipo en la ficha (pedido por el autor: "que se vea mejor" la forma de agregar). Probado: la barra crea pines del tipo elegido con su nombre, la grilla de la ficha cambia el tipo, la barra se acomoda a 800 px sin desborde. Íconos nuevos revisados a 64 px y a tamaño real (16 px en la insignia); se corrigieron Campo de batalla (parecía una X) y Bosque sagrado (piedras como palitos).

- 2026-10-19: tamaño y forma de pines por importancia, generalización de nombres y "Poblar mapa" con reinos, capitales y nombres (pedido por el autor). Probado: poblar con 4 reinos nuevos (aviso de reemplazo, 64 lugares, capitales grandes con estrella), volver a poblar con otra semilla y los reinos actuales (aviso de 63 generados, la aldea renombrada se conservó, cada reino recibió capital), alejado se ven 46 nombres y cerca 79, revisión a tamaño real de fortalezas cuadradas y cuevas sin insignia. Ajustes tras la prueba: menos poblados, separación propia para los extras, nombres sin letras dobles, ícono más grande en los pines sin insignia.

- 2026-10-19: análisis pedido por el autor sobre "crear continentes con referencias" y "dibujar un contorno y que se genere adentro". Opciones: A plantillas procedurales, B mapas de alturas reales incluidos, C importar mapa de alturas; para el dibujo, camino 1 procedural guiado por el trazo (distancia con signo + ruido, deslizador "Fidelidad al trazo") o camino 2 IA generativa (difusión: requiere servidor y pierde los datos; modelo de lenguaje que traduce texto a parámetros: posible más adelante con un servidor mínimo para la clave). El autor eligió A y el camino 1.
- 2026-10-19: plantillas de relieve con miniaturas (opción A). Probado: compatibilidad exacta de "Continente", las 10 plantillas con tres semillas (tierra exacta, islas, sin lagos falsos), miniaturas en la interfaz, Mediterráneo generado y poblado de punta a punta con el aviso de lugares generados.

- 2026-10-19: dibujo guiado por el trazo (camino 1). Probado en la app: elegir Dibujo, trazar dos islas (simplificadas a 61 y 29 puntos), descarte de un garabato con aviso, vista previa que cambia con la fidelidad, el dibujo sobrevive a recargar, generar al 90 % (las islas salen con su forma) y al 20 % (inventa: se unen y aparecen islotes), proyecto nuevo sin imagen con hoja en blanco, y aviso si se genera sin islas. Se corrigió un error del paso anterior: elegir una plantilla antes de que terminaran las miniaturas rompía la app (el módulo de JS se liberaba mientras el bucle lo usaba); ahora `TemplatePicker` marca `_disposed` y corta el bucle.

- 2026-10-19: herramientas de dibujo (agua, cordilleras y zonas de bioma), pedido por el autor. Probado en la app: las cuatro herramientas con su dibujo en el mapa y el resumen en el panel, la cordillera queda abierta, y al generar al 85 % la isla tiene el lago, la cordillera nevada y el desierto donde se dibujaron. También se verificó que al generar se sale del modo dibujo.

- 2026-10-20: retoques sobre cualquier plantilla y goma (pedido por el autor: "generé un mapa al azar y quiero agregarle una isla", "y también borrar"). Probado en la app: continente generado, isla agregada en el mar y agua sobre un bosque (sale lago), los puntos lejanos no cambian, la goma borra una cordillera y una isla, "Deshacer" las devuelve, los retoques sobreviven a recargar y Dibujo sigue generando con los mismos trazos. Prueba fuera de la app: ver "Retoques, valores de la prueba".

- 2026-10-20: proyecto de pruebas xUnit (pedido por el autor, primero en la lista de próximos pasos). 41 pruebas, todas pasan en unos 2 s. Comprobado que fallan cuando deben: con `AltitudeCooling` en 0,41 fallan las 3 huellas, y con el alcance de los retoques en 1000 px falla la prueba de celdas lejanas.

- 2026-10-22: fichas del mundo (hoja de ruta, punto 2). Probado en la app: buscar un lugar sin texto y abrir su ficha, escribir con títulos, negrita, lista, cita y enlaces (uno con texto propio y uno a un nombre sin ficha), el texto sobrevive a recargar, seguir el enlace a un reino (ficha con capital y "Mencionada en", mapa centrado), volver por la mención, crear una ficha desde un enlace sin ficha, borrar un pin con texto (su ficha queda suelta y los enlaces siguen andando), y migración de la descripción de un proyecto viejo. 64 pruebas (23 nuevas del traductor y los enlaces). Se corrigieron dos errores encontrados en la prueba: el "*" suelto que abría cursiva y el espacio antes de la puntuación después de un enlace.

- 2026-10-22: renombrar arregla los enlaces (pedido por el autor). Probado en la app: renombrar un pin, una ficha suelta (rechazando: el enlace queda sin ficha) y un reino desde la lista (el texto propio del enlace se conserva), sin aviso al cambiar solo mayúsculas, todo guardado. 70 pruebas (6 nuevas).

- 2026-10-22: dirección visual B "Atlas encuadernado" (pedido por el autor: trabajar más la estética; eligió la B entre dos maquetas). Lomo con modos (los modos salieron de la barra de arriba, que ya no existe), mapa a pantalla completa, ficha como hoja flotante con nombre, línea de datos ("Ciudad · Unelor · capital"), "x" para cerrar y "Eliminar" al pie; tira de abajo con vista, etiquetas y ajuste; íconos SVG propios para modos, cerrar, subir y bajar (revisados a 72 y 16 px); ángulo propio en las secciones desplegables; botones que no se parten. Revisado en la vista previa en claro, oscuro, 1024 px y 375 px. Ajustes tras la revisión: la tira y la barra de dibujo se corren a la izquierda de la hoja (`:has(.sheet)`, solo en pantallas anchas) y `centerOn` centra en la parte visible del mapa.

Pendiente:

- Próximo paso posible: importar mapa de alturas (opción C).
- Lagos dibujados muy hondos (casi 1900 m en la prueba): el agua tiene el doble de fuerza y llega al fondo de la escala. Si molesta, limitar su profundidad.
- Próximo paso posible: rutas entre ciudades (caminos por el terreno más barato, como los reinos); deshacer la última edición; fronteras de reino suavizadas en el estilo Pintado; mostrar en la lista de reinos su capital y cantidad de pines; filtrar pines por tipo.
- Con muchos lugares, los nombres medianos todavía se pisan en ventanas chicas. Mejora posible: correr las etiquetas que chocan o generalizar también los medianos a más distancia.
- Ejercicio del autor (renombrar, nuevo): en `RenameLinksTests`, dos pruebas más: un enlace partido en dos líneas (`"[[Cor\nthal]]"` en C#) no se toca, y `[[Corthal|]]` (texto propio vacío) queda como `[[Puerto Gris|]]`. Pensar antes qué debería pasar y después correrlas.
- Ejercicio del autor (fichas, nuevo): `~~tachado~~` en el formato. Primero la prueba en `WikiMarkupTests` (que falle), después `Strike` en `WikiInline`, el signo en `ParseLine` (mismas reglas que `**`), y `<s>` en `WikiText.Span`.
- Ejercicio del autor (pruebas, nuevo): en `SketchesTests`, una prueba para `Sketches.Length` (una "L" de 3 + 4 mide 7) y otra para `DistanceToPolyline`; en un archivo nuevo `MeshTests.cs`, que `FindCell` con el centro de una celda devuelva esa celda (`[Theory]` con varias celdas, malla de `new Mesh(2000, 1250, 18, 42)`).
- Ejercicio del autor (nuevo): que la goma diga qué borró ("Se borró una cordillera."). `EraseSketchAt` devuelve `bool`; cambiarlo para que devuelva el nombre de lo borrado (o null si no tocó nada) y usarlo en `OnSketchErase` de `MapEditor`.
- Ejercicios del autor: "Distancia a la costa" en `CellInfo.razor`; botón "Seleccionar todo este terreno" en `CellPanel.razor` (necesita un `EventCallback` nuevo hacia `MapEditor`); vista "Ríos" o "Distancia a la costa" en `ThematicRenderer` (enum, nombre en `Views`, color en `RenderData`, leyenda en `Legend`); tipo de pin nuevo con ícono propio (por ejemplo "Faro") en `KindGroups` y `PinIcon`.
- Edición: cada cambio redibuja el SVG completo (2 a 3 s). Si molesta, dibujar solo lo que cambió o mostrar la selección mientras se redibuja.
- Generador: mientras genera, la interfaz se congela unos segundos (C# en WebAssembly corre en el mismo hilo). Opciones: compilación AOT o generar por partes cediendo el control.
- Generador: sombra de lluvia, nombres de lugares, símbolos de montaña. Ejercicio del autor: bioma "Pantano" en `BiomeStage.Classify`.
- Botón para elegir tema a mano (hoy sigue al sistema). Tipografías locales si se quiere usar sin conexión.
- Imagen como data URL: pesada en memoria y en interop para mapas grandes. Pasarla a Blob mueve parte del import/export a JS; esperar a que el autor diga si su SVG real tarda en cargar.
- El mapa no se reajusta solo al cambiar el tamaño de la ventana (hay que usar "Ajustar a pantalla").
- No existe la carpeta `/ejemplos` con el archivo de Azgaar (lo aporta el autor).
- Ejercicio del autor: mover el pin seleccionado con las flechas del teclado en `OnPinKeyDown`.

Dirección visual B "Atlas encuadernado" (elegida por el autor el 2026-10-22, reemplaza a la A "Carta impresa"): el mapa ocupa toda la mesa, un lomo oscuro a la izquierda lleva los modos y las listas, y las fichas y barras flotan como hojas de papel con sombra (`--lift`). EB Garamond (títulos, nombres en el mapa, fichas) y Source Sans 3 (controles).

Paleta B (contrastes AA verificados en todas las combinaciones de texto, tenue y acento sobre panel, campo y hover; al agregar un color nuevo, calcular su ratio antes de usarlo, por ejemplo con un script de Node):

- Hoja clara: panel #F3ECDD, campo #FBF7EF, hover #E8DFCC, texto #221C16, tenue #5E5444, borde #8A7D66, acento #80520F (peor caso: acento sobre hover 5,06; el ocre #8C5A14 de la maqueta daba 4,42 y se oscureció), texto sobre acento #FBF7EF. Mesa #3A332B.
- Lomo claro: panel #2A211B, campo #362C24, hover #433830, texto #EFE6D4, tenue #B9AC93, borde #8E806B, acento #D9A75B, texto sobre acento #2A211B.
- Hoja oscura: panel #29231C, campo #211C16, hover #362E26, texto #ECE3D1, tenue #B3A68D, borde #857A66, acento #DDAB60, texto sobre acento #1C1712. Mesa #14110E: la hoja contra la mesa da solo 1,21, la separa el borde (4,46).
- Lomo oscuro: panel #1C1712, campo #262019, hover #322A22, texto #EFE6D4, tenue #A99C84, borde #7F735F, acento #D9A75B.
- El lomo no tiene reglas propias para sus controles: `.spine` redefine los mismos tokens (`--panel`, `--bg`, `--text`, `--accent`...) con los valores `--spine-*`, y todo lo de adentro se pinta solo.
- Pines y etiquetas usan tinta #23201A y papel #F4EFE3 fijos, porque van sobre el mapa y no sobre la interfaz.

## Hoja de ruta (en orden)

0. Corregir los problemas de la revisión inicial. Falta: decidir lo del Blob (los íconos de pin se hicieron el 2026-10-19).
1. Importador de Azgaar (CSV/GeoJSON de ciudades a pines). Falta el archivo de ejemplo en `/ejemplos`.
2. Vincular pines con fichas de un wiki del mundo. Primera versión hecha el 2026-10-22 (ver "Fichas").
3. Generador de mapas por etapas (continente, clima, ríos, biomas), cada etapa con su semilla. Primera versión hecha el 2026-10-19; quedan mejoras (ver Pendiente).

## Forma de trabajo

- Antes de cambiar código, explicar el concepto y el plan en pocas líneas.
- Cambios chicos, uno por vez, verificando con `dotnet build`.
- Después de tocar el generador o los modelos, correr también `dotnet test Tests/MapaMundos.Tests`. Si una prueba falla, entender por qué antes de cambiar la prueba.
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
