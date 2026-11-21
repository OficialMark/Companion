using Companion.Application.Contracts;
using Companion.Domain.Characters;
using Companion.Domain.Dice;

namespace Companion.Application.DiceRolls;

/// <summary>Validates the client-supplied type labels and calculates their subtotals from server results.</summary>
public static class DamageBreakdownBuilder
{
    public static IReadOnlyList<DiceRollResultDto> Attach(
        DiceExpression expression,
        IReadOnlyList<DiceRollResultDto> results,
        DiceDamageBreakdownRequest? request)
    {
        if (request is null) return results;
        if (expression.Selection != DiceSelection.None || request.BaseTermCount < 0 || request.Components is null || request.Components.Count > 8)
            throw new ArgumentException("O detalhamento de dano não corresponde a uma rolagem simples.");

        var components = request.Components.Select(component =>
        {
            if (component is null || component.TermCount is < 1 or > 7 || string.IsNullOrWhiteSpace(component.Label) || component.Label.Trim().Length > 40 || string.IsNullOrWhiteSpace(component.DamageType))
                throw new ArgumentException("Revise os componentes do detalhamento de dano.");
            return component with { Label = component.Label.Trim(), DamageType = DamageTypeCatalog.Normalize(component.DamageType) };
        }).ToArray();
        var baseType = string.IsNullOrWhiteSpace(request.BaseType) ? null : DamageTypeCatalog.Normalize(request.BaseType);
        var baseTermCount = request.BaseTermCount;
        if (baseTermCount + components.Sum(component => component.TermCount) != expression.Terms.Count ||
            (baseType is null && components.Length == 0))
            throw new ArgumentException("Os componentes de dano não correspondem aos dados rolados.");

        return results.Select(result =>
        {
            var componentsResult = new List<DiceDamageComponentDto>(components.Length);
            var termOffset = baseTermCount;
            foreach (var component in components)
            {
                var subtotal = result.Terms.Skip(termOffset).Take(component.TermCount).Sum(term => term.Subtotal);
                componentsResult.Add(new DiceDamageComponentDto(component.Label, component.DamageType, subtotal));
                termOffset += component.TermCount;
            }
            int? baseSubtotal = baseType is null
                ? null
                : result.Terms.Take(baseTermCount).Sum(term => term.Subtotal) + result.Modifier;
            return result with { DamageBreakdown = new DiceDamageBreakdownDto(baseType, baseSubtotal, componentsResult) };
        }).ToArray();
    }
}
