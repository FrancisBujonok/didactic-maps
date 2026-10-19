using System.Text;
using MapaMundos.Models;

namespace MapaMundos.Generation;

/// <summary>Las vistas del mapa: una a la vez, como capas que se ponen encima del terreno.</summary>
public enum MapView
{
    Terrain,
    Regions,
    Elevation,
    Temperature,
    Moisture
}

/// <summary>Escala de colores de una vista de datos, para dibujar su leyenda.</summary>
public sealed record ColorScale(string Title, string[] Stops, string MinLabel, string MaxLabel);

/// <summary>Un nombre de reino para poner sobre el mapa, en coordenadas normalizadas (0 a 1).</summary>
public sealed record RegionLabel(string Name, double X, double Y);

/// <summary>
/// Dibuja las vistas que no son el terreno. Son SVG del mismo tamaño que el mapa generado,
/// así se apoyan encima de la imagen base sin calcular nada más.
///
/// - Vistas de datos (altura, temperatura, humedad): cada celda pintada según un valor,
///   en escalones de color. Tapan el terreno por completo.
/// - Reinos: solo las celdas con reino, semitransparentes, más las fronteras. El terreno se
///   sigue viendo debajo.
/// </summary>
public static class ThematicRenderer
{
    /// <summary>Vistas que ofrece la interfaz, con su nombre.</summary>
    public static readonly (MapView View, string Name)[] Views =
    {
        (MapView.Terrain, "Terreno"),
        (MapView.Regions, "Reinos"),
        (MapView.Elevation, "Altura"),
        (MapView.Temperature, "Temperatura"),
        (MapView.Moisture, "Humedad"),
    };

    // Cantidad de escalones de color. Con pocos escalones las zonas se leen como en un atlas
    // (franjas de igual valor) y el archivo tiene pocos colores distintos.
    private const int Steps = 12;

    private const string NeutralWater = "#C9D6DA";

    // Escalas: de verde (tierras bajas) a nieve; mar de azul profundo a celeste;
    // de azul (frío) a rojo (calor) pasando por un tono neutro; de ocre (seco) a verde azulado.
    private static readonly string[] LandStops = { "#7FA36B", "#C9C98A", "#B99A68", "#8C7358", "#F4F1EA" };
    private static readonly string[] SeaStops = { "#2E5A7A", "#A9C8D6" };
    private static readonly string[] TemperatureStops = { "#3B6EA5", "#9CC3D9", "#F2EFE6", "#E5A66B", "#B5402A" };
    private static readonly string[] MoistureStops = { "#D9C39A", "#B9C98A", "#6FA57A", "#2F7F8F" };

    /// <summary>Leyenda de la vista, o null si la vista no tiene escala.</summary>
    public static ColorScale? Legend(MapView view) => view switch
    {
        MapView.Elevation => new("Altura", LandStops, "0 m", "4.500 m"),
        MapView.Temperature => new("Temperatura media", TemperatureStops, "-15 °C", "35 °C"),
        MapView.Moisture => new("Humedad", MoistureStops, "Seca", "Húmeda"),
        _ => null
    };

    // ---------------------------------------------------------- vistas de datos

    /// <summary>Dibuja una vista de datos completa (tapa el terreno).</summary>
    public static string RenderData(World world, MapView view)
    {
        var mesh = world.Mesh;
        Func<int, string> colorOf = view switch
        {
            MapView.Elevation => i => world.Elevation[i] > 0
                ? Scale(LandStops, world.Elevation[i])
                : Scale(SeaStops, 1 + world.Elevation[i]), // -1 (lo más hondo) da 0 en la escala
            MapView.Temperature => i => Scale(TemperatureStops, world.Temperature[i]),
            MapView.Moisture => i => world.Water[i] ? NeutralWater : Scale(MoistureStops, world.Moisture[i]),
            _ => throw new ArgumentException("No es una vista de datos.", nameof(view))
        };

        var svg = Open(mesh);
        svg.Append($"<rect width=\"100%\" height=\"100%\" fill=\"{colorOf(0)}\"/>");

        // Celdas agrupadas por color, como en el mapa de terreno: un path por color.
        var groups = new Dictionary<string, StringBuilder>();
        for (var i = 0; i < mesh.Count; i++)
        {
            if (mesh.Polygons[i] is not { } polygon) continue;
            var color = colorOf(i);
            if (!groups.TryGetValue(color, out var path))
                groups[color] = path = new StringBuilder();
            AppendPolygon(path, polygon);
        }
        foreach (var (color, path) in groups)
            svg.Append($"<path fill=\"{color}\" stroke=\"{color}\" stroke-width=\"0.8\" stroke-linejoin=\"round\" d=\"{path}\"/>");

        // La costa en tinta ayuda a ubicarse aunque los colores cambien.
        var (ocean, lake) = SvgMapRenderer.TraceShores(world, 0);
        SvgMapRenderer.AppendStroke(svg, SvgMapRenderer.ClosedPath(lake), SvgMapRenderer.Ink, 0.8);
        SvgMapRenderer.AppendStroke(svg, SvgMapRenderer.ClosedPath(ocean), SvgMapRenderer.Ink, 1.2);

        svg.Append("</svg>");
        return svg.ToString();
    }

    // ------------------------------------------------------------------ reinos

    /// <summary>
    /// Dueño de cada celda: el índice del reino en la lista, o -1 si no tiene. Solo cuentan las
    /// celdas de tierra que existen en esta malla (un reino puede tener celdas que una edición
    /// volvió agua).
    /// </summary>
    public static int[] Owners(World world, IReadOnlyList<Region> regions)
    {
        var owner = new int[world.Mesh.Count];
        Array.Fill(owner, -1);
        for (var r = 0; r < regions.Count; r++)
        {
            foreach (var cell in regions[r].Cells)
            {
                if (cell >= 0 && cell < owner.Length && world.IsLand(cell) && owner[cell] < 0)
                    owner[cell] = r;
            }
        }
        return owner;
    }

    /// <summary>
    /// Capa de reinos: relleno de color de cada reino, recortado con la costa del mapa, y
    /// fronteras entre celdas de dueños distintos.
    /// </summary>
    public static string RenderRegions(World world, IReadOnlyList<Region> regions, MapStyle style)
    {
        var mesh = world.Mesh;
        var owner = Owners(world, regions);
        var svg = Open(mesh);

        // Mismo molde de tierra que el mapa de terreno, así los rellenos no se salen de la costa.
        var passes = style == MapStyle.Painted ? SvgMapRenderer.SmoothPasses : 0;
        var (ocean, lake) = SvgMapRenderer.TraceShores(world, passes);
        svg.Append($"<defs><clipPath id=\"tierra\"><path clip-rule=\"evenodd\" d=\"{SvgMapRenderer.ClosedPath(ocean)}{SvgMapRenderer.ClosedPath(lake)}\"/></clipPath></defs>");

        // La transparencia va en el grupo y no en cada celda: si cada celda fuera transparente,
        // los bordes compartidos se pintarían dos veces y se vería la grilla más oscura.
        svg.Append("<g clip-path=\"url(#tierra)\" opacity=\"0.45\">");
        for (var r = 0; r < regions.Count; r++)
        {
            var path = new StringBuilder();
            for (var i = 0; i < mesh.Count; i++)
            {
                if (owner[i] == r && mesh.Polygons[i] is { } polygon)
                    AppendPolygon(path, polygon);
            }
            if (path.Length == 0) continue;

            var color = regions[r].Color;
            svg.Append($"<path fill=\"{color}\" stroke=\"{color}\" stroke-width=\"0.8\" stroke-linejoin=\"round\" d=\"{path}\"/>");
        }
        svg.Append("</g>");

        // Fronteras: el lado entre dos celdas de tierra con dueños distintos (o un reino y tierra
        // sin reino). La costa no hace falta, ya está en el mapa de terreno.
        var borders = new StringBuilder();
        for (var i = 0; i < mesh.Count; i++)
        {
            if (owner[i] < 0 || mesh.Polygons[i] is not { } polygon) continue;
            var neighbors = mesh.Neighbors[i];
            for (var k = 0; k < 6; k++)
            {
                var other = neighbors[k];
                if (!world.IsLand(other) || owner[other] == owner[i]) continue;

                // Cada frontera entre dos reinos aparece desde las dos celdas: se dibuja una sola
                // vez, desde la celda de número menor (o siempre, si la otra no tiene reino).
                if (owner[other] >= 0 && other < i) continue;

                var from = polygon[(k + 5) % 6];
                var to = polygon[k];
                borders.Append('M').Append(SvgMapRenderer.F(from.X)).Append(' ').Append(SvgMapRenderer.F(from.Y))
                       .Append('L').Append(SvgMapRenderer.F(to.X)).Append(' ').Append(SvgMapRenderer.F(to.Y));
            }
        }
        if (borders.Length > 0)
        {
            svg.Append($"<path fill=\"none\" stroke=\"{SvgMapRenderer.Ink}\" stroke-opacity=\"0.75\" stroke-width=\"1.6\" stroke-linecap=\"round\" d=\"{borders}\"/>");
        }

        svg.Append("</svg>");
        return svg.ToString();
    }

    /// <summary>
    /// Dónde escribir el nombre de cada reino: en la celda del reino más cercana al promedio de
    /// sus celdas. El promedio solo podría caer fuera del reino (por ejemplo, en un reino con
    /// forma de medialuna), por eso se busca la celda propia más cercana a ese punto.
    /// </summary>
    public static List<RegionLabel> Labels(World world, IReadOnlyList<Region> regions)
    {
        var mesh = world.Mesh;
        var owner = Owners(world, regions);
        var labels = new List<RegionLabel>();

        for (var r = 0; r < regions.Count; r++)
        {
            var cells = Enumerable.Range(0, mesh.Count).Where(i => owner[i] == r).ToList();
            if (cells.Count == 0) continue;

            var meanX = cells.Average(i => mesh.X[i]);
            var meanY = cells.Average(i => mesh.Y[i]);
            var best = cells.MinBy(i => (mesh.X[i] - meanX) * (mesh.X[i] - meanX) + (mesh.Y[i] - meanY) * (mesh.Y[i] - meanY));
            labels.Add(new RegionLabel(regions[r].Name, mesh.X[best] / mesh.Width, mesh.Y[best] / mesh.Height));
        }
        return labels;
    }

    // ------------------------------------------------------------- utilidades

    private static StringBuilder Open(Mesh mesh)
    {
        var svg = new StringBuilder(1 << 19);
        svg.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{SvgMapRenderer.F(mesh.Width)}\" height=\"{SvgMapRenderer.F(mesh.Height)}\" viewBox=\"0 0 {SvgMapRenderer.F(mesh.Width)} {SvgMapRenderer.F(mesh.Height)}\">");
        return svg;
    }

    private static void AppendPolygon(StringBuilder path, (double X, double Y)[] polygon)
    {
        path.Append('M').Append(SvgMapRenderer.F(polygon[0].X)).Append(' ').Append(SvgMapRenderer.F(polygon[0].Y));
        for (var k = 1; k < polygon.Length; k++)
            path.Append('L').Append(SvgMapRenderer.F(polygon[k].X)).Append(' ').Append(SvgMapRenderer.F(polygon[k].Y));
        path.Append('Z');
    }

    /// <summary>
    /// Color para un valor entre 0 y 1 en una escala de varios colores. Primero se redondea el
    /// valor a uno de los escalones; después se mezclan los dos colores de la escala entre los
    /// que cae.
    /// </summary>
    private static string Scale(string[] stops, double value)
    {
        var t = Math.Round(Math.Clamp(value, 0, 1) * (Steps - 1)) / (Steps - 1);
        var position = t * (stops.Length - 1);
        var index = Math.Min((int)position, stops.Length - 2);
        return SvgMapRenderer.Mix(stops[index], stops[index + 1], position - index);
    }
}
