namespace MapaMundos.Models;

/// <summary>
/// Plantilla de relieve: la forma general de la tierra (ver Generation/ReliefShape.cs).
/// Continent es la forma original, así los mapas guardados antes de las plantillas no cambian.
/// </summary>
public enum ReliefTemplate
{
    Continent,
    Continents,
    Pangea,
    Archipelago,
    VolcanicIsland,
    Peninsula,
    Isthmus,
    Mediterranean,
    Atoll,
    Shattered,

    /// <summary>La costa la dibuja el autor sobre el mapa (ver SketchFidelity).</summary>
    Sketch
}

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

    /// <summary>Forma general de la tierra. La semilla del continente decide los detalles.</summary>
    public ReliefTemplate Template { get; set; } = ReliefTemplate.Continent;

    /// <summary>
    /// Dibujo con el que se generó la tierra (plantilla Sketch): cada elemento es una isla, como
    /// lista plana de coordenadas normalizadas [x0, y0, x1, y1, ...] entre 0 y 1. Se guarda acá
    /// (y no solo en el proyecto) para que la misma configuración rearme siempre el mismo mundo.
    /// </summary>
    public List<double[]> Sketch { get; set; } = new();

    /// <summary>Cuánto respeta la costa el dibujo: 0 casi nada (inventa), 1 sigue la línea.</summary>
    public double SketchFidelity { get; set; } = 0.7;

    /// <summary>Zonas dibujadas que no son tierra (agua, cordilleras, biomas), usadas en el mapa actual.</summary>
    public List<SketchFeature> SketchFeatures { get; set; } = new();

    /// <summary>Latitud del mapa y manchas de humedad.</summary>
    public int ClimateSeed { get; set; }

    /// <summary>Variación de la lluvia que alimenta a los ríos.</summary>
    public int RiverSeed { get; set; }

    /// <summary>Irregularidad de los límites entre biomas.</summary>
    public int BiomeSeed { get; set; }

    /// <summary>Porcentaje del mapa que es tierra, entre 0.05 y 0.7 (un atolón necesita poca).</summary>
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
    public GeneratorSettings Clone()
    {
        // MemberwiseClone copia los números, pero la lista del dibujo quedaría compartida entre
        // las dos copias: se copia aparte, isla por isla.
        var copy = (GeneratorSettings)MemberwiseClone();
        copy.Sketch = Sketch.Select(island => (double[])island.Clone()).ToList();
        copy.SketchFeatures = Sketches.CopyFeatures(SketchFeatures);
        return copy;
    }

    /// <summary>Deja los valores dentro de rangos válidos (por ejemplo, tras importar un archivo).</summary>
    public void Normalize()
    {
        LandRatio = Math.Clamp(LandRatio, 0.05, 0.7);
        if (!DetailLevels.Any(d => d.Size == CellSize))
            CellSize = 18;
        SettlementDensity = Math.Clamp(SettlementDensity, 0, 2);
        KingdomCount = Math.Clamp(KingdomCount, 0, 8);
        if (SettlementSeed == 0) SettlementSeed = 1; // proyectos guardados antes de esta etapa
        SketchFidelity = Math.Clamp(SketchFidelity, 0, 1);
        Sketch = Sketches.Clean(Sketch);
        SketchFeatures = Sketches.CleanFeatures(SketchFeatures);
        if (!Enum.IsDefined(Template))
            Template = ReliefTemplate.Continent;
        if (!Enum.IsDefined(Style))
            Style = MapStyle.Painted;
    }
}
