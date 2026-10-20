using MapaMundos.Models;

namespace MapaMundos.Generation;

/// <summary>
/// Etapa de biomas dibujados (plantilla Dibujo). Se aplica en dos momentos:
///
/// 1. Clima, antes de los ríos: la zona toma la temperatura y la humedad típicas de su bioma.
///    Así los ríos salen coherentes: pocos en un desierto, muchos en una selva.
/// 2. Biomas, al final: la zona toma el bioma elegido, aunque el clima diga otra cosa (por
///    ejemplo, un desierto dibujado junto a la costa, donde la humedad sería alta).
///
/// Las ediciones a mano se aplican después de esta etapa, así que siguen ganando.
/// </summary>
public static class SketchStage
{
    // Temperatura y humedad típicas de cada bioma (escala 0 a 1, como en ClimateStage). Quedan
    // dentro de la casilla correspondiente de la tabla de BiomeStage.
    private static readonly Dictionary<Biome, (double Temperature, double Moisture)> Climate = new()
    {
        [Biome.Ice] = (0.05, 0.3),
        [Biome.Tundra] = (0.2, 0.25),
        [Biome.Taiga] = (0.22, 0.55),
        [Biome.Grassland] = (0.45, 0.18),
        [Biome.TemperateForest] = (0.45, 0.45),
        [Biome.TemperateRainforest] = (0.45, 0.8),
        [Biome.Desert] = (0.8, 0.08),
        [Biome.Savanna] = (0.75, 0.3),
        [Biome.TropicalForest] = (0.75, 0.55),
        [Biome.Jungle] = (0.8, 0.85),
        [Biome.Mountain] = (0.35, 0.4),
        [Biome.Snow] = (0.05, 0.4),
    };

    public static void ApplyClimate(World world, IReadOnlyList<SketchFeature> features)
    {
        foreach (var (biome, cells) in Zones(world, features))
        {
            var (temperature, moisture) = Climate[biome];
            foreach (var cell in cells)
            {
                world.Temperature[cell] = temperature;
                world.Moisture[cell] = moisture;
            }
        }
    }

    public static void ApplyBiomes(World world, IReadOnlyList<SketchFeature> features)
    {
        foreach (var (biome, cells) in Zones(world, features))
        {
            foreach (var cell in cells)
                world.Biomes[cell] = biome;
        }
    }

    /// <summary>
    /// Cada zona de bioma con sus celdas de tierra: las que tienen el centro adentro del
    /// polígono dibujado. Si dos zonas se pisan, gana la dibujada después (va última).
    /// </summary>
    private static IEnumerable<(Biome Biome, List<int> Cells)> Zones(World world, IReadOnlyList<SketchFeature> features)
    {
        var mesh = world.Mesh;
        foreach (var feature in features)
        {
            if (feature.Kind != SketchFeatureKind.Biome || feature.Biome is not { } biome) continue;

            var polygon = Sketches.ToPixels(feature.Points, mesh.Width, mesh.Height);
            if (polygon.Length < 3) continue;

            // La caja que rodea al polígono evita probar las celdas que quedan lejos.
            var minX = polygon.Min(p => p.X);
            var maxX = polygon.Max(p => p.X);
            var minY = polygon.Min(p => p.Y);
            var maxY = polygon.Max(p => p.Y);

            var cells = new List<int>();
            for (var i = 0; i < mesh.Count; i++)
            {
                if (!world.IsLand(i) || mesh.X[i] < minX || mesh.X[i] > maxX || mesh.Y[i] < minY || mesh.Y[i] > maxY) continue;
                if (Sketches.Contains(polygon, mesh.X[i], mesh.Y[i])) cells.Add(i);
            }

            yield return (biome, cells);
        }
    }
}
