using System.Globalization;

namespace Ptt;

public class Parser
{
    TestContext guide = new TestContext();

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

            var body = ParseExpression(input, outerPrecedence: isBraceExpression ? Double.MinValue : ownPrecedence);

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

            return new ParsedQuantization { token = firstToken, precedence = isBraceExpression ? Double.NaN : ownPrecedence, head = head, body = body };
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
                return new ParsedEmptyResult { token = nextToken };
            }
            else if (c > 1 || constituents[0].op)
            {
                return new ParsedChain { constituents = constituents, precedence = ownPrecedence };
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
        throw new ParsingException(token.GetContextMessage(message));
    }
}
