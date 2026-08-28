using System.Security.Cryptography;
using System.Text;
using Companion.Application.Contracts;
using Microsoft.Extensions.Configuration;

namespace Companion.Infrastructure.Security;

public sealed class AdminCredentialValidator(IConfiguration configuration) : IAdminCredentialValidator
{
    public bool IsValid(string credential)
    {
        var expected = configuration["Admin:AccessKey"];
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrEmpty(credential)) return false;

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(credential));
        return CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }
}
