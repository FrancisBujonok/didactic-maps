using MapaMundos.Wiki;

namespace MapaMundos.Tests;

/// <summary>El traductor del formato simple de las fichas (Wiki/WikiMarkup.cs).</summary>
public class WikiMarkupTests
{
    [Fact]
    public void EmptyText_HasNoBlocks()
    {
        Assert.Empty(WikiMarkup.Parse(""));
        Assert.Empty(WikiMarkup.Parse("   \n  \n"));
    }

    [Fact]
    public void Blocks_AreSeparatedByBlankLinesAndByKind()
    {
        var text = "# Valdoria\nUn reino del norte.\nFrío y montañoso.\n\n- Capital: Orm\n- Río: Ser\n> Nadie cruza el paso en invierno.";

        var blocks = WikiMarkup.Parse(text);

        Assert.Equal(
            new[] { WikiBlockKind.Heading1, WikiBlockKind.Paragraph, WikiBlockKind.List, WikiBlockKind.Quote },
            blocks.Select(b => b.Kind));
        Assert.Equal(2, blocks[1].Lines.Count); // las dos líneas del párrafo quedan juntas
        Assert.Equal(2, blocks[2].Lines.Count); // dos puntos de la lista
    }

    [Theory]
    [InlineData("# A", WikiBlockKind.Heading1)]
    [InlineData("## A", WikiBlockKind.Heading2)]
    [InlineData("### A", WikiBlockKind.Heading3)]
    [InlineData("#A", WikiBlockKind.Paragraph)] // sin espacio no es título
    [InlineData("* A", WikiBlockKind.List)]
    public void LineStart_DecidesTheKind(string line, WikiBlockKind expected)
    {
        Assert.Equal(expected, WikiMarkup.Parse(line).Single().Kind);
    }

    [Fact]
    public void BoldAndItalic()
    {
        var line = WikiMarkup.ParseLine("Un **gran** río y un *viejo* puente");

        Assert.Equal(
            new[]
            {
                new WikiInline("Un "),
                new WikiInline("gran", Bold: true),
                new WikiInline(" río y un "),
                new WikiInline("viejo", Italic: true),
                new WikiInline(" puente")
            },
            line);
    }

    [Fact]
    public void ItalicInsideBold()
    {
        var line = WikiMarkup.ParseLine("**el *último* rey**");

        Assert.Equal(
            new[]
            {
                new WikiInline("el ", Bold: true),
                new WikiInline("último", Bold: true, Italic: true),
                new WikiInline(" rey", Bold: true)
            },
            line);
    }

    [Fact]
    public void MarkWithoutPair_StaysAsText()
    {
        var line = WikiMarkup.ParseLine("5 * 3 es **mucho");

        Assert.Equal(new[] { new WikiInline("5 * 3 es **mucho") }, line);
    }

    [Fact]
    public void Links_WithAndWithoutShownText()
    {
        var line = WikiMarkup.ParseLine("Nació en [[Orm]] y sirvió a [[Valdoria|la corona]].");

        Assert.Equal(
            new[]
            {
                new WikiInline("Nació en "),
                new WikiInline("Orm", Link: "Orm"),
                new WikiInline(" y sirvió a "),
                new WikiInline("la corona", Link: "Valdoria"),
                new WikiInline(".")
            },
            line);
    }

    [Fact]
    public void LinkInsideBold_KeepsTheFormat()
    {
        var line = WikiMarkup.ParseLine("**[[Orm]]**");

        Assert.Equal(new[] { new WikiInline("Orm", Bold: true, Link: "Orm") }, line);
    }

    [Theory]
    [InlineData("[[]]")]        // enlace vacío
    [InlineData("[[Orm")]       // sin cierre
    [InlineData("[ [Orm] ]")]   // corchetes separados
    public void BrokenLinks_StayAsText(string text)
    {
        Assert.All(WikiMarkup.ParseLine(text), inline => Assert.Null(inline.Link));
    }

    [Fact]
    public void LinksIn_ListsEachNameOnce()
    {
        var text = "[[Orm]] y [[Ser]].\n\n- [[orm]] otra vez\n- [[Valdoria|el reino]]";

        Assert.Equal(new[] { "Orm", "Ser", "Valdoria" }, WikiMarkup.LinksIn(text));
    }
}
