namespace MapaMundos.Tests;

/// <summary>Las ayudas geométricas de los dibujos (Models/Sketches.cs).</summary>
public class SketchesTests
{
    // Cuadrado de 10 x 10 con una esquina en el origen.
    private static readonly (double X, double Y)[] Square = { (0, 0), (10, 0), (10, 10), (0, 10) };

    [Fact]
    public void Area_OfSquare_IsSideSquared()
    {
        Assert.Equal(100, Sketches.Area(Square), precision: 9);
    }

    [Fact]
    public void Area_DoesNotDependOnDirection()
    {
        // El cordón de zapato da negativo si se recorre al revés: Area devuelve el valor absoluto.
        Assert.Equal(100, Sketches.Area(Square.Reverse().ToArray()), precision: 9);
    }

    [Theory]
    [InlineData(5, 5, true)]     // centro
    [InlineData(0.1, 9.9, true)] // cerca de una esquina, adentro
    [InlineData(15, 5, false)]   // a la derecha
    [InlineData(-1, 5, false)]   // a la izquierda
    [InlineData(5, 11, false)]   // abajo
    public void Contains_Square(double x, double y, bool expected)
    {
        Assert.Equal(expected, Sketches.Contains(Square, x, y));
    }

    [Fact]
    public void Contains_ConcaveShape_LeavesTheNotchOutside()
    {
        // Una "U": el hueco del medio está afuera aunque quede entre dos brazos.
        var u = new (double X, double Y)[] { (0, 0), (3, 0), (3, 8), (7, 8), (7, 0), (10, 0), (10, 10), (0, 10) };

        Assert.True(Sketches.Contains(u, 1, 5));   // brazo izquierdo
        Assert.False(Sketches.Contains(u, 5, 4));  // hueco
        Assert.True(Sketches.Contains(u, 5, 9));   // base
    }

    [Theory]
    [InlineData(5, 3, 3)]    // sobre el segmento: distancia perpendicular
    [InlineData(-4, 3, 5)]   // antes del extremo A: distancia a A (triángulo 3-4-5)
    [InlineData(14, -3, 5)]  // después del extremo B: distancia a B
    public void DistanceToSegment_UsesTheNearestPoint(double x, double y, double expected)
    {
        // Segmento horizontal de (0, 0) a (10, 0).
        Assert.Equal(expected, Sketches.DistanceToSegment((x, y), (0, 0), (10, 0)), precision: 9);
    }

    [Fact]
    public void Simplify_StraightLine_KeepsOnlyTheEnds()
    {
        var line = Enumerable.Range(0, 50).Select(i => (X: (double)i, Y: 0.0)).ToList();

        var simple = Sketches.Simplify(line, tolerance: 1);

        Assert.Equal(new[] { (0.0, 0.0), (49.0, 0.0) }, simple);
    }

    [Fact]
    public void Simplify_KeepsTheCorner()
    {
        // Una "L": baja 20 y después va 20 a la derecha. La esquina cambia la forma: se queda.
        var down = Enumerable.Range(0, 21).Select(i => (X: 0.0, Y: (double)i));
        var right = Enumerable.Range(1, 20).Select(i => (X: (double)i, Y: 20.0));

        var simple = Sketches.Simplify(down.Concat(right).ToList(), tolerance: 1);

        Assert.Equal(new[] { (0.0, 0.0), (0.0, 20.0), (20.0, 20.0) }, simple);
    }

    [Fact]
    public void Clean_DropsInvalidIslandsAndClampsTheRest()
    {
        var sketch = new List<double[]>
        {
            new[] { 0.1, 0.1, 0.5, 0.1, 0.3, 0.5 },        // válida
            new[] { 0.1, 0.1, 0.5, 0.1 },                  // 2 puntos: no es una isla
            new[] { 0.1, 0.1, 0.5, 0.1, 0.3 },             // cantidad impar de números
            new[] { 0.1, 0.1, 0.5, double.NaN, 0.3, 0.5 }, // número inválido
            new[] { -0.5, 0.1, 1.5, 0.1, 0.3, 0.5 },       // fuera de 0 a 1: se recorta
        };

        var clean = Sketches.Clean(sketch);

        Assert.Equal(2, clean.Count);
        Assert.Equal(new[] { 0.0, 0.1, 1.0, 0.1, 0.3, 0.5 }, clean[1]);
    }
}
