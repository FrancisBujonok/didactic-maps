using System.Globalization;
using System.Text;
using MapaMundos.Models;

namespace MapaMundos.Generation;

/// <summary>
/// Detalle de la tierra en el estilo Pintado: sombreado de relieve y símbolos de atlas antiguo
/// (montañas, colinas y bosques). Solo dibuja: el mundo no cambia.
/// </summary>
internal static class LandDecor
{
    // ------------------------------------------------------------- sombreado

    // Luz desde el noroeste y a 45 grados, como en los mapas físicos: en pantalla, arriba a la
    // izquierda (x crece a la derecha, y hacia abajo, z hacia el que mira).
    private static readonly (double X, double Y, double Z) Light = Normalize((-1, -1, 1.4));

    // Cuánto se exagera la pendiente. La altura va de 0 a 1 y se mide por celda: sin exagerar,
    // hasta las montañas parecen casi planas a la luz.
    private const double Relief = 6;

    // Escalones de sombra: pocos, así hay pocos colores distintos (y pocos paths), como los
    // escalones de altura de la tierra.
    private const double ShadeStep = 0.06;
    private const int ShadeLevels = 6;

    /// <summary>
    /// Sombreado de relieve de una celda de tierra, en escalones de -6 a 6: positivo si su ladera
    /// mira a la luz (se aclara), negativo si le da la espalda (se oscurece), 0 si es plana.
    /// </summary>
    public static int ShadeLevel(World world, int i)
    {
        var (gx, gy) = Gradient(world, i);
        var normal = Normalize((-gx * Relief, -gy * Relief, 1));
        var light = normal.X * Light.X + normal.Y * Light.Y + normal.Z * Light.Z;

        // Light.Z es lo que recibe una celda plana: la referencia del sombreado.
        var level = (int)Math.Round((light - Light.Z) / ShadeStep);
        return Math.Clamp(level, -ShadeLevels, ShadeLevels);
    }

    /// <summary>
    /// Color de una celda con su sombreado. Pintar una sombra semitransparente encima de un
    /// color es lo mismo que mezclar el color con la sombra, así que el sombreado va directo en el
    /// color de cada celda: el mismo resultado que una capa aparte, sin repetir los polígonos de
    /// toda la tierra (eso sumaba 240 KB al SVG y más de un segundo en el navegador).
    /// </summary>
    public static string Shade(string color, int level) => level switch
    {
        < 0 => SvgMapRenderer.Mix(color, "#2E261C", -level * 0.07), // sombra en tinta parda
        > 0 => SvgMapRenderer.Mix(color, "#FFFFFF", level * 0.055), // luz en blanco
        _ => color
    };

    /// <summary>
    /// Pendiente de una celda (cuánto sube la altura por celda en x y en y), con mínimos
    /// cuadrados: el plano que mejor pasa por la celda y sus seis vecinas. El mar cuenta como
    /// altura 0, así la tierra baja hacia la costa.
    /// </summary>
    private static (double X, double Y) Gradient(World world, int i)
    {
        var mesh = world.Mesh;
        double sxx = 0, sxy = 0, syy = 0, sxe = 0, sye = 0;
        var height = Math.Max(world.Elevation[i], 0);

        foreach (var n in mesh.Neighbors[i])
        {
            var dx = (mesh.X[n] - mesh.X[i]) / mesh.Spacing;
            var dy = (mesh.Y[n] - mesh.Y[i]) / mesh.Spacing;
            var de = Math.Max(world.Elevation[n], 0) - height;
            sxx += dx * dx; sxy += dx * dy; syy += dy * dy;
            sxe += dx * de; sye += dy * de;
        }

        // Sistema de 2 x 2: [sxx sxy; sxy syy] [gx gy] = [sxe sye], resuelto con el determinante.
        var det = sxx * syy - sxy * sxy;
        if (Math.Abs(det) < 1e-9) return (0, 0);
        return ((sxe * syy - sye * sxy) / det, (sye * sxx - sxe * sxy) / det);
    }

    // ------------------------------------------------------------- símbolos

    /// <summary>
    /// Dibujos de los símbolos, en píxeles del mapa con el pie en (0, 0). Cada uno es primero un
    /// relleno (tapa lo que queda atrás, así las montañas se superponen como en un grabado) y
    /// después el trazo en tinta. El relleno lo pone cada uso (fill del &lt;use&gt;).
    /// </summary>
    private const string Defs =
        // Montañas: tres perfiles distintos (si todas son iguales, la cordillera parece un papel
        // tapiz), siempre con el lado en sombra a la derecha, rayado: la luz viene del noroeste.
        // Dos cumbres:
        "<symbol id=\"monte\" overflow=\"visible\">" +
        "<path d=\"M-13 0L-3 -15L0 -11L4 -16L14 0Z\" stroke=\"none\"/>" +
        "<path d=\"M-13 0L-3 -15L0 -11L4 -16L14 0\" fill=\"none\" stroke=\"#3B3226\" stroke-width=\"1.1\" stroke-linejoin=\"miter\"/>" +
        "<path d=\"M5 -13L6 0M7 -10L9 0M9 -7L11.5 0M-1 -9L0 -3\" fill=\"none\" stroke=\"#3B3226\" stroke-width=\"0.7\"/>" +
        "</symbol>" +
        // Un pico alto:
        "<symbol id=\"monte-alto\" overflow=\"visible\">" +
        "<path d=\"M-11 0L0 -19L11 0Z\" stroke=\"none\"/>" +
        "<path d=\"M-11 0L0 -19L11 0\" fill=\"none\" stroke=\"#3B3226\" stroke-width=\"1.1\" stroke-linejoin=\"miter\"/>" +
        "<path d=\"M2 -14L3.5 0M4.5 -10L6.5 0M7 -6L8.5 0\" fill=\"none\" stroke=\"#3B3226\" stroke-width=\"0.7\"/>" +
        "</symbol>" +
        // Tres picos bajos:
        "<symbol id=\"monte-bajo\" overflow=\"visible\">" +
        "<path d=\"M-14 0L-8 -10L-4 -6L1 -13L5 -8L8 -11L14 0Z\" stroke=\"none\"/>" +
        "<path d=\"M-14 0L-8 -10L-4 -6L1 -13L5 -8L8 -11L14 0\" fill=\"none\" stroke=\"#3B3226\" stroke-width=\"1\" stroke-linejoin=\"miter\"/>" +
        "<path d=\"M3 -10L4 0M9.5 -8L10.5 0M11.5 -5L12.5 0\" fill=\"none\" stroke=\"#3B3226\" stroke-width=\"0.7\"/>" +
        "</symbol>" +
        // Colina: una loma baja, con dos rayas de sombra.
        "<symbol id=\"colina\" overflow=\"visible\">" +
        "<path d=\"M-9 0Q-1 -10 9 0Z\" stroke=\"none\"/>" +
        "<path d=\"M-9 0Q-1 -10 9 0\" fill=\"none\" stroke=\"#3B3226\" stroke-width=\"0.9\"/>" +
        "<path d=\"M3 -3L4.5 0M5.5 -1.8L6.5 0\" fill=\"none\" stroke=\"#3B3226\" stroke-width=\"0.6\"/>" +
        "</symbol>" +
        // Árbol de copa redonda (bosques templados, selva).
        "<symbol id=\"arbol\" overflow=\"visible\">" +
        "<path d=\"M0 0V-3\" fill=\"none\" stroke=\"#3B3226\" stroke-width=\"0.8\"/>" +
        "<circle cx=\"0\" cy=\"-5.5\" r=\"3.3\" stroke=\"#3B3226\" stroke-width=\"0.7\"/>" +
        "</symbol>" +
        // Pino (taiga): un triángulo sobre un tronco corto.
        "<symbol id=\"pino\" overflow=\"visible\">" +
        "<path d=\"M0 0V-2\" fill=\"none\" stroke=\"#3B3226\" stroke-width=\"0.8\"/>" +
        "<path d=\"M-3 -2L0 -10L3 -2Z\" stroke=\"#3B3226\" stroke-width=\"0.7\" stroke-linejoin=\"miter\"/>" +
        "</symbol>";

    public static void AppendDefs(StringBuilder svg) => svg.Append(Defs);

    private sealed record Symbol(string Id, double X, double Y, double Scale, string Fill);

    /// <summary>
    /// Ubica y dibuja los símbolos: montañas en las celdas más altas, colinas en las alturas
    /// medias y árboles en los bosques. Las montañas y colinas se eligen de la más alta a la más
    /// baja, sin pisarse; todos se dibujan de arriba hacia abajo del mapa, así lo de adelante
    /// (más abajo) tapa a lo de atrás, como en un grabado. Posiciones con una semilla fija:
    /// el mismo mundo da siempre el mismo dibujo.
    /// </summary>
    public static void AppendSymbols(StringBuilder svg, World world)
    {
        var mesh = world.Mesh;
        var rng = new Rng(7);
        var symbols = new List<Symbol>();

        // Color de relleno por bioma, calculado una sola vez: Mix lee y escribe texto hexadecimal,
        // y llamarlo por cada árbol (miles) se notaba en el navegador.
        var fills = new Dictionary<(Biome, string), string>();
        string Fill(Biome biome, string toward, double amount) =>
            fills.TryGetValue((biome, toward), out var color) ? color
            : fills[(biome, toward)] = SvgMapRenderer.Mix(SvgMapRenderer.BiomeColor(biome), toward, amount);
        // Símbolos ya ubicados, en una grilla de casilleros de 48 px: para saber si un lugar está
        // libre alcanza con mirar el casillero del punto y sus 8 vecinos, en vez de comparar con
        // todos los símbolos (con miles de árboles, eso tardaba segundos en el navegador). Sirve
        // porque ningún símbolo ocupa más de 24 px de radio (una montaña grande con detalle bajo
        // llega a 20): dos que se tocan están a menos de 48.
        const double Bucket = 48;
        var grid = new Dictionary<(int, int), List<(double X, double Y, double Radius)>>();
        (int, int) KeyOf(double x, double y) => ((int)Math.Floor(x / Bucket), (int)Math.Floor(y / Bucket));

        bool Free(double x, double y, double radius)
        {
            var (bx, by) = KeyOf(x, y);
            for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
            {
                if (!grid.TryGetValue((bx + dx, by + dy), out var near)) continue;
                foreach (var p in near)
                {
                    if ((p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y) < (p.Radius + radius) * (p.Radius + radius))
                        return false;
                }
            }
            return true;
        }

        void Place(double x, double y, double radius)
        {
            var key = KeyOf(x, y);
            if (!grid.TryGetValue(key, out var list))
                grid[key] = list = new List<(double X, double Y, double Radius)>();
            list.Add((x, y, radius));
        }

        // Celdas de tierra lejos de la costa (los símbolos no se salen al mar) y sin río (un
        // árbol o una montaña encima de un río lo taparía).
        bool Usable(int i) => world.IsLand(i) && world.CoastDistance[i] >= 1 && world.RiverSize(i) < 0;

        // Montañas y colinas, de la celda más alta a la más baja.
        var byHeight = Enumerable.Range(0, mesh.Count).Where(Usable)
            .Where(i => world.Elevation[i] >= 0.38)
            .OrderByDescending(i => world.Elevation[i]);
        foreach (var i in byHeight)
        {
            var elevation = world.Elevation[i];
            var mountain = elevation >= 0.58 || world.Biomes[i] is Biome.Mountain or Biome.Snow;
            var x = mesh.X[i] + rng.Range(-0.25, 0.25) * mesh.Spacing;
            var y = mesh.Y[i] + rng.Range(-0.25, 0.25) * mesh.Spacing;

            if (mountain)
            {
                // Más grande cuanto más alta (las cumbres principales se destacan), con un poco
                // de azar para que no queden todas del mismo tamaño.
                var size = Math.Clamp(0.8 + (elevation - 0.58) * 1.8, 0.8, 1.55) * rng.Range(0.9, 1.1);
                var scale = size * mesh.Spacing / 18;
                if (!Free(x, y, 9 * scale)) continue; // menos que su ancho: se montan y forman cordones
                Place(x, y, 9 * scale);
                var fill = world.Biomes[i] == Biome.Snow ? "#FAF8F2" : "#D8D0BE";
                var shape = elevation >= 0.8 ? "monte-alto" : rng.NextDouble() < 0.5 ? "monte" : "monte-bajo";
                symbols.Add(new Symbol(shape, x, y, scale, fill));
            }
            else
            {
                // Las colinas van más espaciadas y no en todas las celdas: si no, todo el
                // interior se llena de lomas y se pierde el contraste con las montañas.
                var scale = mesh.Spacing / 18;
                if (rng.NextDouble() > 0.45 || !Free(x, y, 12 * scale)) continue;
                Place(x, y, 12 * scale);
                symbols.Add(new Symbol("colina", x, y, scale, Fill(world.Biomes[i], "#FFFFFF", 0.25)));
            }
        }

        // Árboles: uno o dos por celda de bosque, corridos un poco al azar.
        for (var i = 0; i < mesh.Count; i++)
        {
            if (!Usable(i)) continue;
            var biome = world.Biomes[i];
            var (id, count) = biome switch
            {
                Biome.Taiga => ("pino", 2),
                Biome.TemperateForest or Biome.TemperateRainforest or Biome.TropicalForest => ("arbol", 1),
                Biome.Jungle => ("arbol", 2),
                _ => ("", 0)
            };
            for (var k = 0; k < count; k++)
            {
                var x = mesh.X[i] + rng.Range(-0.4, 0.4) * mesh.Spacing;
                var y = mesh.Y[i] + rng.Range(-0.4, 0.4) * mesh.Spacing;
                var scale = rng.Range(1.1, 1.45) * mesh.Spacing / 18; // a 1 se leían como puntos
                if (rng.NextDouble() > 0.8 || !Free(x, y, 3 * scale)) continue;
                symbols.Add(new Symbol(id, x, y, scale, Fill(biome, "#3B3226", 0.3)));
            }
        }

        // De arriba hacia abajo: lo de más abajo en el mapa se dibuja último y queda adelante.
        svg.Append("<g clip-path=\"url(#tierra)\">");
        foreach (var s in symbols.OrderBy(s => s.Y))
        {
            svg.Append(CultureInfo.InvariantCulture,
                $"<use href=\"#{s.Id}\" fill=\"{s.Fill}\" transform=\"translate({s.X:0.#} {s.Y:0.#}) scale({s.Scale:0.##})\"/>");
        }
        svg.Append("</g>");
    }

    // ------------------------------------------------------------- ayudas

    private static (double X, double Y, double Z) Normalize((double X, double Y, double Z) v)
    {
        var length = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        return (v.X / length, v.Y / length, v.Z / length);
    }
}
