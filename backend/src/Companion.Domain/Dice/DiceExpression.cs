using System.Globalization;
using System.Text;

namespace Companion.Domain.Dice;

/// <summary>Um termo de dados, como <c>2d6</c> ou <c>-1d4</c>.</summary>
public sealed record DiceTerm(int Count, int Faces, int Sign)
{
    /// <summary>Forma legada e compacta (<c>1d20</c>, <c>-2d6</c>), usada também no resultado de cada termo.</summary>
    public string Normalized => $"{(Sign < 0 ? "-" : string.Empty)}{Count}d{Faces}";

    /// <summary>Forma canônica nova, sem sinal e sem o <c>1</c> do dado único (<c>d20</c>, <c>2d6</c>).</summary>
    internal string Canonical => Count == 1 ? $"d{Faces}" : $"{Count}d{Faces}";
}

/// <summary>Seleção entre duas execuções da soma inteira (spec 011, contracts/dice-expression.md).</summary>
public enum DiceSelection
{
    None,
    /// <summary><c>2#(…) maior</c> (vantagem).</summary>
    Highest,
    /// <summary><c>2#(…) menor</c> (desvantagem).</summary>
    Lowest
}

/// <summary>Uma constante da soma, com sinal. <see cref="Label"/> é nulo nas constantes sem rótulo.</summary>
public sealed record DiceConstant(string? Label, int Value);

/// <summary>
/// Expressão de dados já validada. A gramática e a forma canônica estão em
/// <c>specs/011-ficha-jogador/contracts/dice-expression.md</c>.
/// </summary>
/// <param name="Repetitions">Quantas vezes a expressão inteira é executada (1..20).</param>
/// <param name="Terms">Dados de uma execução, na ordem em que vieram.</param>
/// <param name="Modifier">Soma das constantes de uma execução.</param>
public sealed record DiceExpression(int Repetitions, IReadOnlyList<DiceTerm> Terms, int Modifier)
{
    public const int MaxRepetitions = 20;
    public const int MaxDice = 100;
    public const int MaxFaces = 1000;
    public const int MaxModifier = 10000;
    public const int MaxLength = 256;
    public const int MaxLabelLength = 16;

    internal const string LimitsMessage =
        "Use no máximo 20 repetições, 100 dados no total (maior e menor contam as duas execuções), " +
        "faces entre 2 e 1000, modificador entre -10000 e 10000 e até 256 caracteres.";

    /// <summary>Vantagem/desvantagem. Com seleção, cada repetição executa a soma duas vezes.</summary>
    public DiceSelection Selection { get; init; }

    /// <summary>Constantes de uma execução, com rótulo quando houver, na ordem em que vieram.</summary>
    public IReadOnlyList<DiceConstant> Constants { get; init; } = [];

    /// <summary>A repetição foi escrita com colchetes (<c>N#[…]</c>).</summary>
    public bool Bracketed { get; init; }

    /// <summary>Usa alguma construção nova (rótulo, colchetes ou seleção). Sem elas, o <see cref="Normalized"/> é o legado.</summary>
    public bool UsesExtendedSyntax =>
        Bracketed || Selection != DiceSelection.None || Constants.Any(x => x.Label is not null);

    /// <summary>1 sem seleção, 2 com <c>maior</c>/<c>menor</c>.</summary>
    public int ExecutionsPerRepetition => Selection == DiceSelection.None ? 1 : 2;

    /// <summary>Forma canônica. Legado: compacta (<c>3#3d5+4</c>). Nova: espaçada (<c>3#[2#(d20 + (FOR) 3) maior]</c>).</summary>
    public string Normalized => UsesExtendedSyntax ? ExtendedForm() : LegacyForm();

    public static DiceExpression Parse(string input) => new Parser(input ?? string.Empty).Parse();

    private string LegacyForm()
    {
        var terms = string.Concat(Terms.Select((term, index) =>
            $"{(term.Sign > 0 && index > 0 ? "+" : string.Empty)}{term.Normalized}"));
        var modifier = Modifier > 0
            ? $"+{Modifier}"
            : Modifier < 0 ? Modifier.ToString(CultureInfo.InvariantCulture) : string.Empty;
        return $"{(Repetitions > 1 ? $"{Repetitions}#" : string.Empty)}{terms}{modifier}";
    }

    private string ExtendedForm()
    {
        var sum = new StringBuilder();
        for (var index = 0; index < Terms.Count; index++)
        {
            var term = Terms[index];
            sum.Append(index == 0
                ? term.Sign < 0 ? "-" : string.Empty
                : term.Sign < 0 ? " - " : " + ");
            sum.Append(term.Canonical);
        }

        foreach (var constant in Constants)
        {
            sum.Append(constant.Value < 0 ? " - " : " + ");
            if (constant.Label is not null) sum.Append('(').Append(constant.Label).Append(") ");
            sum.Append(Math.Abs((long)constant.Value).ToString(CultureInfo.InvariantCulture));
        }

        var body = Selection switch
        {
            DiceSelection.Highest => $"2#({sum}) maior",
            DiceSelection.Lowest => $"2#({sum}) menor",
            _ => sum.ToString()
        };
        return Bracketed || Repetitions > 1
            ? $"{Repetitions.ToString(CultureInfo.InvariantCulture)}#[{body}]"
            : body;
    }

    /// <summary>Analisador recursivo descendente. Espaços são ignorados fora dos rótulos.</summary>
    private sealed class Parser(string text)
    {
        private enum Context { Top, Repeated, Bracket, Selection }

        private readonly List<DiceTerm> _terms = [];
        private readonly List<(string? Label, long Value)> _constants = [];
        private int _position;
        private int _repetitions = 1;
        private bool _bracketed;
        private DiceSelection _selection;

        private char Current => _position < text.Length ? text[_position] : '\0';
        private bool AtEnd => _position >= text.Length;

        public DiceExpression Parse()
        {
            SkipWhitespace();
            if (TryRepeatPrefix(out var count))
            {
                SkipWhitespace();
                if (Current == '[')
                {
                    _position++;
                    _bracketed = true;
                    _repetitions = count;
                    ParseBracketBody();
                    SkipWhitespace();
                    Expect(']');
                }
                else if (Current == '(')
                {
                    ParseSelection(count);
                }
                else
                {
                    _repetitions = count;
                    ParseSum(Context.Repeated);
                }
            }
            else
            {
                ParseSum(Context.Top);
            }

            SkipWhitespace();
            if (!AtEnd) throw InvalidFormat();
            return Build();
        }

        private void ParseBracketBody()
        {
            SkipWhitespace();
            if (TryRepeatPrefix(out var count))
            {
                SkipWhitespace();
                if (Current != '(') throw NestedRepetition();
                ParseSelection(count);
                return;
            }

            ParseSum(Context.Bracket);
        }

        private void ParseSelection(int count)
        {
            if (count != 2) throw SelectionOnlyTwo();
            Expect('(');
            ParseSum(Context.Selection);
            SkipWhitespace();
            Expect(')');
            SkipWhitespace();
            var start = _position;
            while (char.IsLetter(Current)) _position++;
            _selection = text[start.._position].ToLowerInvariant() switch
            {
                "maior" => DiceSelection.Highest,
                "menor" => DiceSelection.Lowest,
                _ => throw MissingSelection()
            };
        }

        private void ParseSum(Context context)
        {
            SkipWhitespace();
            var sign = 1;
            if (Current is '+' or '-')
            {
                sign = Current == '-' ? -1 : 1;
                _position++;
            }

            ParseTerm(sign, context);
            while (true)
            {
                SkipWhitespace();
                if (Current is not ('+' or '-')) return;
                sign = Current == '-' ? -1 : 1;
                _position++;
                ParseTerm(sign, context);
            }
        }

        private void ParseTerm(int sign, Context context)
        {
            SkipWhitespace();
            if (Current == '(')
            {
                _position++;
                var close = text.IndexOf(')', _position);
                if (close < 0) throw InvalidFormat();
                var label = NormalizeLabel(text[_position..close]);
                _position = close + 1;
                var digits = ReadDigits();
                if (digits.Length == 0) throw InvalidFormat();
                _constants.Add((label, sign * ParseConstant(digits)));
                return;
            }

            var count = ReadDigits();
            SkipWhitespace();
            if (Current is 'd' or 'D')
            {
                _position++;
                var faces = ReadDigits();
                if (faces.Length == 0) throw InvalidFormat();
                _terms.Add(new DiceTerm(count.Length == 0 ? 1 : ParsePositive(count), ParsePositive(faces), sign));
                return;
            }

            if (count.Length == 0) throw InvalidFormat();
            if (Current == '#') throw context == Context.Selection ? SelectionNesting() : NestedRepetition();
            _constants.Add((null, sign * ParseConstant(count)));
        }

        /// <summary>Lê <c>INT #</c>. Volta à posição original quando não é um prefixo de repetição.</summary>
        private bool TryRepeatPrefix(out int count)
        {
            var start = _position;
            var digits = ReadDigits();
            SkipWhitespace();
            if (digits.Length > 0 && Current == '#')
            {
                _position++;
                count = ParsePositive(digits);
                return true;
            }

            _position = start;
            count = 0;
            return false;
        }

        /// <summary>Dígitos seguidos; espaços entre dígitos são ignorados, como a remoção de espaços do legado.</summary>
        private string ReadDigits()
        {
            SkipWhitespace();
            var digits = new StringBuilder();
            while (true)
            {
                while (char.IsAsciiDigit(Current)) digits.Append(text[_position++]);
                var afterDigits = _position;
                SkipWhitespace();
                if (digits.Length > 0 && char.IsAsciiDigit(Current)) continue;
                _position = afterDigits;
                return digits.ToString();
            }
        }

        private void SkipWhitespace()
        {
            while (!AtEnd && char.IsWhiteSpace(text[_position])) _position++;
        }

        private void Expect(char expected)
        {
            if (Current != expected) throw InvalidFormat();
            _position++;
        }

        private DiceExpression Build()
        {
            if (_terms.Count == 0) throw InvalidFormat();

            var modifier = _constants.Aggregate(0L, (sum, constant) => checked(sum + constant.Value));
            var executions = _selection == DiceSelection.None ? 1 : 2;
            var dicePerExecution = _terms.Sum(x => (long)x.Count);
            if (_repetitions is < 1 or > MaxRepetitions ||
                _terms.Any(x => x.Count is < 1 or > MaxDice || x.Faces is < 2 or > MaxFaces) ||
                dicePerExecution * executions * _repetitions > MaxDice ||
                _constants.Any(x => x.Value is < int.MinValue or > int.MaxValue) ||
                modifier is < -MaxModifier or > MaxModifier)
            {
                throw Limits();
            }

            var expression = new DiceExpression(_repetitions, _terms.ToArray(), (int)modifier)
            {
                Selection = _selection,
                Bracketed = _bracketed,
                Constants = _constants.Select(x => new DiceConstant(x.Label, (int)x.Value)).ToArray()
            };
            if (expression.Normalized.Length > MaxLength) throw Limits();
            return expression;
        }

        private static string NormalizeLabel(string raw)
        {
            var label = string.Join(' ', raw.Normalize(NormalizationForm.FormC)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (label.Length is < 1 or > MaxLabelLength ||
                !label.All(x => char.IsLetter(x) || char.IsAsciiDigit(x) || x is ' ' or '+' or '.' or '\''))
            {
                throw new FormatException(
                    "O rótulo entre parênteses deve ter de 1 a 16 caracteres: letras, números, espaço, +, . ou '.");
            }

            return label;
        }

        private static int ParsePositive(string value)
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "A expressão excede os limites permitidos.");
            }

            return parsed;
        }

        private static long ParseConstant(string value) =>
            long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : throw InvalidFormat();

        private static FormatException InvalidFormat() =>
            new("Use uma expressão como d20, 2d6+3, 3#3d5+4 ou 2d3+2d8+7.");

        private static FormatException SelectionOnlyTwo() =>
            new("A seleção maior/menor só existe com 2 execuções: use 2#(…) maior ou 2#(…) menor.");

        private static FormatException MissingSelection() =>
            new("A seleção 2#(…) precisa terminar com maior ou menor, como 2#(d20 + 5) maior.");

        private static FormatException NestedRepetition() =>
            new("Não use repetição aninhada: para repetir uma seleção, use 3#[2#(d20) maior].");

        private static FormatException SelectionNesting() =>
            new("A seleção maior/menor não pode conter repetição nem outra seleção.");

        private static ArgumentOutOfRangeException Limits() =>
            new("input", LimitsMessage);
    }
}
