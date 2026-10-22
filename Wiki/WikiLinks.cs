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
    /// Cambia los enlaces a oldName ([[viejo]] y [[viejo|texto]]) por newName, sin tocar nada
    /// más: el texto propio de un enlace se conserva. Compara como Resolve (sin distinguir
    /// mayúsculas ni espacios de más). En count devuelve cuántos enlaces cambió.
    ///
    /// Recorre el texto buscando "[[" y el "]]" siguiente con las mismas reglas que WikiMarkup:
    /// un enlace no cruza de una línea a otra y su nombre no puede estar vacío.
    /// </summary>
    public static string RenameLinks(string? text, string oldName, string newName, out int count)
    {
        count = 0;
        if (string.IsNullOrEmpty(text)) return text ?? "";

        oldName = oldName.Trim();
        newName = newName.Trim();
        var result = new System.Text.StringBuilder();
        var i = 0; // hasta dónde ya se copió el texto al resultado

        while (true)
        {
            var start = text.IndexOf("[[", i, StringComparison.Ordinal);
            var end = start < 0 ? -1 : text.IndexOf("]]", start + 2, StringComparison.Ordinal);
            if (end < 0) break;

            var inner = text[(start + 2)..end];
            var bar = inner.IndexOf('|');
            var target = (bar >= 0 ? inner[..bar] : inner).Trim();

            if (inner.Contains('\n') || target.Length == 0 ||
                !string.Equals(target, oldName, StringComparison.OrdinalIgnoreCase))
            {
                // No es un enlace a oldName: se copia el primer "[" y se sigue buscando desde ahí.
                result.Append(text, i, start + 1 - i);
                i = start + 1;
                continue;
            }

            result.Append(text, i, start - i)
                  .Append("[[").Append(newName)
                  .Append(bar >= 0 ? inner[bar..] : "") // "|texto propio", tal cual
                  .Append("]]");
            i = end + 2;
            count++;
        }

        result.Append(text, i, text.Length - i);
        return result.ToString();
    }

    /// <summary>Cuántos enlaces del texto apuntan a ese nombre.</summary>
    public static int CountLinksTo(string? text, string name)
    {
        RenameLinks(text, name, name, out var count);
        return count;
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
