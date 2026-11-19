using System.Globalization;
using System.Text;

namespace Companion.Domain.Characters;

/// <summary>
/// Regra única do nome de participante (spec 011, FR-041 e FR-042; research R4). Usada no join,
/// na criação da campanha e no backfill dos participantes antigos, para que a regra seja uma só.
/// </summary>
public static class PersonName
{
    public const int MaxLength = 40;

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Remove espaços nas pontas, reduz qualquer sequência de espaços a um e, em cada parte
    /// separada por espaço ou hífen, deixa a primeira letra maiúscula e as demais minúsculas
    /// (cultura pt-BR). O apóstrofo não separa partes: "d'artagnan" vira "D'artagnan".
    /// O texto é composto em NFC antes, para "João" digitado com acento combinado e com o
    /// caractere pronto dar o mesmo nome. O limite de 40 caracteres vale depois da normalização.
    /// </summary>
    /// <exception cref="ArgumentException">Nome vazio ou com mais de 40 caracteres.</exception>
    public static string Normalize(string? input)
    {
        var composed = (input ?? string.Empty).Normalize(NormalizationForm.FormC);
        var builder = new StringBuilder(composed.Length);
        var pendingSpace = false;
        var startOfPart = true;
        foreach (var character in composed)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                startOfPart = true;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            if (character == '-')
            {
                builder.Append(character);
                startOfPart = true;
                continue;
            }

            if (startOfPart && char.IsLetter(character))
            {
                builder.Append(char.ToUpper(character, PtBr));
                startOfPart = false;
            }
            else
            {
                builder.Append(char.ToLower(character, PtBr));
                // Só letra ou número encerram o início da parte: "3rd" fica "3rd", "(loren" vira "(Loren".
                if (char.IsLetterOrDigit(character)) startOfPart = false;
            }
        }

        var normalized = builder.ToString();
        if (normalized.Length == 0)
        {
            throw new ArgumentException("O nome não pode ficar vazio.");
        }

        if (normalized.Length > MaxLength)
        {
            throw new ArgumentException($"O nome deve ter no máximo {MaxLength} caracteres.");
        }

        return normalized;
    }

    /// <summary>
    /// Chave de identidade da conta: o nome normalizado em maiúsculas invariantes. Ignora maiúsculas
    /// e minúsculas, mas não acentos ("Joao" e "João" são contas diferentes).
    /// </summary>
    public static string Key(string? input) => Normalize(input).ToUpperInvariant();
}
