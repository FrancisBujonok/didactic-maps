using System.Globalization;
using System.Text;
using MapaMundos.Models;

namespace MapaMundos.Generation;

/// <summary>
/// Etapa final: dibuja el mundo como un SVG (texto con figuras vectoriales).
///
/// Las celdas son polígonos con esquinas, así que dibujarlas tal cual deja costas en serrucho.
/// Por eso la costa se dibuja aparte: se encadenan los lados entre tierra y agua en contornos
/// cerrados, se suavizan, y la tierra se recorta con ese contorno suave.
///
/// Orden de dibujo, de abajo hacia arriba:
/// 1. Mar profundo (rectángulo de fondo) y bandas de mar costero (la costa trazada con líneas anchas).
/// 2. Lagos.
/// 3. Celdas de tierra coloreadas por bioma, recortadas con el contorno suave.
/// 4. Ríos, orillas de lagos y línea de costa.
/// </summary>
public static class SvgMapRenderer
{
    // Colores de mapa impreso: apagados para que los pines y etiquetas resalten encima.
    internal const string DeepSea = "#9DBDCB";
    private const string MidSea = "#B1CCD6";
    private const string ShallowSea = "#C6DBE1";
    private const string LakeWater = "#B9D3DB";
    private const string RiverWater = "#4E7C9B";
    internal const string Ink = "#23201A";
    private const string Shadow = "#3B3226"; // hacia este color se oscurece la tierra alta

    // Veces que se suaviza cada línea. Cada pasada duplica los puntos; con 2 ya no se ven esquinas.
    internal const int SmoothPasses = 2;

    /// <summary>Color de un bioma en el mapa (lo usa también el editor para las zonas dibujadas).</summary>
    public static string BiomeColor(Biome biome) => BiomeColors.GetValueOrDefault(biome, "#CFCB93");

    private static readonly Dictionary<Biome, string> BiomeColors = new()
    {
        [Biome.Ice] = "#F2F1EC",
        [Biome.Tundra] = "#C9C6AE",
        [Biome.Taiga] = "#8C9C7C",
        [Biome.Grassland] = "#CFCB93",
        [Biome.TemperateForest] = "#9DAF78",
        [Biome.TemperateRainforest] = "#7D9A6B",
        [Biome.Desert] = "#E6D6A8",
        [Biome.Savanna] = "#D6C686",
        [Biome.TropicalForest] = "#93AA66",
        [Biome.Jungle] = "#6E8E57",
        [Biome.Mountain] = "#AFA591",
        [Biome.Snow] = "#FAF8F2",
    };

    /// <summary>
    /// Dibuja el mundo. El estilo "Pintado" suaviza costas y difumina la tierra; el estilo
    /// "Celdas" deja las costas sobre los lados de las celdas y marca el borde de cada una.
    /// </summary>
    public static string Render(World world, MapStyle style)
    {
        var mesh = world.Mesh;
        var painted = style == MapStyle.Painted;
        var svg = new StringBuilder(1 << 20);

        var (oceanShores, lakeShores) = TraceShores(world, painted ? SmoothPasses : 0);
        var coastPath = ClosedPath(oceanShores);
        var lakePath = ClosedPath(lakeShores);

        svg.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{mesh.Width}\" height=\"{mesh.Height}\" viewBox=\"0 0 {mesh.Width} {mesh.Height}\">");

        // Molde de la tierra: todos los contornos juntos. Con la regla "evenodd" un contorno
        // dentro de otro es un agujero, así los lagos quedan afuera del molde.
        svg.Append("<defs>");
        svg.Append($"<clipPath id=\"tierra\"><path clip-rule=\"evenodd\" d=\"{coastPath}{lakePath}\"/></clipPath>");

        // El desenfoque borra los escalones hexagonales entre biomas y alturas: la tierra queda
        // con transiciones suaves, como pintada a mano. La costa no se desenfoca porque se
        // dibuja después, como línea aparte, y el molde recorta el borde borroso.
        if (painted)
        {
            svg.Append(CultureInfo.InvariantCulture,
                $"<filter id=\"suave\"><feGaussianBlur stdDeviation=\"{mesh.Spacing * 0.35:0.#}\"/></filter>");
        }
        svg.Append("</defs>");

        // ---- 1. Mar
        svg.Append($"<rect width=\"100%\" height=\"100%\" fill=\"{DeepSea}\"/>");
        // Una línea ancha centrada en la costa: la mitad que cae sobre tierra queda tapada,
        // la otra mitad es una franja de agua clara que sigue la forma de la costa.
        AppendStroke(svg, coastPath, MidSea, mesh.Spacing * 8);
        AppendStroke(svg, coastPath, ShallowSea, mesh.Spacing * 3);

        // ---- 2. Lagos
        svg.Append($"<path fill=\"{LakeWater}\" fill-rule=\"evenodd\" d=\"{lakePath}\"/>");

        // ---- 3. Tierra
        // Celdas agrupadas por color: un solo path por color hace el archivo mucho más liviano.
        var groups = new Dictionary<string, StringBuilder>();
        for (var i = 0; i < mesh.Count; i++)
        {
            var polygon = mesh.Polygons[i];
            var color = painted ? CellColor(world, i) : world.IsLand(i) ? LandColor(world, i) : null;
            if (polygon is null || color is null) continue;

            if (!groups.TryGetValue(color, out var path))
                groups[color] = path = new StringBuilder();

            path.Append('M').Append(F(polygon[0].X)).Append(' ').Append(F(polygon[0].Y));
            for (var k = 1; k < polygon.Length; k++)
                path.Append('L').Append(F(polygon[k].X)).Append(' ').Append(F(polygon[k].Y));
            path.Append('Z');
        }

        svg.Append(painted ? "<g clip-path=\"url(#tierra)\"><g filter=\"url(#suave)\">" : "<g><g>");
        foreach (var (color, path) in groups)
        {
            // Pintado: el borde del mismo color tapa las rayitas finas que el navegador deja entre
            // polígonos vecinos. Celdas: un borde apenas más oscuro deja ver la grilla.
            var stroke = painted ? color : Mix(color, Ink, 0.18);
            svg.Append($"<path fill=\"{color}\" stroke=\"{stroke}\" stroke-width=\"0.8\" stroke-linejoin=\"round\" d=\"{path}\"/>");
        }
        svg.Append("</g></g>");

        // ---- 4. Líneas
        AppendRivers(svg, world);
        AppendStroke(svg, lakePath, RiverWater, 1);
        AppendStroke(svg, coastPath, Ink, 1.4);

        svg.Append("</svg>");
        return svg.ToString();
    }

    /// <summary>
    /// Color de relleno de una celda, o null si no se dibuja.
    /// Las celdas de agua que tocan tierra se pintan como su vecina de tierra: el contorno suave
    /// a veces pasa un poco por encima de ellas, y así no queda un hueco entre la costa y la tierra.
    /// </summary>
    private static string? CellColor(World world, int i)
    {
        if (world.Water[i])
        {
            var land = world.Mesh.Neighbors[i].FirstOrDefault(world.IsLand, -1);
            return land >= 0 ? LandColor(world, land) : null;
        }
        return LandColor(world, i);
    }

    // Sombreado por altura en 5 escalones: la tierra alta se ve más oscura y se lee el relieve.
    // Son pocos escalones a propósito, así hay pocos colores distintos y pocos paths.
    private static string LandColor(World world, int i)
    {
        var step = Math.Round(world.Elevation[i] * 5) / 5;
        return Mix(BiomeColors[world.Biomes[i]], Shadow, step * 0.3);
    }

    // ------------------------------------------------------------------ costas

    /// <summary>
    /// Encadena los lados entre tierra y agua en contornos cerrados y los suaviza.
    /// Los separa en costa de mar y orilla de lago, que se dibujan distinto.
    ///
    /// Cada lado va de un vértice a otro, siempre con la tierra del mismo lado. En cada vértice
    /// se juntan 3 celdas, así que por cada vértice de la costa sale exactamente un lado:
    /// buscando el lado que sale de donde terminó el anterior se recorre el contorno entero.
    /// </summary>
    internal static (List<List<(double X, double Y)>> Ocean, List<List<(double X, double Y)>> Lake) TraceShores(World world, int passes)
    {
        var mesh = world.Mesh;
        var sides = new Dictionary<(long, long), ((double X, double Y) To, bool Lake)>();

        for (var i = 0; i < mesh.Count; i++)
        {
            var polygon = mesh.Polygons[i];
            if (polygon is null || world.Water[i]) continue;

            for (var k = 0; k < 6; k++)
            {
                var other = mesh.Neighbors[i][k];
                if (!world.Water[other]) continue;
                sides[Key(polygon[(k + 5) % 6])] = (polygon[k], world.IsLake[other]);
            }
        }

        var ocean = new List<List<(double X, double Y)>>();
        var lake = new List<List<(double X, double Y)>>();

        while (sides.Count > 0)
        {
            var (startKey, first) = sides.First();
            sides.Remove(startKey);

            var loop = new List<(double X, double Y)> { first.To };
            var current = first.To;
            while (sides.Remove(Key(current), out var next))
            {
                loop.Add(next.To);
                current = next.To;
            }

            (first.Lake ? lake : ocean).Add(SmoothClosed(loop, passes));
        }

        return (ocean, lake);
    }

    // Dos cálculos del mismo vértice pueden diferir en el último decimal: se redondea a 0.01 px.
    private static (long, long) Key((double X, double Y) p) =>
        ((long)Math.Round(p.X * 100), (long)Math.Round(p.Y * 100));

    /// <summary>
    /// Suavizado de Chaikin para un contorno cerrado: cada lado se reemplaza por dos puntos,
    /// a 1/4 y a 3/4 de su largo. Las esquinas se "cortan" y, repitiendo, quedan curvas.
    /// </summary>
    private static List<(double X, double Y)> SmoothClosed(List<(double X, double Y)> points, int passes)
    {
        for (var pass = 0; pass < passes; pass++)
        {
            var result = new List<(double X, double Y)>(points.Count * 2);
            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                var q = points[(i + 1) % points.Count];
                result.Add(Lerp(p, q, 0.25));
                result.Add(Lerp(p, q, 0.75));
            }
            points = result;
        }
        return points;
    }

    /// <summary>Chaikin para una línea abierta: igual, pero conserva el primer y el último punto.</summary>
    private static List<(double X, double Y)> SmoothOpen(List<(double X, double Y)> points)
    {
        for (var pass = 0; pass < SmoothPasses && points.Count > 2; pass++)
        {
            var result = new List<(double X, double Y)>(points.Count * 2) { points[0] };
            for (var i = 0; i < points.Count - 1; i++)
            {
                if (i > 0) result.Add(Lerp(points[i], points[i + 1], 0.25));
                if (i < points.Count - 2) result.Add(Lerp(points[i], points[i + 1], 0.75));
            }
            result.Add(points[^1]);
            points = result;
        }
        return points;
    }

    // ------------------------------------------------------------------- ríos

    /// <summary>
    /// Cada río se recorre desde su naciente hasta el mar, un lago o el río al que se une,
    /// y se dibuja como una línea suavizada. El grosor crece con el caudal en tres escalones.
    ///
    /// Al unirse a otro río, el afluente termina en el punto medio del tramo siguiente del río
    /// principal. El suavizado de Chaikin nunca mueve esos puntos medios, así que la unión
    /// queda exacta aunque las dos líneas estén suavizadas.
    /// </summary>
    private static void AppendRivers(StringBuilder svg, World world)
    {
        var mesh = world.Mesh;
        var widths = new[] { 1.0, 1.8, 2.8 };
        var paths = widths.Select(_ => new StringBuilder()).ToArray();

        bool IsRiver(int i) => world.RiverSize(i) >= 0;
        (double X, double Y) Center(int i) => (mesh.X[i], mesh.Y[i]);
        (double X, double Y) Shore(int a, int b) => Lerp(Center(a), Center(b), 0.5);
        int WidthClass(int i) => world.RiverSize(i); // 0 arroyo, 1 río, 2 río caudaloso

        // Una celda es naciente si es río y ninguna celda de tierra con río drena hacia ella.
        var fedByRiver = new bool[mesh.Count];
        for (var i = 0; i < mesh.Count; i++)
        {
            if (IsRiver(i) && world.IsLand(i) && world.Downstream[i] >= 0)
                fedByRiver[world.Downstream[i]] = true;
        }

        var drawn = new bool[mesh.Count];
        for (var start = 0; start < mesh.Count; start++)
        {
            if (!IsRiver(start) || drawn[start]) continue;

            var points = new List<(double X, double Y)>();
            var classes = new List<int>(); // grosor de cada tramo (entre points[k] y points[k + 1])
            int cell;

            if (world.IsLake[start])
            {
                // Salida de un lago: el río arranca en la orilla.
                var below = world.Downstream[start];
                if (below < 0 || world.Water[below]) continue;
                points.Add(Shore(start, below));
                points.Add(Center(below));
                classes.Add(WidthClass(start));
                if (drawn[below]) { Flush(points, classes, paths); continue; }
                cell = below;
            }
            else
            {
                if (fedByRiver[start]) continue; // no es naciente: lo dibuja el río que llega
                points.Add(Center(start));
                cell = start;
            }

            drawn[cell] = true;
            while (true)
            {
                var below = world.Downstream[cell];
                if (below < 0) break;

                classes.Add(WidthClass(cell));

                if (world.Water[below])
                {
                    points.Add(Shore(cell, below)); // desemboca en el mar o en un lago
                    break;
                }

                if (drawn[below])
                {
                    // Se une a un río ya dibujado: termina en el medio de su tramo siguiente.
                    var after = world.Downstream[below];
                    var next = after < 0 ? Center(below)
                        : world.Water[after] ? Shore(below, after) : Center(after);
                    points.Add(Lerp(Center(below), next, 0.5));
                    break;
                }

                points.Add(Center(below));
                drawn[below] = true;
                cell = below;
            }

            Flush(points, classes, paths);
        }

        for (var s = 0; s < widths.Length; s++)
        {
            if (paths[s].Length == 0) continue;
            svg.Append(CultureInfo.InvariantCulture,
                $"<path fill=\"none\" stroke=\"{RiverWater}\" stroke-width=\"{widths[s]}\" stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\"{paths[s]}\"/>");
        }
    }

    /// <summary>Corta el río en tramos del mismo grosor, suaviza cada uno y lo agrega a su path.</summary>
    private static void Flush(List<(double X, double Y)> points, List<int> classes, StringBuilder[] paths)
    {
        var from = 0;
        for (var k = 1; k <= classes.Count; k++)
        {
            if (k < classes.Count && classes[k] == classes[from]) continue;

            // Tramo de points[from] a points[k], todo con el mismo grosor.
            var stretch = SmoothOpen(points.GetRange(from, k - from + 1));
            AppendOpen(paths[classes[from]], stretch);
            from = k;
        }
    }

    // ------------------------------------------------------------- utilidades

    internal static string ClosedPath(List<List<(double X, double Y)>> loops)
    {
        var d = new StringBuilder();
        foreach (var loop in loops)
        {
            d.Append('M').Append(F(loop[0].X)).Append(' ').Append(F(loop[0].Y));
            for (var k = 1; k < loop.Count; k++)
                d.Append('L').Append(F(loop[k].X)).Append(' ').Append(F(loop[k].Y));
            d.Append('Z');
        }
        return d.ToString();
    }

    private static void AppendOpen(StringBuilder d, List<(double X, double Y)> line)
    {
        d.Append('M').Append(F(line[0].X)).Append(' ').Append(F(line[0].Y));
        for (var k = 1; k < line.Count; k++)
            d.Append('L').Append(F(line[k].X)).Append(' ').Append(F(line[k].Y));
    }

    internal static void AppendStroke(StringBuilder svg, string d, string color, double width)
    {
        if (d.Length == 0) return;
        svg.Append(CultureInfo.InvariantCulture,
            $"<path fill=\"none\" stroke=\"{color}\" stroke-width=\"{width:0.#}\" stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\"{d}\"/>");
    }

    private static (double X, double Y) Lerp((double X, double Y) a, (double X, double Y) b, double t) =>
        (a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    /// <summary>Mezcla dos colores "#rrggbb": amount 0 devuelve a, 1 devuelve b.</summary>
    internal static string Mix(string a, string b, double amount)
    {
        int Channel(string hex, int index) => Convert.ToInt32(hex.Substring(1 + index * 2, 2), 16);

        var result = new StringBuilder("#");
        for (var c = 0; c < 3; c++)
        {
            var value = Channel(a, c) + (Channel(b, c) - Channel(a, c)) * amount;
            result.Append(((int)Math.Round(value)).ToString("X2"));
        }
        return result.ToString();
    }

    // Un decimal alcanza: a 16x de zoom, 0.1 px de imagen son 1.6 px de pantalla.
    // InvariantCulture porque en es-AR el separador decimal es la coma y SVG necesita punto.
    internal static string F(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
