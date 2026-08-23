using System.Security.Cryptography;
using Companion.Application.Contracts;

namespace Companion.Infrastructure.Randomness;

public sealed class CryptoDiceRandomizer : IDiceRandomizer
{
    public int Roll(int faces) => RandomNumberGenerator.GetInt32(1, checked(faces + 1));
}
