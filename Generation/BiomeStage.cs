using MapaMundos.Models;

namespace MapaMundos.Generation;

/// <summary>
/// Etapa 4, biomas: el tipo de terreno de cada celda según su temperatura, humedad y altura.
///
/// Es una versión simplificada del diagrama de Whittaker, que usan los ecólogos: con poca
/// temperatura hay tundra o taiga, con calor y sequía desierto, con calor y lluvia selva.
/// La semilla de esta etapa agrega un poco de ruido a la temperatura y la humedad antes de
/// consultar la tabla, para que los límites entre biomas no sigan las curvas del clima.
/// </summary>
public static class BiomeStage
{
    public static void Apply(World world, int seed)
    {
        var mesh = world.Mesh;
        var noise = new PerlinNoise(seed);

        for (var i = 0; i < mesh.Count; i++)
        {
            if (world.IsOcean(i))
            {
                world.Biomes[i] = Biome.Ocean;
                continue;
            }
            if (world.IsLake[i])
            {
                world.Biomes[i] = Biome.Lake;
                continue;
            }

            var nx = mesh.X[i] / mesh.Height * 8;
            var ny = mesh.Y[i] / mesh.Height * 8;
            var temperature = world.Temperature[i] + noise.Sample(nx, ny) * 0.06;
            var moisture = world.Moisture[i] + noise.Sample(nx + 31.7, ny + 17.3) * 0.08;

            world.Biomes[i] = Classify(temperature, moisture, world.Elevation[i]);
        }
    }

    /// <summary>La tabla de biomas. Temperatura y humedad van de 0 a 1; la altura, de 0 a 1.</summary>
    public static Biome Classify(double temperature, double moisture, double elevation)
    {
        if (elevation > 0.6) return temperature < 0.2 ? Biome.Snow : Biome.Mountain;

        if (temperature < 0.12) return Biome.Ice;

        if (temperature < 0.3)
            return moisture < 0.4 ? Biome.Tundra : Biome.Taiga;

        if (temperature < 0.6)
        {
            if (moisture < 0.25) return Biome.Grassland;
            if (moisture < 0.6) return Biome.TemperateForest;
            return Biome.TemperateRainforest;
        }

        if (moisture < 0.2) return Biome.Desert;
        if (moisture < 0.4) return Biome.Savanna;
        if (moisture < 0.65) return Biome.TropicalForest;
        return Biome.Jungle;
    }
}
