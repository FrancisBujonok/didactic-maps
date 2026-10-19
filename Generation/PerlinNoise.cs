namespace MapaMundos.Generation;

/// <summary>
/// Ruido de Perlin en 2D: una función que da valores entre -1 y 1 que cambian suavemente.
/// Dos puntos cercanos tienen valores parecidos; dos puntos lejanos, independientes.
///
/// Cómo funciona: el plano se divide en una grilla de cuadrados de lado 1. Cada esquina recibe
/// una dirección "al azar" (un gradiente). Para un punto dentro de un cuadrado se mira cuánto
/// apunta cada gradiente hacia el punto y se mezclan las cuatro esquinas con una curva suave.
/// </summary>
public sealed class PerlinNoise
{
    // Tabla de permutación: los números 0 a 255 mezclados según la semilla, repetidos dos
    // veces para no tener que calcular "módulo 256" al buscar esquinas vecinas.
    private readonly int[] _perm = new int[512];

    // Ocho direcciones posibles para los gradientes (ejes y diagonales).
    private static readonly (double X, double Y)[] Gradients =
    {
        (1, 0), (-1, 0), (0, 1), (0, -1),
        (0.7071, 0.7071), (-0.7071, 0.7071), (0.7071, -0.7071), (-0.7071, -0.7071)
    };

    public PerlinNoise(int seed)
    {
        var rng = new Rng(seed);
        var p = new int[256];
        for (var i = 0; i < 256; i++) p[i] = i;

        // Mezcla de Fisher-Yates: recorre de atrás hacia adelante intercambiando con una
        // posición al azar anterior. Cada orden posible tiene la misma probabilidad.
        for (var i = 255; i > 0; i--)
        {
            var j = rng.NextInt(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }

        for (var i = 0; i < 512; i++) _perm[i] = p[i & 255];
    }

    /// <summary>Valor del ruido en (x, y), aproximadamente entre -1 y 1.</summary>
    public double Sample(double x, double y)
    {
        // Cuadrado de la grilla que contiene al punto y posición dentro de él (0 a 1).
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var fx = x - x0;
        var fy = y - y0;
        var xi = x0 & 255;
        var yi = y0 & 255;

        // Aporte de cada esquina: producto entre su gradiente y el vector esquina -> punto.
        var n00 = Corner(xi, yi, fx, fy);
        var n10 = Corner(xi + 1, yi, fx - 1, fy);
        var n01 = Corner(xi, yi + 1, fx, fy - 1);
        var n11 = Corner(xi + 1, yi + 1, fx - 1, fy - 1);

        // Mezcla con una curva en S para que no se noten los bordes de los cuadrados.
        var u = Fade(fx);
        var v = Fade(fy);
        var top = Lerp(n00, n10, u);
        var bottom = Lerp(n01, n11, u);
        return Lerp(top, bottom, v) * 1.414; // escala para acercarse al rango -1 a 1
    }

    /// <summary>
    /// Ruido fractal (fBm): suma varias "octavas", cada una con el doble de detalle y la mitad
    /// de fuerza. La primera da la forma grande; las siguientes, las irregularidades de la costa.
    /// </summary>
    public double Fractal(double x, double y, int octaves)
    {
        double sum = 0, amplitude = 1, frequency = 1, total = 0;
        for (var i = 0; i < octaves; i++)
        {
            sum += Sample(x * frequency, y * frequency) * amplitude;
            total += amplitude;
            amplitude *= 0.5;
            frequency *= 2;
        }
        return sum / total;
    }

    private double Corner(int xi, int yi, double dx, double dy)
    {
        var g = Gradients[_perm[_perm[xi & 255] + (yi & 255)] & 7];
        return g.X * dx + g.Y * dy;
    }

    // 6t^5 - 15t^4 + 10t^3: empieza y termina plana, así no quedan quiebres entre cuadrados.
    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
}
