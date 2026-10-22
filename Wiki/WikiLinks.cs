using MapaMundos.Models;

namespace MapaMundos.Wiki;

public enum WikiTargetKind { Pin, Region, Entry }

/// <summary>
/// Algo que puede tener ficha: un pin, un reino o una ficha suelta (Entry, con el Id de la
/// ficha). Es un "record struct": dos con el mismo tipo e Id son iguales, sin escribir Equals.
/// </summary>
public readonly record struct WikiTarget(WikiTargetKind Kind, Guid Id)
{
    public static WikiTarget Pin(Guid id) => new(WikiTargetKind.Pin, id);
    public static WikiTarget Region(Guid id) => new(WikiTargetKind.Region, id);
    public static WikiTarget Entry(Guid id) => new(WikiTargetKind.Entry, id);
}

/// <summary>
/// Índice de nombres del mundo: a qué apunta un [[enlace]] y qué fichas mencionan a otra.
/// Son funciones puras sobre el proyecto (no cambian nada), así se pueden probar sin la app.
/// </summary>
public static class WikiLinks
{
    /// <summary>La ficha de un pin, de un reino o suelta, o null si todavía no tiene.</summary>
    public static WikiEntry? EntryOf(MapProject project, WikiTarget target) => target.Kind switch
    {
        WikiTargetKind.Pin => project.Entries.FirstOrDefault(e => e.PinId == target.Id),
        WikiTargetKind.Region => project.Entries.FirstOrDefault(e => e.RegionId == target.Id),
        _ => project.Entries.FirstOrDefault(e => e.Id == target.Id && e.PinId is null && e.RegionId is null)
    };

    /// <summary>A qué apunta una ficha: su pin, su reino o ella misma si es suelta.</summary>
    public static WikiTarget TargetOf(WikiEntry entry) =>
        entry.PinId is { } pin ? WikiTarget.Pin(pin)
        : entry.RegionId is { } region ? WikiTarget.Region(region)
        : WikiTarget.Entry(entry.Id);

    /// <summary>True si el pin, el reino o la ficha todavía existen.</summary>
    public static bool Exists(MapProject project, WikiTarget target) => target.Kind switch
    {
        WikiTargetKind.Pin => project.Pins.Any(p => p.Id == target.Id),
        WikiTargetKind.Region => project.Regions.Any(r => r.Id == target.Id),
        _ => EntryOf(project, target) is not null
    };

    /// <summary>Nombre que se muestra: el del pin, el del reino o el título de la ficha suelta.</summary>
    public static string NameOf(MapProject project, WikiTarget target) => target.Kind switch
    {
        WikiTargetKind.Pin => project.Pins.FirstOrDefault(p => p.Id == target.Id)?.Title ?? "",
        WikiTargetKind.Region => project.Regions.FirstOrDefault(r => r.Id == target.Id)?.Name ?? "",
        _ => EntryOf(project, target)?.Title ?? ""
    };

    /// <summary>
    /// A qué apunta [[nombre]]: sin distinguir mayúsculas y sin espacios de más. Si hay nombres
    /// repetidos, gana en este orden: ficha suelta, reino, pin (un personaje "Aldara" le gana a
    /// una aldea con el mismo nombre). Null si no existe: el enlace se muestra como "sin ficha".
    /// </summary>
    public static WikiTarget? Resolve(MapProject project, string name)
    {
        name = name.Trim();
        bool Same(string other) => string.Equals(other.Trim(), name, StringComparison.OrdinalIgnoreCase);

        if (project.Entries.FirstOrDefault(e => e.PinId is null && e.RegionId is null && Same(e.Title)) is { } entry)
            return WikiTarget.Entry(entry.Id);
        if (project.Regions.FirstOrDefault(r => Same(r.Name)) is { } region)
            return WikiTarget.Region(region.Id);
        if (project.Pins.FirstOrDefault(p => Same(p.Title)) is { } pin)
            return WikiTarget.Pin(pin.Id);
        return null;
    }

    /// <summary>
    /// Fichas que enlazan a esta ("Mencionada en"). No se guardan: se calculan leyendo los
    /// enlaces de cada ficha, así nunca quedan desactualizadas cuando se edita un texto.
    /// </summary>
    public static List<WikiTarget> MentionsOf(MapProject project, WikiTarget target)
    {
        var result = new List<WikiTarget>();
        foreach (var entry in project.Entries)
        {
            var source = TargetOf(entry);
            if (source == target || !Exists(project, source)) continue;

            if (WikiMarkup.LinksIn(entry.Text).Any(name => Resolve(project, name) == target))
                result.Add(source);
        }
        return result;
    }
}
