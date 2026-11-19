using Companion.Application.Contracts;
using Companion.Domain.Dice;

namespace Companion.Application.DiceRolls;

/// <summary>
/// Executa uma <see cref="DiceExpression"/> já validada (spec 011, contracts/dice-expression.md, seção "Execução").
/// Pura: o único efeito é chamar o <see cref="IDiceRandomizer"/>, na ordem repetição, execução, termo, dado.
/// Fica na Application porque <see cref="IDiceRandomizer"/> é declarado aqui e o Domain não depende da Application.
/// </summary>
public static class DiceRoller
{
    public const string Highest = "maior";
    public const string Lowest = "menor";

    public static IReadOnlyList<DiceRollResultDto> Roll(DiceExpression expression, IDiceRandomizer randomizer)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(randomizer);

        var modifiers = expression.UsesExtendedSyntax
            ? expression.Constants.Select(x => new DiceModifierDto(x.Label, x.Value)).ToArray()
            : null;
        var results = new DiceRollResultDto[expression.Repetitions];
        for (var index = 0; index < results.Length; index++)
        {
            var sequence = index + 1;
            if (expression.Selection == DiceSelection.None)
            {
                var single = Execute(expression, randomizer);
                results[index] = new DiceRollResultDto(sequence, single.Terms, single.Modifier, single.Total, modifiers);
                continue;
            }

            var first = Execute(expression, randomizer);
            var second = Execute(expression, randomizer);
            // Empate fica com a primeira execução.
            var selected = expression.Selection == DiceSelection.Highest
                ? second.Total > first.Total ? 1 : 0
                : second.Total < first.Total ? 1 : 0;
            var chosen = selected == 0 ? first : second;
            results[index] = new DiceRollResultDto(sequence, chosen.Terms, chosen.Modifier, chosen.Total, modifiers,
                expression.Selection == DiceSelection.Highest ? Highest : Lowest, [first, second], selected);
        }

        return results;
    }

    /// <summary>Uma execução da soma inteira: todos os dados rolados, todas as constantes somadas.</summary>
    private static DiceExecutionDto Execute(DiceExpression expression, IDiceRandomizer randomizer)
    {
        var terms = new DiceTermResultDto[expression.Terms.Count];
        var subtotal = 0;
        for (var index = 0; index < terms.Length; index++)
        {
            var term = expression.Terms[index];
            var values = new int[term.Count];
            for (var die = 0; die < values.Length; die++) values[die] = randomizer.Roll(term.Faces);
            terms[index] = new DiceTermResultDto(term.Normalized, values, checked(term.Sign * values.Sum()));
            subtotal = checked(subtotal + terms[index].Subtotal);
        }

        return new DiceExecutionDto(terms, expression.Modifier, checked(subtotal + expression.Modifier));
    }
}
