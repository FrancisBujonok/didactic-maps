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

    /// <summary>Valida colores del estilo "#rrggbb" (se usa antes de ponerlos en CSS).</summary>
    public static bool IsHexColor(string? value) =>
        value is { Length: 7 } && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);
}

/// <summary>
/// Un punto del mapa con su ficha (título, tipo, descripción).
/// </summary>
public class MapPin
{
    public static readonly string[] Kinds =
    {
        "Ciudad", "Fortaleza", "Ruina", "Templo", "Punto de interés", "Facción", "Evento"
    };

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
}
