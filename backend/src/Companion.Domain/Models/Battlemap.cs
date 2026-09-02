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
