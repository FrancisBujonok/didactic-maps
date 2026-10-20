using System.Text;
using MapaMundos.Models;

namespace MapaMundos.Generation;

/// <summary>
/// Punto de entrada del generador: arma la malla, ejecuta las etapas en orden y dibuja el SVG.
/// Cada etapa usa solo su semilla, así que cambiar una no altera lo que hicieron las anteriores.
/// </summary>
public static class MapGenerator
{
    /// <summary>Tamaño del mapa generado, en píxeles del SVG (proporción 16:10).</summary>
    public const double Width = 2000;
    public const double Height = 1250;

    /// <summary>Arma el mundo con las semillas y le aplica las ediciones hechas a mano.</summary>
    public static World Build(GeneratorSettings settings, IReadOnlyList<CellEdit> edits)
    {
        // La malla usa la semilla del continente con un desplazamiento, para que su secuencia
        // de números no sea la misma que usa el ruido del relieve.
        var mesh = new Mesh(Width, Height, settings.CellSize, settings.ContinentSeed + 7);
        var world = new World(mesh);

        ReliefStage.Apply(world, settings);
        if (EditStage.ApplyElevation(world, edits))
            ReliefStage.FindWater(world); // la costa cambió: se recalculan mar, lagos y distancias

        ClimateStage.Apply(world, settings.ClimateSeed);
        var drawn = settings.Template == ReliefTemplate.Sketch ? settings.SketchFeatures : new List<SketchFeature>();
        SketchStage.ApplyClimate(world, drawn);   // biomas dibujados: su clima, antes de los ríos
        RiverStage.Apply(world, settings.RiverSeed);
        BiomeStage.Apply(world, settings.BiomeSeed);
        SketchStage.ApplyBiomes(world, drawn);    // y su bioma, antes de las ediciones a mano
        EditStage.ApplyBiomes(world, edits);

        return world;
    }

    /// <summary>
    /// Genera el mundo y su dibujo. Devuelve los dos: el data URL para usar de imagen base y
    /// el mundo, que se guarda en memoria para consultar y editar las celdas.
    /// </summary>
    public static (World World, string DataUrl) Generate(GeneratorSettings settings, IReadOnlyList<CellEdit> edits)
    {
        var world = Build(settings, edits);
        return (world, Draw(world, settings.Style));
    }

    /// <summary>Dibuja un mundo ya armado y lo devuelve como data URL.</summary>
    public static string Draw(World world, MapStyle style) => ToDataUrl(SvgMapRenderer.Render(world, style));

    /// <summary>
    /// Convierte el texto de un SVG en un data URL ("data:image/svg+xml;base64,..."), que se
    /// puede usar directamente como src de una imagen y guardar como texto.
    /// </summary>
    public static string ToDataUrl(string svg) =>
        "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));

    /// <summary>
    /// True si con estos parámetros la malla sale distinta (otras celdas): entonces las
    /// ediciones a mano ya no apuntan a las mismas celdas y hay que descartarlas.
    /// </summary>
    public static bool ChangesMesh(GeneratorSettings before, GeneratorSettings after) =>
        before.ContinentSeed != after.ContinentSeed || before.CellSize != after.CellSize;

    /// <summary>
    /// True si cambia dónde está la tierra aunque las celdas sean las mismas (otra plantilla u
    /// otro porcentaje de tierra). Las ediciones siguen valiendo, pero los lugares generados
    /// pueden quedar en el mar.
    /// </summary>
    public static bool ChangesCoast(GeneratorSettings before, GeneratorSettings after) =>
        ChangesMesh(before, after) || before.Template != after.Template || before.LandRatio != after.LandRatio ||
        (after.Template == ReliefTemplate.Sketch && (!Sketches.SameFeatures(before.SketchFeatures, after.SketchFeatures) ||
         before.SketchFidelity != after.SketchFidelity || !Sketches.SameAs(before.Sketch, after.Sketch)));
}
