namespace MapaMundos.Models;

/// <summary>Cómo se dibuja el mapa generado. No cambia el mundo, solo su aspecto.</summary>
public enum MapStyle
{
    /// <summary>Costas suaves y transiciones difuminadas, como un mapa pintado.</summary>
    Painted,

    /// <summary>Cada celda visible con su borde: la grilla del generador a la vista.</summary>
    Cells
}

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

    public MapStyle Style { get; set; } = MapStyle.Painted;

    // ---- Poblados ("Poblar mapa"). No cambian la imagen: se aplican sobre el mundo ya generado.

    /// <summary>Dónde caen las ciudades, aldeas y lugares, y cómo se llaman.</summary>
    public int SettlementSeed { get; set; }

    /// <summary>Cantidad de poblados: 0 pocos, 1 normal, 2 muchos.</summary>
    public int SettlementDensity { get; set; } = 1;

    /// <summary>Reinos a crear alrededor de las capitales, o 0 para usar los reinos que ya hay.</summary>
    public int KingdomCount { get; set; } = 4;

    public static readonly (int Value, string Name)[] DensityLevels =
    {
        (0, "Pocos"), (1, "Normal"), (2, "Muchos")
    };

    /// <summary>Tamaños de celda que ofrece la interfaz.</summary>
    public static readonly (int Size, string Name)[] DetailLevels =
    {
        (24, "Bajo"), (18, "Medio"), (13, "Alto")
    };

    /// <summary>Configuración nueva con todas las semillas al azar.</summary>
    public static GeneratorSettings CreateRandom() => new()
    {
        ContinentSeed = NewSeed(),
        ClimateSeed = NewSeed(),
        RiverSeed = NewSeed(),
        BiomeSeed = NewSeed(),
        SettlementSeed = NewSeed()
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
        SettlementDensity = Math.Clamp(SettlementDensity, 0, 2);
        KingdomCount = Math.Clamp(KingdomCount, 0, 8);
        if (SettlementSeed == 0) SettlementSeed = 1; // proyectos guardados antes de esta etapa
        if (!Enum.IsDefined(Style))
            Style = MapStyle.Painted;
    }
}
