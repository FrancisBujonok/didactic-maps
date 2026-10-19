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

    public static World Build(GeneratorSettings settings)
    {
        // La malla usa la semilla del continente con un desplazamiento, para que su secuencia
        // de números no sea la misma que usa el ruido del relieve.
        var mesh = new Mesh(Width, Height, settings.CellSize, settings.ContinentSeed + 7);
        var world = new World(mesh);

        ReliefStage.Apply(world, settings.ContinentSeed, settings.LandRatio);
        ClimateStage.Apply(world, settings.ClimateSeed);
        RiverStage.Apply(world, settings.RiverSeed);
        BiomeStage.Apply(world, settings.BiomeSeed);

        return world;
    }

    /// <summary>
    /// Genera el mundo y su dibujo. Devuelve los dos: el data URL para usar de imagen base y
    /// el mundo, que se guarda en memoria para consultar las celdas.
    /// </summary>
    public static (World World, string DataUrl) Generate(GeneratorSettings settings)
    {
        var world = Build(settings);
        var svg = SvgMapRenderer.Render(world, settings.Style);
        return (world, "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg)));
    }
}
