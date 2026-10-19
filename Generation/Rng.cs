namespace MapaMundos.Generation;

/// <summary>
/// Generador de números pseudoaleatorios con semilla (algoritmo Mulberry32).
///
/// Por qué no System.Random: .NET no garantiza que Random(semilla) devuelva la misma secuencia
/// en todas sus versiones. Acá la secuencia depende solo de estas líneas, así que la misma
/// semilla da siempre el mismo mapa, hoy y dentro de cinco años.
/// </summary>
public sealed class Rng
{
    private uint _state;

    public Rng(int seed) => _state = (uint)seed;

    /// <summary>Entero sin signo de 32 bits. Todo lo demás se construye a partir de esto.</summary>
    public uint NextUInt()
    {
        // Mulberry32: suma una constante al estado y "mezcla" sus bits con desplazamientos
        // y multiplicaciones. Los uint desbordan en silencio (es lo que se busca).
        _state += 0x6D2B79F5;
        uint z = _state;
        z = (z ^ (z >> 15)) * (z | 1);
        z ^= z + (z ^ (z >> 7)) * (z | 61);
        return z ^ (z >> 14);
    }

    /// <summary>Número decimal entre 0 (incluido) y 1 (excluido).</summary>
    public double NextDouble() => NextUInt() / 4294967296.0;

    /// <summary>Número decimal entre min y max.</summary>
    public double Range(double min, double max) => min + (max - min) * NextDouble();

    /// <summary>Entero entre 0 (incluido) y max (excluido).</summary>
    public int NextInt(int max) => (int)(NextDouble() * max);
}
