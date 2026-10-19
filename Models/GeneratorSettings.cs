namespace MapaMundos.Models;

/// <summary>
/// Parámetros del generador de mapas. Se guardan con el proyecto: con los mismos valores
/// se obtiene exactamente el mismo mapa.
/// </summary>
public class GeneratorSettings
{
    /// <summary>Forma del continente y relieve (también ubica los puntos de la malla).</summary>
    public int ContinentSeed { get; set; }

    /// <summary>Latitud del mapa y manchas de humedad.</summary>
    public int ClimateSeed { get; set; }

    /// <summary>Variación de la lluvia que alimenta a los ríos.</summary>
    public int RiverSeed { get; set; }

    /// <summary>Irregularidad de los límites entre biomas.</summary>
    public int BiomeSeed { get; set; }

    /// <summary>Porcentaje del mapa que es tierra, entre 0.2 y 0.7.</summary>
    public double LandRatio { get; set; } = 0.4;

    /// <summary>Separación entre celdas en píxeles: menos separación, más detalle y más demora.</summary>
    public int CellSize { get; set; } = 18;

    /// <summary>Tamaños de celda que ofrece la interfaz.</summary>
    public static readonly (int Size, string Name)[] DetailLevels =
    {
        (24, "Bajo"), (18, "Medio"), (13, "Alto")
    };

    /// <summary>Configuración nueva con las cuatro semillas al azar.</summary>
    public static GeneratorSettings CreateRandom() => new()
    {
        ContinentSeed = NewSeed(),
        ClimateSeed = NewSeed(),
        RiverSeed = NewSeed(),
        BiomeSeed = NewSeed()
    };

    /// <summary>
    /// Semilla nueva. Acá sí sirve Random.Shared: no importa repetir este número,
    /// solo que sea distinto cada vez. Se limita a 6 cifras para que sea fácil de anotar.
    /// </summary>
    public static int NewSeed() => Random.Shared.Next(1, 1_000_000);

    /// <summary>Copia independiente: el panel edita una copia y solo la guarda al generar.</summary>
    public GeneratorSettings Clone() => (GeneratorSettings)MemberwiseClone();

    /// <summary>Deja los valores dentro de rangos válidos (por ejemplo, tras importar un archivo).</summary>
    public void Normalize()
    {
        LandRatio = Math.Clamp(LandRatio, 0.2, 0.7);
        if (!DetailLevels.Any(d => d.Size == CellSize))
            CellSize = 18;
    }
}
