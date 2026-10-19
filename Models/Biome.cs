namespace MapaMundos.Models;

/// <summary>
/// Tipos de terreno. El generador decide el de cada celda, y el autor puede cambiarlo a mano
/// (por eso está en Models: el proyecto guardado recuerda los biomas elegidos a mano).
/// </summary>
public enum Biome
{
    Ocean,
    Lake,
    Ice,
    Tundra,
    Taiga,
    Grassland,
    TemperateForest,
    TemperateRainforest,
    Desert,
    Savanna,
    TropicalForest,
    Jungle,
    Mountain,
    Snow
}

/// <summary>Nombres de los biomas para mostrar en la interfaz.</summary>
public static class BiomeNames
{
    /// <summary>Los biomas de tierra, en el orden en que se ofrecen para editar a mano.</summary>
    public static readonly Biome[] Land =
    {
        Biome.Ice, Biome.Tundra, Biome.Taiga, Biome.Grassland, Biome.TemperateForest,
        Biome.TemperateRainforest, Biome.Desert, Biome.Savanna, Biome.TropicalForest,
        Biome.Jungle, Biome.Mountain, Biome.Snow
    };

    public static string Of(Biome biome) => biome switch
    {
        Biome.Ocean => "Mar",
        Biome.Lake => "Lago",
        Biome.Ice => "Hielo",
        Biome.Tundra => "Tundra",
        Biome.Taiga => "Taiga",
        Biome.Grassland => "Pradera",
        Biome.TemperateForest => "Bosque templado",
        Biome.TemperateRainforest => "Bosque lluvioso templado",
        Biome.Desert => "Desierto",
        Biome.Savanna => "Sabana",
        Biome.TropicalForest => "Bosque tropical",
        Biome.Jungle => "Selva",
        Biome.Mountain => "Montaña",
        Biome.Snow => "Nieves eternas",
        _ => biome.ToString()
    };
}
