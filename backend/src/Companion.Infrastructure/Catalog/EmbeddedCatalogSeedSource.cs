using Companion.Application.Catalog;

namespace Companion.Infrastructure.Catalog;

/// <summary>Catalog seeds embedded in this assembly (Persistence/Seed), written by scripts/import-catalogs.</summary>
public sealed class EmbeddedCatalogSeedSource(string key, CatalogKind kind, string fileName) : ICatalogSeedSource
{
    public static EmbeddedCatalogSeedSource Spells() => new("spells.magias5e", CatalogKind.Spells, "spells.magias5e.json");
    public static EmbeddedCatalogSeedSource Items() => new("items.srd52", CatalogKind.Items, "items.srd52.json");

    public string Key { get; } = key;
    public CatalogKind Kind { get; } = kind;

    public byte[]? Read()
    {
        using var stream = typeof(EmbeddedCatalogSeedSource).Assembly
            .GetManifestResourceStream($"Companion.Infrastructure.Persistence.Seed.{fileName}");
        if (stream is null) return null;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
