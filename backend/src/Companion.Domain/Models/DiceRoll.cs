namespace Companion.Domain.Models;

public sealed class DiceRoll
{
    private DiceRoll() { }

    public DiceRoll(
        Guid id,
        Guid campaignId,
        Guid participantId,
        Guid clientRequestId,
        string expression,
        string valuesJson,
        int modifier,
        int total,
        DateTimeOffset createdAt)
    {
        Id = id;
        CampaignId = campaignId;
        ParticipantId = participantId;
        ClientRequestId = clientRequestId;
        Expression = expression;
        ValuesJson = valuesJson;
        Modifier = modifier;
        Total = total;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public Guid ParticipantId { get; private set; }
    public Guid ClientRequestId { get; private set; }
    public string Expression { get; private set; } = string.Empty;
    public string ValuesJson { get; private set; } = "[]";
    public int Modifier { get; private set; }
    public int Total { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
