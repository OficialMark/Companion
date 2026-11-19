namespace Companion.Domain.Models;

public sealed class Battlemap
{
    public const int MinGridSize = 1;
    public const int MaxGridSize = 100;
    public const int MaxImageBytes = 20 * 1024 * 1024;

    private Battlemap() { }

    public Battlemap(
        Guid id,
        Guid encounterId,
        byte[] imageContent,
        string imageMediaType,
        string imageFileName,
        int columns,
        int rows,
        bool isGridVisible,
        DateTimeOffset createdAt)
    {
        Id = id;
        EncounterId = encounterId;
        CreatedAt = createdAt;
        ApplyImage(imageContent, imageMediaType, imageFileName, columns, rows, isGridVisible, createdAt);
    }

    public Guid Id { get; private set; }
    public Guid EncounterId { get; private set; }
    public byte[] ImageContent { get; private set; } = [];
    public string ImageMediaType { get; private set; } = string.Empty;
    public string ImageFileName { get; private set; } = string.Empty;
    public Guid ImageRevision { get; private set; }
    public int Columns { get; private set; }
    public int Rows { get; private set; }
    public bool IsGridVisible { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public byte[] Version { get; private set; } = [];

    /// <summary>
    /// Fog of war as a row-major bitset (cell index = row * Columns + column; bit i % 8
    /// of byte i / 8). Null means the whole map is revealed.
    /// </summary>
    public byte[]? FogMask { get; private set; }

    public static int FogMaskLengthFor(int columns, int rows) => (columns * rows + 7) / 8;
    public int FogMaskLength => FogMaskLengthFor(Columns, Rows);
    public const int MaxFogMaskBytes = (MaxGridSize * MaxGridSize + 7) / 8;

    public void SetFog(byte[]? mask)
    {
        if (mask is null) { FogMask = null; return; }
        if (mask.Length != FogMaskLength)
            throw new ArgumentException(
                $"A névoa deve ter {FogMaskLength} bytes para um grid de {Columns}x{Rows}.", nameof(mask));
        var copy = mask.ToArray();
        // Bits past the last cell are padding and must never count as fog.
        var cells = Columns * Rows;
        var tailBits = cells % 8;
        if (tailBits != 0) copy[^1] &= (byte)((1 << tailBits) - 1);
        FogMask = copy.All(b => b == 0) ? null : copy;
    }

    public bool IsCellFogged(int column, int row)
    {
        if (FogMask is null || !Contains(column, row)) return false;
        var index = row * Columns + column;
        return (FogMask[index / 8] & (1 << (index % 8))) != 0;
    }

    /// <summary>
    /// True when every in-grid cell of the footprint anchored at (column,row) is fogged.
    /// A token partly in revealed cells stays visible.
    /// </summary>
    public bool IsAreaFogged(int column, int row, int footprintSize)
    {
        if (FogMask is null) return false;
        var span = Math.Max(1, footprintSize);
        var any = false;
        for (var r = row; r < row + span; r++)
        for (var c = column; c < column + span; c++)
        {
            if (!Contains(c, r)) continue;
            any = true;
            if (!IsCellFogged(c, r)) return false;
        }
        return any;
    }

    public void Replace(
        byte[] imageContent,
        string imageMediaType,
        string imageFileName,
        int columns,
        int rows,
        bool isGridVisible,
        DateTimeOffset updatedAt) =>
        ApplyImage(imageContent, imageMediaType, imageFileName, columns, rows, isGridVisible, updatedAt);

    public void ValidateCoordinate(int column, int row)
    {
        if (column < 0 || column >= Columns)
            throw new ArgumentOutOfRangeException(nameof(column), "A coluna está fora do grid.");
        if (row < 0 || row >= Rows)
            throw new ArgumentOutOfRangeException(nameof(row), "A linha está fora do grid.");
    }

    public bool Contains(int column, int row) =>
        column >= 0 && column < Columns && row >= 0 && row < Rows;

    private void ApplyImage(
        byte[] imageContent,
        string imageMediaType,
        string imageFileName,
        int columns,
        int rows,
        bool isGridVisible,
        DateTimeOffset updatedAt)
    {
        if (imageContent.Length is < 1 or > MaxImageBytes)
            throw new ArgumentOutOfRangeException(nameof(imageContent), "A imagem deve ter no máximo 20 MB.");
        if (columns is < MinGridSize or > MaxGridSize)
            throw new ArgumentOutOfRangeException(nameof(columns), "As colunas devem estar entre 1 e 100.");
        if (rows is < MinGridSize or > MaxGridSize)
            throw new ArgumentOutOfRangeException(nameof(rows), "As linhas devem estar entre 1 e 100.");
        if (string.IsNullOrWhiteSpace(imageMediaType))
            throw new ArgumentException("O tipo da imagem é obrigatório.", nameof(imageMediaType));

        var safeName = imageFileName.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim() ?? string.Empty;
        if (safeName.Length == 0) safeName = "battlemap";
        if (safeName.Length > 255) safeName = safeName[..255];

        // The fog bitset is indexed by grid shape; a different grid invalidates it.
        if (columns != Columns || rows != Rows) FogMask = null;
        ImageContent = imageContent.ToArray();
        ImageMediaType = imageMediaType;
        ImageFileName = safeName;
        Columns = columns;
        Rows = rows;
        IsGridVisible = isGridVisible;
        ImageRevision = Guid.NewGuid();
        UpdatedAt = updatedAt;
    }
}
