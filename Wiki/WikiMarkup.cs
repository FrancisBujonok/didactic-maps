using System.Text;

namespace MapaMundos.Wiki;

public enum WikiBlockKind
{
    Heading1,   // # Título
    Heading2,   // ## Título
    Heading3,   // ### Título
    Paragraph,  // líneas seguidas de texto
    List,       // - punto (cada línea es un punto)
    Quote       // > cita
}

/// <summary>
/// Un trozo de texto con su formato. Si Link no es null es un enlace: Text es lo que se ve y
/// Link el nombre de la ficha a la que apunta.
/// </summary>
public sealed record WikiInline(string Text, bool Bold = false, bool Italic = false, string? Link = null);

/// <summary>
/// Un bloque del texto (título, párrafo, lista o cita). Todos tienen líneas, y cada línea es una
/// lista de trozos: en un párrafo o una cita las líneas van separadas por saltos de línea, en una
/// lista cada línea es un punto, y un título tiene una sola.
/// </summary>
public sealed record WikiBlock(WikiBlockKind Kind, IReadOnlyList<IReadOnlyList<WikiInline>> Lines);

/// <summary>
/// Traductor del formato simple de las fichas, un pedacito de Markdown:
///   # Título, ## Subtítulo, ### Sección
///   **negrita**, *cursiva*
///   - punto de una lista
///   > cita
///   [[Nombre de otra ficha]] o [[Nombre|texto que se ve]]
///
/// No arma HTML: devuelve bloques y trozos, y el componente WikiText los dibuja con Razor. Así
/// todo lo que escribe el autor se muestra como texto y nunca se ejecuta como código de la página.
/// </summary>
public static class WikiMarkup
{
    public static List<WikiBlock> Parse(string? text)
    {
        var blocks = new List<WikiBlock>();
        if (string.IsNullOrWhiteSpace(text)) return blocks;

        // Bloque en armado: las líneas seguidas del mismo tipo (párrafo, lista, cita) se juntan.
        WikiBlockKind? kind = null;
        var lines = new List<IReadOnlyList<WikiInline>>();

        void Close()
        {
            if (kind is { } k && lines.Count > 0)
                blocks.Add(new WikiBlock(k, lines.ToList()));
            kind = null;
            lines.Clear();
        }

        void Add(WikiBlockKind k, string content)
        {
            if (kind != k) Close();
            kind = k;
            lines.Add(ParseLine(content));
        }

        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();

            if (line.Length == 0)
            {
                Close(); // una línea en blanco separa bloques
            }
            else if (line.StartsWith("### "))
            {
                Close();
                Add(WikiBlockKind.Heading3, line[4..]);
                Close(); // un título es siempre un bloque de una línea
            }
            else if (line.StartsWith("## "))
            {
                Close();
                Add(WikiBlockKind.Heading2, line[3..]);
                Close();
            }
            else if (line.StartsWith("# "))
            {
                Close();
                Add(WikiBlockKind.Heading1, line[2..]);
                Close();
            }
            else if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                Add(WikiBlockKind.List, line[2..]);
            }
            else if (line.StartsWith('>'))
            {
                Add(WikiBlockKind.Quote, line[1..].TrimStart());
            }
            else
            {
                Add(WikiBlockKind.Paragraph, line);
            }
        }

        Close();
        return blocks;
    }

    /// <summary>
    /// Separa una línea en trozos con su formato. Recorre la línea letra por letra con dos
    /// "interruptores" (negrita y cursiva) que se prenden y se apagan con ** y *. Un signo sin
    /// pareja más adelante en la línea se deja como texto: "5 * 3" no abre una cursiva eterna.
    /// </summary>
    public static List<WikiInline> ParseLine(string line)
    {
        var result = new List<WikiInline>();
        var buffer = new StringBuilder();
        var bold = false;
        var italic = false;

        // Lo juntado hasta ahora pasa a ser un trozo con el formato actual.
        void Flush()
        {
            if (buffer.Length == 0) return;
            result.Add(new WikiInline(buffer.ToString(), bold, italic));
            buffer.Clear();
        }

        var i = 0;
        while (i < line.Length)
        {
            // Enlace: [[Nombre]] o [[Nombre|texto]].
            if (line.AsSpan(i).StartsWith("[[") && line.IndexOf("]]", i + 2, StringComparison.Ordinal) is var end and > 0)
            {
                var inner = line[(i + 2)..end];
                var bar = inner.IndexOf('|');
                var target = (bar >= 0 ? inner[..bar] : inner).Trim();
                var shown = (bar >= 0 ? inner[(bar + 1)..] : inner).Trim();
                if (target.Length > 0)
                {
                    Flush();
                    result.Add(new WikiInline(shown.Length > 0 ? shown : target, bold, italic, target));
                    i = end + 2;
                    continue;
                }
            }

            // Negrita: ** se apaga si va pegado a la palabra de antes ("**así**"), y se prende si
            // va pegado a la palabra de después y hay un cierre más adelante.
            if (line.AsSpan(i).StartsWith("**"))
            {
                var closes = bold && i > 0 && !char.IsWhiteSpace(line[i - 1]);
                var opens = !bold && WordAfter(line, i + 2) && FindClose(line, i + 2, "**") >= 0;
                if (closes || opens)
                {
                    Flush();
                    bold = !bold;
                    i += 2;
                    continue;
                }
            }

            // Cursiva: lo mismo con un * solo (no uno de los dos de **).
            if (IsSingleStar(line, i))
            {
                var closes = italic && i > 0 && !char.IsWhiteSpace(line[i - 1]);
                var opens = !italic && WordAfter(line, i + 1) && FindClose(line, i + 1, "*") >= 0;
                if (closes || opens)
                {
                    Flush();
                    italic = !italic;
                    i++;
                    continue;
                }
            }

            buffer.Append(line[i]);
            i++;
        }

        Flush();
        return result;
    }

    // True si en "index" empieza una palabra (hay una letra y no un espacio).
    private static bool WordAfter(string line, int index) => index < line.Length && !char.IsWhiteSpace(line[index]);

    // True si en "index" hay un * suelto: ni el anterior ni el siguiente son *.
    private static bool IsSingleStar(string line, int index) =>
        line[index] == '*' &&
        (index == 0 || line[index - 1] != '*') &&
        (index + 1 >= line.Length || line[index + 1] != '*');

    /// <summary>
    /// Busca desde "from" un signo de cierre ("**" o "*") pegado a la palabra de antes. Devuelve
    /// su posición o -1. Para "*" solo cuentan los sueltos: el de "**" es de la negrita.
    /// </summary>
    private static int FindClose(string line, int from, string mark)
    {
        for (var i = from; i < line.Length; i++)
        {
            var found = mark == "**" ? line.AsSpan(i).StartsWith("**") : IsSingleStar(line, i);
            if (found && i > from && !char.IsWhiteSpace(line[i - 1])) return i;
        }
        return -1;
    }

    /// <summary>Nombres a los que enlaza un texto, en orden y sin repetir.</summary>
    public static List<string> LinksIn(string? text) =>
        Parse(text)
            .SelectMany(block => block.Lines)
            .SelectMany(line => line)
            .Where(inline => inline.Link is not null)
            .Select(inline => inline.Link!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
