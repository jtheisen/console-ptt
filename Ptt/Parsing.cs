using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Ptt;

public enum InputCharClass
{
    Unset,
    Unknown,
    Unsupported,

    Space,
    Comment,

    Eof,

    Comma,
    Semikolon,
    Colon,
    Dot,

    Letter,
    Operator, // includes relations
    OpeningBracket,
    ClosingBracket
}

public struct InputToken
{
    public InputCharClass cls;
    public String line;
    public Int32 lineI;
    public Int32 colI;
    public Int32 endI;

    public String TokenString => line[colI..endI];
    public ReadOnlySpan<Char> TokenSpan => line.AsSpan()[colI..endI];

    public override String ToString() => TokenString;

    public static implicit operator Boolean(InputToken token) => token.cls != InputCharClass.Unset;

    public void Clear() => cls = InputCharClass.Unset;
}

public static class InputCharClassExtensions
{
    public static Boolean IsSubstantial(this InputCharClass cls)
    {
        switch (cls)
        {
            case InputCharClass.Space:
            case InputCharClass.Comment:
                return false;
            default:
                return true;
        }
    }
}

public class ParsingException : Exception
{
    public ParsingException(String message)
        : base(message)
    {
    }
}

public class ParsedResult
{
}

public class ParsedChain : ParsedResult
{
    public required List<(ParsedResult item, InputToken op)> constituents;

    public override String ToString()
    {
        var sb = new StringBuilder();
        foreach (var part in constituents)
        {
            var (item, op) = part;

            if (op)
            {
                sb.Append(op.TokenSpan);
            }

            if (item is ParsedAtom atom)
            {
                sb.Append(atom.token.TokenSpan);
            }
            else
            {
                sb.Append('(');
                sb.Append(item.ToString());
                sb.Append(')');
            }
        }
        return sb.ToString();
    }
}

public class ParsedAtom : ParsedResult
{
    public InputToken token;

    public override String ToString()
    {
        return token.TokenString;
    }
}

public class PrecedenceProvider
{
    static String BooleanSymbols = "⇒⇐⇔ ,∨∧ =";
    static String DomainSymbols = "+- */";
    static String RelationSymbols = "<>≤≥ ⊂⊃ ⊆⊇ ∊∍ ∈∋ ϵ";

    static Dictionary<Char, Double> precedences;

    static PrecedenceProvider()
    {
        precedences = new Dictionary<Char, Double>();

        {
            var precedence = 0;
            foreach (var symbolGroup in DomainSymbols.Split())
            {
                ++precedence;

                foreach (var chr in symbolGroup)
                {
                    precedences[chr] = precedence;
                }
            }
        }

        {
            var symbolGroups = BooleanSymbols.Split();
            for (var i = 0; i < symbolGroups.Length; ++i)
            {
                foreach (var chr in symbolGroups[i])
                {
                    precedences[chr] = -symbolGroups.Length + i;
                }
            }
        }
    }

    public Double GetPrecedence(ReadOnlySpan<Char> op)
    {
        if (op.Length == 1)
        {
            var chr = op[0];

            if (RelationSymbols.IndexOf(chr) != -1 )
            {
                return 0;
            }

            if (precedences.TryGetValue(chr, out var precedence))
            {
                return precedence;
            }
        }

        throw new Exception($"Unknown operator {new String(op)}");
    }
}

public class Parser
{
    static InputCharClass GetInputCharClass(Char c)
    {
        if (Char.IsWhiteSpace(c))
        {
            return InputCharClass.Space;
        }

        switch (c)
        {
            case ',':
                return InputCharClass.Comma;
            case ';':
                return InputCharClass.Semikolon;
            case ':':
                return InputCharClass.Colon;
            case '.':
                return InputCharClass.Dot;
            default:
                break;
        }

        var category = Char.GetUnicodeCategory(c);

        switch (category)
        {
            case UnicodeCategory.UppercaseLetter:
            case UnicodeCategory.LowercaseLetter:
            case UnicodeCategory.TitlecaseLetter:
            case UnicodeCategory.ModifierLetter:
            case UnicodeCategory.OtherLetter:
                return InputCharClass.Letter;
            case UnicodeCategory.DashPunctuation:
            case UnicodeCategory.MathSymbol:
            case UnicodeCategory.CurrencySymbol:
            case UnicodeCategory.ModifierSymbol:
            case UnicodeCategory.OtherSymbol:
            case UnicodeCategory.OtherPunctuation:
                return InputCharClass.Operator;
            case UnicodeCategory.OpenPunctuation:
                return InputCharClass.OpeningBracket;
            case UnicodeCategory.ClosePunctuation:
                return InputCharClass.ClosingBracket;
            case UnicodeCategory.InitialQuotePunctuation:
            case UnicodeCategory.FinalQuotePunctuation:
                return InputCharClass.Unsupported;
            default:
                return InputCharClass.Unknown;
        }
    }

    static (Char chr, InputCharClass cls) AugmentInputCharWithClass(Char c) => (c, GetInputCharClass(c));

    public IEnumerable<InputToken> Tokenize(IEnumerable<String> input)
    {
        var reader = input.GetEnumerator();

        InputToken token = default;

        token.lineI = -1;

        while (reader.MoveNext())
        {
            ++token.lineI;
            token.colI = 0;

            token.line = reader.Current;

            var n = token.line.Length;

            if (n == 0)
            {
                ++token.lineI;
                continue;
            }

            var current = AugmentInputCharWithClass(token.line[0]);

            if (current.chr == '#')
            {
                token.cls = InputCharClass.Comment;
                token.endI = n;
                yield return token;

                continue;
            }

            token.cls = current.cls;

            var i = 0;

            while (i < n)
            {
                var next = AugmentInputCharWithClass(token.line[i]);

                if (next.cls != current.cls)
                {
                    token.endI = i;
                    yield return token;

                    token.colI = i;
                    token.cls = next.cls;
                }

                current = next;
                ++i;
            }

            token.endI = n;
            yield return token;
        }

        token.colI = token.endI = token.line?.Length ?? 0;
        token.cls = InputCharClass.Eof;
        yield return token;
    }

    PrecedenceProvider precedenceProvider = new PrecedenceProvider();

    /* This parses only atoms and chains
     */
    public ParsedResult ParseExpression(IEnumerator<InputToken> input, ParsedResult? prefix = null, Double outerPrecedence = 0)
    {
        var nextToken = input.Current;

        var constituents = new List<(ParsedResult item, InputToken op)>();

        if (prefix is not null)
        {
            constituents.Add((prefix, default));
        }

        Double ownPrecedence = outerPrecedence;

        //InputToken currentSymbol = default;
        ParsedResult? pendingResult = null;

        InputToken latestOpToken = default;

        void FlushPending()
        {
            if (pendingResult is not null)
            {
                // add atom with latestOp
                constituents.Add((pendingResult, latestOpToken));
                pendingResult = null;
                latestOpToken.Clear();
            }
        }

        ParsedResult GetResult()
        {
            FlushPending();

            if (constituents.Count > 1 || constituents[0].op)
            {
                return new ParsedChain { constituents = constituents };
            }
            else if (constituents.Count == 1)
            {
                return constituents[0].item;
            }
            else
            {
                throw new Exception("Empty result");
            }
        }

        while (true)
        {
            switch (nextToken.cls)
            {
                case InputCharClass.Unknown:
                case InputCharClass.Space:
                case InputCharClass.Comment:
                    Throw(nextToken, $"Not expected to have token class {nextToken.cls} in the input stream for this method");
                    break;
                case InputCharClass.Unsupported:
                    Throw(nextToken, "Unsupported character");
                    break;
                case InputCharClass.Comma:
                case InputCharClass.Semikolon:
                case InputCharClass.Colon:
                case InputCharClass.Dot:
                    NotYetImplemented();
                    break;
                case InputCharClass.Letter:
                    if (pendingResult is not null)
                    {
                        // must be application or end of expression
                        return GetResult();
                    }
                    else
                    {
                        pendingResult = new ParsedAtom { token = nextToken };
                        Increment(ref input);
                    }
                    break;
                case InputCharClass.Operator:
                    var isFirstOperator = ownPrecedence == outerPrecedence;

                    if (isFirstOperator)
                    {
                        // We encoutered an operator for the first time, let's set our precedence.

                        var precedence = precedenceProvider.GetPrecedence(nextToken.TokenSpan);

                        if (precedence == outerPrecedence)
                        {
                            Throw(nextToken, "First operator has same precedence as the outer context");
                        }
                        else if (precedence < outerPrecedence)
                        {
                            Throw(nextToken, "First operator has even lower precedence than the outer context");
                        }

                        ownPrecedence = precedence;
                    }

                    // Now we have always ownPrecedence > outerPrecedence

                    if (pendingResult is not null)
                    {
                        // We already have an expression, now do we take it or is it part of
                        // a more tightly bound parent expression?

                        var newPrecedence = precedenceProvider.GetPrecedence(nextToken.TokenSpan);

                        if (newPrecedence > ownPrecedence)
                        {
                            // A new operator binds even tighter. The alreay parsed result is to be
                            // consumed by the recursive call.

                            pendingResult = ParseExpression(input, prefix: pendingResult, outerPrecedence: ownPrecedence);
                        }
                        else if (newPrecedence == ownPrecedence)
                        {
                            // A new operator on the same level. We are consuming it in the current chain.

                            FlushPending();

                            latestOpToken = nextToken;

                            Increment(ref input);
                        }
                        else if (newPrecedence > outerPrecedence)
                        {
                            // A precedence lower that ownPrecedence, but higher than outerPrecdence.
                            // We pass the current result into a recursive call for the rest.
                            // We pass the outerPrecedence unchanged to ensure we don't parse beyond the
                            // outer precedence level.

                            var currentResult = GetResult();

                            return ParseExpression(input, prefix: currentResult, outerPrecedence: outerPrecedence);
                        }
                        else
                        {
                            // An operator of the outer context. This expression is complete.

                            return GetResult();
                        }
                    }
                    else if (latestOpToken)
                    {
                        // Two consecutive operators

                        var newPrecedence = precedenceProvider.GetPrecedence(nextToken.TokenSpan);

                        if (ownPrecedence == newPrecedence)
                        {
                            Throw(nextToken, "Two consecutive operators with the same precedence encountered");
                        }
                        else
                        {
                            var nested = ParseExpression(input, outerPrecedence: ownPrecedence);
                            constituents.Add((nested, latestOpToken));
                            latestOpToken.Clear();
                        }
                    }
                    else
                    {
                        latestOpToken = nextToken;
                        Increment(ref input);
                    }
                    break;
                case InputCharClass.OpeningBracket:
                    if (pendingResult is not null)
                    {
                        Throw(nextToken, "Can't have an opening bracket after a pending expression");
                    }
                    else
                    {
                        Increment(ref input);
                        var nested = ParseExpression(input);
                        constituents.Add((nested, latestOpToken));
                        latestOpToken.Clear();
                    }
                    break;
                case InputCharClass.Eof:
                    return GetResult();
                case InputCharClass.ClosingBracket:
                    Increment(ref input);
                    return GetResult();
                default:
                    Throw(nextToken, $"Unknown input class {nextToken.cls}");
                    break;
            }

            nextToken = input.Current;
        }
    }

    void Increment(ref IEnumerator<InputToken> input)
    {
        var token = input.Current;

        if (!input.MoveNext())
        {
            Throw(token, "Unexpected end of input");
        }
    }

    void NotYetImplemented()
    {
        throw new NotImplementedException("Not yet implemented");
    }

    void Throw(InputToken token, String message)
    {
        var prefix = token.line[..token.colI];
        var suffix = token.line[token.endI..];

        throw new ParsingException($"{message} at {token.lineI}:{token.colI}, line: '{prefix}⋮{token.TokenString}⋮{suffix}'");
    }
}
