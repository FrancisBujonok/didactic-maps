namespace MapaMundos.Generation;

/// <summary>
/// Inventa nombres de lugares uniendo sílabas: un comienzo, a veces una sílaba del medio, y un
/// final. Las listas están elegidas para que los nombres suenen a fantasía y se lean bien en
/// español. Con la misma semilla salen los mismos nombres, y nunca se repite uno en el mismo mapa.
/// </summary>
public sealed class NameGenerator
{
    // Comienzos: consonante (o grupo) más vocal.
    private static readonly string[] Starts =
    {
        "Ba", "Bra", "Ca", "Cor", "Da", "Dra", "E", "Fa", "Ga", "Gre", "Ha", "I", "Ka", "La", "Lo",
        "Ma", "Mor", "Na", "O", "Pa", "Ra", "Ro", "Sa", "Se", "Ta", "Tor", "U", "Va", "Ve", "Za"
    };

    // Sílabas del medio, para nombres más largos.
    private static readonly string[] Middles =
    {
        "la", "ra", "ne", "mi", "do", "ri", "sa", "ven", "lor", "ta", "ga", "mon", "re", "li"
    };

    // Finales de ciudades y lugares.
    private static readonly string[] PlaceEnds =
    {
        "dor", "mar", "ria", "lon", "gar", "nia", "vel", "ros", "thal", "dal", "mir", "bran",
        "sur", "ven", "lis", "cor", "nar", "len"
    };

    // Finales de reinos: suenan a tierra o región ("Valdoria", "Morenor"). Empiezan con
    // consonante para no chocar con la vocal del comienzo ("Pa" + "ia" daría "Paia").
    private static readonly string[] KingdomEnds = { "ria", "nia", "dor", "mor", "thia", "gard", "lia", "vania", "lor" };

    private readonly Rng _rng;
    private readonly HashSet<string> _used = new();

    public NameGenerator(int seed) => _rng = new Rng(seed);

    /// <summary>Nombre de ciudad, aldea o lugar.</summary>
    public string Place() => Unique(PlaceEnds);

    /// <summary>Nombre de reino.</summary>
    public string Kingdom() => Unique(KingdomEnds);

    private string Unique(string[] ends)
    {
        // Pocos intentos alcanzan: hay miles de combinaciones y un mapa usa unas decenas.
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var name = Pick(Starts) + (_rng.NextDouble() < 0.45 ? Pick(Middles) : "") + Pick(ends);
            if (Pronounceable(name) && _used.Add(name)) return name;
        }

        // Muy improbable: si se agotaran los intentos, se numera para no repetir.
        var fallback = Pick(Starts) + Pick(ends) + " " + (_used.Count + 1);
        _used.Add(fallback);
        return fallback;
    }

    private string Pick(string[] options) => options[_rng.NextInt(options.Length)];

    // Descarta nombres demasiado cortos o largos, y los que repiten la misma letra seguida
    // ("Kaand", "Pariia"), que en las pruebas salían raros de leer.
    private static bool Pronounceable(string name)
    {
        if (name.Length is < 4 or > 9) return false;
        var lower = name.ToLowerInvariant();
        for (var i = 1; i < lower.Length; i++)
        {
            if (lower[i] == lower[i - 1]) return false;
        }
        return true;
    }
}
