namespace MapaMundos.Models;

/// <summary>Tipo de camino: cambia cómo se dibuja y cuánto se tarda en recorrerlo.</summary>
public enum RouteKind
{
    Highway, // camino real: el más rápido
    Road,    // camino
    Trail    // sendero: el más lento
}

/// <summary>
/// Un camino entre dos lugares (pines). Points es el recorrido como lista plana normalizada
/// [x0, y0, x1, y1, ...] entre 0 y 1, igual que los pines y los dibujos: no depende del tamaño
/// del mapa. Se guarda en MapProject.Routes.
/// </summary>
public class Route
{
    public static readonly (RouteKind Kind, string Name)[] Kinds =
    {
        (RouteKind.Highway, "Camino real"), (RouteKind.Road, "Camino"), (RouteKind.Trail, "Sendero")
    };

    public static string NameOf(RouteKind kind) => Kinds.First(k => k.Kind == kind).Name;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FromPinId { get; set; }
    public Guid ToPinId { get; set; }
    public RouteKind Kind { get; set; } = RouteKind.Road;
    public double[] Points { get; set; } = Array.Empty<double>();

    /// <summary>
    /// True si lo trazó "Poblar mapa" o "Rehacer caminos" y el autor no lo tocó. Como con los
    /// pines: al rehacer los caminos solo se reemplazan estos.
    /// </summary>
    public bool Generated { get; set; }
}
