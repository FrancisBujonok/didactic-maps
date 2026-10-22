using MapaMundos.Models;

namespace MapaMundos.Generation;

/// <summary>
/// Deformación del plano para que las costas tengan detalle fino ("domain warping").
///
/// La costa sale de los lados de las celdas, así que no tiene nada más chico que una celda: se
/// ve lisa. En vez de mover cada punto de la costa por su cuenta (dos orillas de un estrecho
/// podrían cruzarse), se corre CADA punto del plano un poco, según dos campos de ruido (uno para
/// x y otro para y). La deformación es continua: dos puntos cercanos se mueven parecido, así que
/// dos orillas vecinas se deforman juntas y no se cruzan. Se aplica a todo lo que se dibuja con
/// líneas (costa, orillas de lagos, ríos), para que las desembocaduras sigan en la costa.
///
/// Solo cambia el dibujo: el mundo (celdas, alturas, tierra) es el mismo.
/// </summary>
internal sealed class CoastWarp
{
    private readonly PerlinNoise _noiseX = new(101);
    private readonly PerlinNoise _noiseY = new(202);
    private readonly double _amplitude;   // cuánto se puede correr un punto, en píxeles
    private readonly double _frequency;   // ondas por píxel de la capa más grande
    private readonly double _persistence; // fuerza de cada capa respecto de la anterior
    private readonly int _octaves;

    private CoastWarp(double amplitude, double wavelength, double persistence, int octaves)
    {
        _amplitude = amplitude;
        _frequency = 1 / wavelength;
        _persistence = persistence;
        _octaves = octaves;
    }

    /// <summary>
    /// Deformación para el nivel de detalle elegido, medida en celdas (spacing) para que se vea
    /// igual con cualquier detalle de malla. Suaves: ninguna (null).
    /// </summary>
    public static CoastWarp? For(CoastDetail detail, double spacing) => detail switch
    {
        CoastDetail.Irregular => new CoastWarp(spacing * 0.9, spacing * 6, 0.62, 6),
        CoastDetail.Rugged => new CoastWarp(spacing * 1.5, spacing * 8, 0.66, 7),
        _ => null
    };

    public (double X, double Y) Apply((double X, double Y) p) =>
        (p.X + _amplitude * Fractal(_noiseX, p.X, p.Y), p.Y + _amplitude * Fractal(_noiseY, p.X, p.Y));

    public List<(double X, double Y)> Apply(List<(double X, double Y)> points) => points.Select(Apply).ToList();

    /// <summary>
    /// Ruido fractal como PerlinNoise.Fractal, pero con la fuerza de cada capa ajustable. Con la
    /// mitad (lo de Fractal, 0.5) las ondas chicas quedan por debajo de un píxel y la costa se
    /// sigue viendo lisa; con 0.7 las capas finas pesan más y aparece el "serrucho" de una costa
    /// real. Resultado entre -1 y 1, aproximadamente.
    /// </summary>
    private double Fractal(PerlinNoise noise, double x, double y)
    {
        double sum = 0, total = 0, strength = 1, frequency = _frequency;
        for (var i = 0; i < _octaves; i++)
        {
            sum += noise.Sample(x * frequency, y * frequency) * strength;
            total += strength;
            strength *= _persistence;
            frequency *= 2;
        }
        return sum / total;
    }
}
