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
    SymbolLetter,
    Operator,
    OpeningBrace,
    ClosingBrace,
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

    public override String ToString() => line is not null ? line.Length == 0 ? $":empty {cls}:" : TokenString : $":{cls}:";

    public static implicit operator Boolean(InputToken token) => token.cls != InputCharClass.Unset;

    public void Clear() => cls = InputCharClass.Unset;
}

public static class InputCharClassExtensions
{
    public static Boolean IsSubstantial(this InputCharClass cls, Boolean includeEof = true)
    {
        switch (cls)
        {
            case InputCharClass.Space:
            case InputCharClass.Comment:
                return false;
            case InputCharClass.Eof:
                return includeEof;
            default:
                return true;
        }
    }

    public static Boolean IsSingleCharacter(this InputCharClass cls)
    {
        switch (cls)
        {
            case InputCharClass.SymbolLetter:
            case InputCharClass.Comma:
            case InputCharClass.Semikolon:
            case InputCharClass.Colon:
            case InputCharClass.Dot:
            case InputCharClass.Operator:
            case InputCharClass.OpeningBracket:
            case InputCharClass.ClosingBracket:
                return true;
            default:
                return false;
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
    public static ParsedResult EmptyResult = new ParsedResult();

    public virtual Boolean IsEmpty => true;

    public static implicit operator Boolean(ParsedResult self) => !self.IsEmpty;
}

public class ParsedChain : ParsedResult
{
    public required List<(ParsedResult item, InputToken op)> constituents;

    public override Boolean IsEmpty => constituents.Count == 0;

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
    public required InputToken token;

    public override Boolean IsEmpty => false;

    public override String ToString() => token.ToString();
}

public class ParsedQuantization : ParsedResult
{
    public required InputToken token;

    public required ParsedResult head;
    public required ParsedResult body;

    public Boolean IsBraceExpression => token.cls == InputCharClass.OpeningBrace;

    public override Boolean IsEmpty => false;

    public override String ToString()
    {
        if (IsBraceExpression)
        {
            return $"{{ {head}: {body} }}";
        }
        else
        {
            return $"{token} {head}: {body}";
        }
    }
}

public class ParserGuide
{
    static String BooleanSymbols = "⇒⇐⇔ ,∨∧ =";
    static String DomainSymbols = "+- */ ^";
    static String RelationSymbols = "<>≤≥ ⊂⊃ ⊆⊇ ∊∍ ∈∋ ϵ";

    static String QuantizationCharacters = "∀∃";

    static String SymbolLetters =
        "∀∃∑∏";
    static String SymbolLetterPrecedencePairings =
        "∨∨+*";

    static Dictionary<Char, Double> precedences;

    static ParserGuide()
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

    Double? GetPrecedenceForOperatorCharacter(Char chr)
    {
        if (RelationSymbols.IndexOf(chr) != -1)
        {
            return 0;
        }

        if (precedences.TryGetValue(chr, out var precedence))
        {
            return precedence;
        }

        return null;
    }

    public Double GetPrecedence(ReadOnlySpan<Char> op)
    {
        if (op.Length == 1)
        {
            var chr = op[0];

            if (GetPrecedenceForOperatorCharacter(chr) is Double precedence)
            {
                return precedence;
            }
        }

        throw new Exception($"Unknown operator {new String(op)}");
    }

    public Boolean IsSymbolLetter(Char c, out Double precedence)
    {
        precedence = 0;

        var i = SymbolLetters.IndexOf(c);
        
        if (i >= 0)
        {
            var pairing = SymbolLetterPrecedencePairings[i];

            if (GetPrecedenceForOperatorCharacter(pairing) is Double p)
            {
                precedence = p;
            }
            else
            {
                throw new Exception($"Unkown pairing for '{c}'");
            }

            return true;
        }
        else
        {
            return false;
        }
    }

    public Boolean IsQuantizationSymbol(ReadOnlySpan<Char> op, out Double precedence)
    {
        precedence = 0;

        if (op.Length == 1)
        {
            var chr = op[0];

            return IsSymbolLetter(chr, out precedence);
        }
        else
        {
            return false;
        }
    }

    public Boolean IsBooleanQuantizationSymbol(ReadOnlySpan<Char> op)
    {
        if (op.Length == 1)
        {
            var chr = op[0];

            return QuantizationCharacters.IndexOf(chr) >= 0;
        }
        else
        {
            return false;
        }
    }
}

public class Parser
{
    ParserGuide guide = new ParserGuide();

    InputCharClass GetInputCharClass(Char c)
    {
        if (Char.IsWhiteSpace(c))
        {
            return InputCharClass.Space;
        }

        if (guide.IsSymbolLetter(c, out _))
        {
            return InputCharClass.SymbolLetter;
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
            case '{':
                return InputCharClass.OpeningBrace;
            case '}':
                return InputCharClass.ClosingBrace;
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

    (Char chr, InputCharClass cls) AugmentInputCharWithClass(Char c) => (c, GetInputCharClass(c));

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

            var i = 1;

            while (i < n)
            {
                var next = AugmentInputCharWithClass(token.line[i]);

                if (next.cls != current.cls || current.cls.IsSingleCharacter())
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

    // Parses atoms, quantizations and brace expressions
    public ParsedResult ParseLetters(IEnumerator<InputToken> input)
    {
        var firstToken = input.Current;

        var isQuantization = guide.IsQuantizationSymbol(firstToken.TokenSpan, out var ownPrecedence);

        var isBraceExpression = firstToken.cls == InputCharClass.OpeningBrace;

        Increment(ref input);

        if (isQuantization || isBraceExpression)
        {
            if (isBraceExpression)
            {
                ownPrecedence = Double.MinValue;
            }

            var head = ParseExpression(input, outerPrecedence: Double.MinValue);

            if (!head)
            {
                Throw(firstToken, "Expected quantization to be followed by a head");
            }

            if (input.Current.cls == InputCharClass.Colon)
            {
                Increment(ref input);
            }
            else if (!isQuantization)
            {
                Throw(firstToken, "Expected a colon to terminate the head of quantization");
            }

            var body = ParseExpression(input, outerPrecedence: ownPrecedence);

            if (!body)
            {
                Throw(firstToken, "Expected quantization to be followed by a body after the head");
            }

            if (isBraceExpression)
            {
                if (input.Current.cls != InputCharClass.ClosingBrace)
                {
                    Throw(input.Current, "Expected closing brace");
                }

                Increment(ref input);
            }

            return new ParsedQuantization { token = firstToken, head = head, body = body };
        }
        else
        {
            var result = new ParsedAtom { token = firstToken };

            return result;
        }
    }

    public ParsedResult ParseBracketedExpression(IEnumerator<InputToken> input)
    {
        var openingBracketToken = input.Current;

        if (openingBracketToken.cls != InputCharClass.OpeningBracket)
        {
            Throw(openingBracketToken, "Expected an opening bracket");
        }

        Increment(ref input);

        var inner = ParseExpression(input, outerPrecedence: Double.MinValue);

        var closingBracketToken = input.Current;

        if (closingBracketToken.cls != InputCharClass.ClosingBracket)
        {
            Throw(closingBracketToken, "Expected a closing bracket");
        }

        Increment(ref input);

        return inner;
    }

    public ParsedResult ParseExpression(IEnumerator<InputToken> input, ParsedResult? prefix = null, Double outerPrecedence = 0, Boolean stopAfterBracketedExpression = false, Boolean stopOnQuantization = false)
    {
        var nextToken = input.Current;

        var constituents = new List<(ParsedResult item, InputToken op)>();

        if (prefix is not null)
        {
            constituents.Add((prefix, default));
        }

        Double ownPrecedence = outerPrecedence;

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

            var c = constituents.Count;
            
            if (c == 0)
            {
                return ParsedResult.EmptyResult;
            }
            else if (c > 1 || constituents[0].op)
            {
                return new ParsedChain { constituents = constituents };
            }
            else if (c == 1)
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
                case InputCharClass.Semikolon:
                    NotYetImplemented();
                    break;
                case InputCharClass.Letter:
                case InputCharClass.SymbolLetter:
                case InputCharClass.OpeningBrace:
                    if (pendingResult is not null)
                    {
                        if (guide.IsBooleanQuantizationSymbol(nextToken.TokenSpan))
                        {
                            return GetResult();
                        }
                        else
                        {
                            Throw(nextToken, $"Token of type {nextToken.cls} can't follow an expression");
                        }
                    }
                    else
                    {
                        pendingResult = ParseLetters(input);
                    }
                    break;
                case InputCharClass.Comma:
                case InputCharClass.Operator:
                    var isFirstOperator = ownPrecedence == outerPrecedence;

                    if (isFirstOperator)
                    {
                        // We encoutered an operator for the first time, let's set our precedence.

                        var precedence = guide.GetPrecedence(nextToken.TokenSpan);

                        if (precedence == outerPrecedence)
                        {
                            Throw(nextToken, "First operator has same precedence as the outer context");
                        }

                        ownPrecedence = precedence;
                    }

                    // Now we have always ownPrecedence != outerPrecedence

                    if (pendingResult is not null)
                    {
                        // We already have an expression, now do we take it or is it part of
                        // a more tightly bound parent expression?

                        var newPrecedence = guide.GetPrecedence(nextToken.TokenSpan);

                        if (newPrecedence <= outerPrecedence && !isFirstOperator)
                        {
                            // The new operator belongs to an outer context, except if it's the first. If
                            // it's the first, we a consecutive operator and treat it like any other.

                            return GetResult();
                        }
                        else if (newPrecedence > ownPrecedence)
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
                        else
                        {
                            // A precedence lower that ownPrecedence, but higher than outerPrecdence.
                            // We pass the current result into a recursive call for the rest.
                            // We pass the outerPrecedence unchanged to ensure we don't parse beyond the
                            // outer precedence level.

                            var currentResult = GetResult();

                            return ParseExpression(input, prefix: currentResult, outerPrecedence: outerPrecedence);
                        }
                    }
                    else if (latestOpToken)
                    {
                        // Two consecutive operators

                        var newPrecedence = guide.GetPrecedence(nextToken.TokenSpan);

                        if (ownPrecedence == newPrecedence)
                        {
                            Throw(nextToken, "Two consecutive operators with the same precedence encountered");
                        }
                        else
                        {
                            pendingResult = ParseExpression(input, outerPrecedence: ownPrecedence);
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
                        pendingResult = ParseBracketedExpression(input);

                        if (stopAfterBracketedExpression)
                        {
                            return GetResult();
                        }
                    }
                    break;
                case InputCharClass.Colon:
                case InputCharClass.Dot:
                case InputCharClass.Eof:
                case InputCharClass.ClosingBracket:
                case InputCharClass.ClosingBrace:
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
