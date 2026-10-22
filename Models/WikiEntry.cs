namespace MapaMundos.Models;

/// <summary>
/// Una ficha del mundo: texto con formato simple y enlaces [[así]]. Puede estar vinculada a un
/// pin (la ficha de un lugar), a un reino, o a nada (personajes, facciones, eventos...).
///
/// Si está vinculada, su nombre es el del pin o el del reino: hay una sola fuente del nombre y
/// no se desincronizan. Title se usa en las fichas sueltas y, en las vinculadas, guarda el último
/// nombre conocido, por si el pin o el reino desaparecen y la ficha pasa a ser suelta.
/// </summary>
public class WikiEntry
{
    /// <summary>Categorías para elegir en las fichas sueltas. Las vinculadas son "Lugar" o "Reino".</summary>
    public static readonly string[] Categories =
    {
        "Personaje", "Facción", "Evento", "Lugar", "Reino", "Criatura", "Religión", "Objeto", "Otro"
    };

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = "Nueva ficha";

    public string Category { get; set; } = Categories[0];

    /// <summary>Texto con formato simple (ver Wiki/WikiMarkup.cs).</summary>
    public string Text { get; set; } = "";

    /// <summary>Pin del que es ficha, o null.</summary>
    public Guid? PinId { get; set; }

    /// <summary>Reino del que es ficha, o null.</summary>
    public Guid? RegionId { get; set; }
}
