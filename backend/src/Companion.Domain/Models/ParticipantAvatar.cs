namespace Companion.Domain.Models;

/// <summary>
/// Profile photo of a participant. Stored apart from <see cref="Participant"/> so the
/// image bytes are only loaded when the avatar itself is requested.
/// </summary>
public sealed class ParticipantAvatar
{
    public const int MaxImageBytes = 1024 * 1024;

    private ParticipantAvatar() { }

    public ParticipantAvatar(Guid participantId, byte[] content, string mediaType, DateTimeOffset updatedAt)
    {
        ParticipantId = participantId;
        Replace(content, mediaType, updatedAt);
    }

    public Guid ParticipantId { get; private set; }
    public byte[] Content { get; private set; } = [];
    public string MediaType { get; private set; } = string.Empty;
    public Guid Revision { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Replace(byte[] content, string mediaType, DateTimeOffset updatedAt)
    {
        if (content.Length is < 1 or > MaxImageBytes)
            throw new ArgumentOutOfRangeException(nameof(content), "A foto deve ter no máximo 1 MB.");
        if (string.IsNullOrWhiteSpace(mediaType))
            throw new ArgumentException("O tipo da imagem é obrigatório.", nameof(mediaType));

        Content = content.ToArray();
        MediaType = mediaType;
        Revision = Guid.NewGuid();
        UpdatedAt = updatedAt;
    }
}
