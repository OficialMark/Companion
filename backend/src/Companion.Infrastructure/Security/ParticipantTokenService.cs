using System.Security.Cryptography;
using Companion.Application.Contracts;

namespace Companion.Infrastructure.Security;

public sealed class ParticipantTokenService : IParticipantTokenService
{
    public ParticipantCredential Create()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return new ParticipantCredential(token, SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
    }

    public byte[] Hash(string token)
    {
        if (token.Length is < 32 or > 128)
        {
            throw new FormatException("Token inválido.");
        }

        return SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
    }

    public string CreateCampaignCode() => RandomNumberGenerator.GetInt32(10_000).ToString("D4");
}
