namespace MapaMundos.Models;

/// <summary>
/// Lo que se guarda de un mundo: sus capas y sus pines.
/// La imagen base NO va acá porque es pesada: se guarda aparte (ver ProjectService).
/// </summary>
public class MapProject
{
    public string Name { get; set; } = "Mi mundo";
    public List<MapLayer> Layers { get; set; } = new();
    public List<MapPin> Pins { get; set; } = new();

    /// <summary>Parámetros con los que se generó la imagen base, o null si la imagen se cargó de un archivo.</summary>
    public GeneratorSettings? Generator { get; set; }

    /// <summary>Cambios hechos a mano sobre celdas del mapa generado (ver CellEdit).</summary>
    public List<CellEdit> CellEdits { get; set; } = new();

    /// <summary>Reinos o regiones del mapa generado (ver Region).</summary>
    public List<Region> Regions { get; set; } = new();

    /// <summary>Capa donde "Poblar mapa" pone los lugares que crea, o null si todavía no se pobló.</summary>
    public Guid? SettlementLayerId { get; set; }
}

/// <summary>
/// Una capa agrupa pines. El orden en la lista define el orden de dibujo:
/// la primera capa de la lista se dibuja por encima de las demás.
/// </summary>
public class MapLayer
{
    public const string DefaultColor = "#c0392b";

    /// <summary>Colores que se van asignando a las capas nuevas.</summary>
    public static readonly string[] Palette = { "#c0392b", "#2980b9", "#27ae60", "#f39c12", "#8e44ad", "#16a085" };

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Capa";
    public bool Visible { get; set; } = true;
    public string Color { get; set; } = DefaultColor;

    /// <summary>
    /// Color para dibujar encima de un fondo de este color: tinta o papel, el que tenga más
    /// contraste según la fórmula de WCAG (luminancia relativa). Así el ícono de un pin se lee
    /// sobre cualquier color de capa, claro u oscuro.
    /// </summary>
    public static string ContrastingInk(string background)
    {
        const string ink = "#23201A", paper = "#F4EFE3";
        var l = Luminance(IsHexColor(background) ? background : DefaultColor);
        var withInk = (l + 0.05) / (Luminance(ink) + 0.05);
        var withPaper = (Luminance(paper) + 0.05) / (l + 0.05);
        return withInk >= withPaper ? ink : paper;
    }

    // Luminancia relativa de WCAG: cada canal se pasa a escala lineal y se pondera según
    // cuánto lo percibe el ojo (el verde pesa mucho más que el azul).
    private static double Luminance(string hex)
    {
        double Channel(int index)
        {
            var c = Convert.ToInt32(hex.Substring(1 + index * 2, 2), 16) / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(0) + 0.7152 * Channel(1) + 0.0722 * Channel(2);
    }

    /// <summary>Valida colores del estilo "#rrggbb" (se usa antes de ponerlos en CSS).</summary>
    public static bool IsHexColor(string? value) =>
        value is { Length: 7 } && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);
}

/// <summary>
/// Un punto del mapa con su ficha (título, tipo, descripción).
/// </summary>
/// <summary>Forma del pin en el mapa. Depende del grupo de su tipo.</summary>
public enum PinShape
{
    /// <summary>Insignia redonda con el color de la capa (asentamientos, fe).</summary>
    Circle,

    /// <summary>Insignia cuadrada: lo fortificado se distingue de un vistazo.</summary>
    Square,

    /// <summary>Sin insignia: solo el ícono, con un halo de papel. Pesa menos que una ciudad.</summary>
    Plain
}

/// <summary>Tamaño del pin: marca la importancia del lugar, como en los mapas impresos.</summary>
public enum PinSize
{
    Small,
    Medium,
    Large
}

public class MapPin
{
    /// <summary>
    /// Tipos de pin agrupados como se muestran en el selector, con la forma que lleva cada grupo.
    /// Cada tipo tiene su ícono en Components/PinIcon.razor: si se agrega uno acá, hay que
    /// dibujarle el ícono allá (y, si hace falta, darle un tamaño en DefaultSizes).
    /// </summary>
    public static readonly (string Group, PinShape Shape, string[] Kinds)[] KindGroups =
    {
        ("Asentamientos", PinShape.Circle, new[] { "Ciudad", "Aldea", "Puerto", "Posada" }),
        ("Defensa", PinShape.Square, new[] { "Fortaleza", "Torre" }),
        ("Fe", PinShape.Circle, new[] { "Templo", "Bosque sagrado" }),
        ("Lugares", PinShape.Plain, new[] { "Cueva", "Mazmorra", "Mina", "Ruina", "Sitio abandonado" }),
        ("Historia", PinShape.Plain, new[] { "Campo de batalla", "Evento" }),
        ("Otros", PinShape.Plain, new[] { "Punto de interés", "Facción" }),
    };

    /// <summary>Todos los tipos en una sola lista (para validar y para el valor por defecto).</summary>
    public static readonly string[] Kinds = KindGroups.SelectMany(g => g.Kinds).ToArray();

    /// <summary>Tamaño con el que nace cada tipo. Los que no están acá nacen chicos.</summary>
    private static readonly Dictionary<string, PinSize> DefaultSizes = new()
    {
        ["Ciudad"] = PinSize.Large,
        ["Puerto"] = PinSize.Medium,
        ["Fortaleza"] = PinSize.Medium,
        ["Templo"] = PinSize.Medium,
        ["Facción"] = PinSize.Medium,
    };

    public static PinSize DefaultSize(string kind) => DefaultSizes.GetValueOrDefault(kind, PinSize.Small);

    /// <summary>Forma del grupo al que pertenece el tipo (un tipo desconocido va sin insignia).</summary>
    public static PinShape ShapeOf(string kind)
    {
        foreach (var (_, shape, kinds) in KindGroups)
        {
            if (kinds.Contains(kind)) return shape;
        }
        return PinShape.Plain;
    }

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LayerId { get; set; }

    /// <summary>
    /// Posición NORMALIZADA: 0 = borde izquierdo/superior, 1 = borde derecho/inferior.
    /// Así el pin sigue en su lugar aunque cambies la imagen por otra de distinta resolución.
    /// </summary>
    public double X { get; set; }
    public double Y { get; set; }

    public string Title { get; set; } = "Nuevo pin";
    public string Kind { get; set; } = Kinds[0];
    public string Description { get; set; } = "";

    /// <summary>
    /// Importancia del lugar. Los proyectos guardados antes de que existiera no traen este
    /// dato: entonces queda el valor de acá (mediano), que es como se veían todos.
    /// </summary>
    public PinSize Size { get; set; } = PinSize.Medium;

    /// <summary>
    /// True si lo creó "Poblar mapa" y el autor todavía no lo tocó. Al volver a poblar, solo
    /// se reemplazan estos; los que el autor editó dejan de contar como generados y se conservan.
    /// </summary>
    public bool Generated { get; set; }
}
