namespace Companion.Domain.Campaigns;

public enum CampaignKnowledgeKind { Npc, Map, Clue, Journal, MasterRecord }

public sealed class CampaignKnowledgeEntry
{
    private CampaignKnowledgeEntry() { }
    public CampaignKnowledgeEntry(Guid id, Guid campaignId, CampaignKnowledgeKind kind, string title, string? summary, string? description, Guid authorId, bool isPlayerVisible, DateTimeOffset now)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentException("Tipo de conteúdo inválido.");
        Id = id; CampaignId = campaignId; Kind = kind; CreatedByParticipantId = authorId; CreatedAt = now; UpdatedAt = now;
        Apply(title, summary, description, isPlayerVisible, now);
    }
    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public CampaignKnowledgeKind Kind { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Summary { get; private set; }
    public string? SharedDescription { get; private set; }
    public bool IsPlayerVisible { get; private set; }
    public Guid CreatedByParticipantId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public byte[] Version { get; private set; } = [];
    public byte[]? ImageContent { get; private set; }
    public string? ImageMediaType { get; private set; }
    public string? ImageFileName { get; private set; }
    public byte[]? PdfContent { get; private set; }
    public string? PdfFileName { get; private set; }
    public const int MaxTitleLength = 120;
    public const int MaxSummaryLength = 500;
    public const int MaxDescriptionLength = 12_000;
    public const int MaxImageBytes = 20 * 1024 * 1024;
    public const int MaxPdfBytes = 20 * 1024 * 1024;

    public void SetPdf(byte[]? content, string? fileName, DateTimeOffset now)
    {
        if (Kind is not (CampaignKnowledgeKind.Clue or CampaignKnowledgeKind.Journal or CampaignKnowledgeKind.MasterRecord))
            throw new ArgumentException("Somente pistas, diário e registros do mestre aceitam PDF.");
        if (content is not null && (content.Length > MaxPdfBytes || !content.AsSpan().StartsWith("%PDF-"u8)))
            throw new ArgumentException("Selecione um PDF válido de até 20 MB.");

        if (content is null)
            PdfFileName = null;
        else
        {
            var name = Path.GetFileNameWithoutExtension((fileName ?? "").Replace('\\', '/'));
            name = string.Concat(name.Where(c => !char.IsControl(c))).Trim();
            PdfFileName = (name.Length == 0 ? "anexo" : name[..Math.Min(name.Length, 251)]) + ".pdf";
        }
        PdfContent = content;
        UpdatedAt = now;
    }

    public void Apply(string? title, string? summary, string? description, bool isPlayerVisible, DateTimeOffset now)
    {
        Title = Required(title, MaxTitleLength, "O título");
        Summary = Optional(summary, MaxSummaryLength, "O resumo");
        SharedDescription = Optional(description, MaxDescriptionLength, "A descrição");
        if (Kind == CampaignKnowledgeKind.Journal && !isPlayerVisible) throw new ArgumentException("Entradas do diário são compartilhadas com a campanha.");
        IsPlayerVisible = Kind switch
        {
            CampaignKnowledgeKind.Journal => true,
            CampaignKnowledgeKind.MasterRecord => false,
            _ => isPlayerVisible
        };
        UpdatedAt = now;
    }
    public void SetImage(byte[] content, string mediaType, string fileName)
    {
        if (content.Length is < 1 or > MaxImageBytes) throw new ArgumentOutOfRangeException(nameof(content), "A imagem deve ter até 20 MB.");
        var normalizedType = mediaType.ToLowerInvariant() switch { "image/png" => "image/png", "image/jpeg" => "image/jpeg", "image/webp" => "image/webp", _ => throw new ArgumentException("Use uma imagem PNG, JPEG ou WebP.") };
        var signatureValid = normalizedType switch
        {
            "image/png" => content.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/jpeg" => content.Length >= 3 && content[0] == 0xff && content[1] == 0xd8 && content[2] == 0xff,
            "image/webp" => content.Length >= 12 && content.AsSpan(0, 4).SequenceEqual("RIFF"u8) && content.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
        if (!signatureValid) throw new ArgumentException("O conteúdo não corresponde a uma imagem PNG, JPEG ou WebP válida.");
        ImageMediaType = normalizedType;
        ImageContent = content.ToArray();
        var safeName = fileName.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim() ?? "imagem";
        ImageFileName = safeName.Length > 255 ? safeName[..255] : safeName;
    }
    public void RemoveImage() { ImageContent = null; ImageMediaType = null; ImageFileName = null; }
    private static string Required(string? value, int max, string label)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length is < 1 || text.Length > max) throw new ArgumentException($"{label} deve ter entre 1 e {max} caracteres.");
        return text;
    }
    private static string? Optional(string? value, int max, string label)
    {
        var text = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (text?.Length > max) throw new ArgumentException($"{label} deve ter até {max} caracteres.");
        return text;
    }
}

public sealed class CampaignKnowledgeContribution
{
    private CampaignKnowledgeContribution() { }
    public CampaignKnowledgeContribution(Guid id, Guid entryId, string body, Guid authorId, DateTimeOffset now)
    { Id = id; EntryId = entryId; CreatedByParticipantId = authorId; CreatedAt = UpdatedAt = now; Apply(body, now); }
    public Guid Id { get; private set; }
    public Guid EntryId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public Guid CreatedByParticipantId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public void Apply(string? body, DateTimeOffset now)
    {
        var text = body?.Trim() ?? string.Empty;
        if (text.Length is < 1 || text.Length > 8_000) throw new ArgumentException("A contribuição deve ter entre 1 e 8000 caracteres.");
        Body = text; UpdatedAt = now;
    }
}

public sealed class CampaignKnowledgeMasterNote
{
    private CampaignKnowledgeMasterNote() { }
    public CampaignKnowledgeMasterNote(Guid entryId, string? body, Guid authorId, DateTimeOffset now)
    { EntryId = entryId; UpdatedByParticipantId = authorId; Apply(body, now); }
    public Guid EntryId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public Guid UpdatedByParticipantId { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public byte[] Version { get; private set; } = [];
    public void Apply(string? body, DateTimeOffset now)
    {
        var text = body?.Trim() ?? string.Empty;
        if (text.Length > 12_000) throw new ArgumentException("A anotação pessoal deve ter até 12000 caracteres.");
        Body = text; UpdatedAt = now;
    }
}

public sealed class CampaignMapPoint
{
    private CampaignMapPoint() { }
    public CampaignMapPoint(Guid id, Guid mapEntryId, string title, string? description, decimal x, decimal y, Guid authorId, DateTimeOffset now)
    { Id = id; MapEntryId = mapEntryId; CreatedByParticipantId = authorId; CreatedAt = UpdatedAt = now; Apply(title, description, x, y, now); }
    public Guid Id { get; private set; }
    public Guid MapEntryId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal X { get; private set; }
    public decimal Y { get; private set; }
    public Guid CreatedByParticipantId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public void Apply(string? title, string? description, decimal x, decimal y, DateTimeOffset now)
    {
        var name = title?.Trim() ?? string.Empty;
        var detail = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (name.Length is < 1 || name.Length > 100 || detail?.Length > 2_000) throw new ArgumentException("Revise o nome e a descrição do ponto.");
        if (x is < 0 or > 1 || y is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(x), "A posição deve ficar entre 0 e 1.");
        Title = name; Description = detail; X = x; Y = y; UpdatedAt = now;
    }
}
