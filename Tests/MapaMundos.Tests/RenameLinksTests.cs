using MapaMundos.Wiki;

namespace MapaMundos.Tests;

/// <summary>Cambiar los enlaces al renombrar un lugar, un reino o una ficha (WikiLinks.RenameLinks).</summary>
public class RenameLinksTests
{
    private static string Rename(string text, out int count) =>
        WikiLinks.RenameLinks(text, "Corthal", "Puerto Gris", out count);

    [Fact]
    public void SimpleLink_TakesTheNewName()
    {
        Assert.Equal("Nació en [[Puerto Gris]].", Rename("Nació en [[Corthal]].", out var count));
        Assert.Equal(1, count);
    }

    [Fact]
    public void LinkWithOwnText_KeepsTheText()
    {
        Assert.Equal("Volvió a [[Puerto Gris|su ciudad]].", Rename("Volvió a [[Corthal|su ciudad]].", out _));
    }

    [Fact]
    public void IgnoresCaseAndSpaces_LikeResolve()
    {
        Assert.Equal("[[Puerto Gris]] y [[Puerto Gris]]", Rename("[[corthal]] y [[ CORTHAL ]]", out var count));
        Assert.Equal(2, count);
    }

    [Fact]
    public void SimilarNames_AreNotTouched()
    {
        // "Corthal Viejo" y "Nueva Corthal" son otros lugares, aunque contengan el nombre.
        const string text = "[[Corthal Viejo]], [[Nueva Corthal]] y Corthal sin corchetes.";

        Assert.Equal(text, Rename(text, out var count));
        Assert.Equal(0, count);
    }

    [Fact]
    public void RestOfTheText_StaysTheSame()
    {
        const string text = "# Historia\n**[[Corthal]]** cayó.\n\n- [[Orm]]\n> [[corthal|El puerto]] ardió [[";
        const string expected = "# Historia\n**[[Puerto Gris]]** cayó.\n\n- [[Orm]]\n> [[Puerto Gris|El puerto]] ardió [[";

        Assert.Equal(expected, Rename(text, out var count));
        Assert.Equal(2, count);
    }

    [Fact]
    public void CountLinksTo_CountsWithoutChanging()
    {
        Assert.Equal(2, WikiLinks.CountLinksTo("[[Orm]], [[Corthal]] y [[Corthal|allá]]", "corthal"));
        Assert.Equal(0, WikiLinks.CountLinksTo("", "Corthal"));
    }
}
