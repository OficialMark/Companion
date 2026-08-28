namespace Companion.Domain.Models;

public sealed class CampaignAccessCode
{
    private CampaignAccessCode() { }

    public CampaignAccessCode(string code, Guid campaignId, ParticipantRole role)
    {
        if (code.Length != 4 || code.Any(character => character is < '0' or > '9'))
        {
            throw new ArgumentException("O código deve conter exatamente quatro dígitos.", nameof(code));
        }

        Code = code;
        CampaignId = campaignId;
        Role = role;
    }

    public string Code { get; private set; } = string.Empty;
    public Guid CampaignId { get; private set; }
    public ParticipantRole Role { get; private set; }
}
