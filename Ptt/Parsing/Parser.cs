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
            case '#':
                return InputCharClass.Hash;
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
            case ',':
            case '!':
                return InputCharClass.Operator;
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

            // We want to use # for directives from now on
            //if (current.chr == '#')
            //{
            //    token.cls = InputCharClass.Comment;
            //    token.endI = n;
            //    yield return token;

            //    continue;
            //}

            token.cls = current.cls;

            var i = 1;

            while (i < n)
            {
                var next = AugmentInputCharWithClass(token.line[i]);

                // An ugly hack with the '!': We should better parse this some other way, but operators
                // are single-character and by now the token already has knowledge about the '!', so
                // it would be some effort to rework that.
                if (next.cls != current.cls || (current.cls.IsSingleCharacter() && current.chr != '!'))
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
    public SyntaxExpression ParseLetters(IEnumerator<InputToken> input)
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

            var newPrecedence = isBraceExpression ? Double.NaN : ownPrecedence;

            return new SyntaxQuantization
            {
                token = firstToken,
                precedence = newPrecedence,
                head = head,
                body = body,
                quantizationDepth = body.quantizationDepth + 1
            };
        }
        else
        {
            var result = new SyntaxAtom { token = firstToken, quantizationDepth = 0 };

            return result;
        }
    }

    public SyntaxExpression ParseBracketedExpression(IEnumerator<InputToken> input)
    {
        var openingBracketToken = input.Current;

        if (openingBracketToken.cls != InputCharClass.OpeningBracket)
        {
            Throw(openingBracketToken, "Expected an opening bracket");
        }

        Increment(ref input);

        var isParens = openingBracketToken.TokenSpan[0] == '(';

        var nextToken = input.Current;

        var isAnnotation = false;

        if (isParens && nextToken.cls == InputCharClass.Colon)
        {
            isAnnotation = true;

            Increment(ref input);
        }

        var inner = ParseExpression(input, outerPrecedence: Double.MinValue);

        inner.isAnnotation = isAnnotation;

        var closingBracketToken = input.Current;

        if (closingBracketToken.cls != InputCharClass.ClosingBracket)
        {
            Throw(closingBracketToken, "Expected a closing bracket");
        }

        Increment(ref input);

        return inner;
    }

    public SyntaxBlockContent ParseContent(IEnumerator<InputToken> input)
    {
        var items = new List<SyntaxBlockItem>();

        var token = input.Current;

        SyntaxBlockContent GetResult()
        {
            return new SyntaxBlockContent
            {
                items = items
            };
        }

        while (true)
        {
            switch (token.cls)
            {
                case InputCharClass.Hash:
                    items.Add(ParseDirectiveBlock(input));
                    break;
                case InputCharClass.Semikolon:
                    Increment(ref input);
                    break;
                case InputCharClass.Eof:
                case InputCharClass.Dot:
                    return GetResult();
                case InputCharClass.Colon:
                case InputCharClass.ClosingBrace:
                case InputCharClass.ClosingBracket:
                case InputCharClass.Space:
                case InputCharClass.Comment:
                case InputCharClass.Unset:
                case InputCharClass.Unknown:
                case InputCharClass.Unsupported:
                    throw Error(token, "Unexpected token in block content");
                default:
                    items.Add(ParseExpression(input, outerPrecedence: Double.MinValue));
                    break;
            }

            token = input.Current;
        }
    }

    DirectiveType GetDirectiveTypeFromName(InputToken token)
    {
        if (Enum.TryParse<DirectiveType>(token.TokenString, true, out var type))
        {
            return type;
        }
        else
        {
            throw Error(token, "Unkown directive");
        }
    }

    InputToken ConsumeExpectation(IEnumerator<InputToken> input, InputCharClass cls, String message)
    {
        if (input.Current.cls != cls)
        {
            throw Error(input.Current, message);
        }

        return Increment(ref input);
    }

    public SyntaxDirectiveBlock ParseDirectiveBlock(IEnumerator<InputToken> input)
    {
        var token = input.Current;

        if (token.cls != InputCharClass.Hash) throw new AssertionException("Should have had a # here");

        var nameToken = Increment(ref input);

        if (nameToken.cls != InputCharClass.Letter)
        {
            throw Error(nameToken, "A hash character must be followed by a directive name");
        }

        var type = GetDirectiveTypeFromName(nameToken);

        Increment(ref input);

        SyntaxDirectiveBlock ParseBody(SyntaxExpression? expr)
        {
            var body = ParseContent(input);

            if (input.Current.cls != InputCharClass.Dot)
            {
                throw Error(nameToken, $"The directive did not terminate on a dot");
            }

            Increment(ref input);

            return new SyntaxDirectiveBlock
            {
                type = type,
                nameToken = nameToken,
                dotToken = token,
                body = body,
                expr = expr
            };
        }

        switch (type)
        {
            case DirectiveType.Section:
                ConsumeExpectation(input, InputCharClass.Colon, "A section directive must be followed by a colon");
                return ParseBody(null);
            case DirectiveType.Claim:
                ConsumeExpectation(input, InputCharClass.Colon, "A claim directive must be followed by a colon");
                var claimExpression = ParseExpression(input, outerPrecedence: Double.MinValue);
                ConsumeExpectation(input, InputCharClass.Semikolon, "A claim directive must be followed by a semicolon after its claim");
                return ParseBody(claimExpression);
            case DirectiveType.Take:
                var takenExpression = ParseExpression(input, outerPrecedence: Double.MinValue);
                ConsumeExpectation(input, InputCharClass.Colon, "A take directive must be followed by a colon after the expression");
                return ParseBody(takenExpression);
            default:
                throw Error(nameToken, $"Unhandled directive type {nameToken}");
        }
    }

    public SyntaxExpression ParseExpression(IEnumerator<InputToken> input, Double outerPrecedence, SyntaxExpression? prefix = null, Boolean stopAfterBracketedExpression = false, Boolean stopOnQuantization = false)
    {
        var nextToken = input.Current;

        var constituents = new List<(SyntaxExpression item, InputToken op)>();

        if (prefix is not null)
        {
            constituents.Add((prefix, default));
        }

        Double ownPrecedence = outerPrecedence;

        SyntaxExpression? pendingResult = null;

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

        SyntaxExpression GetResult()
        {
            FlushPending();

            var c = constituents.Count;
            
            if (c == 0)
            {
                return new SyntaxEmpty { token = nextToken, quantizationDepth = 0 };
            }
            else if (c > 1 || constituents[0].op)
            {
                var quantizationDepth = constituents.Max(i => i.item.quantizationDepth);

                return new SyntaxSequence
                {
                    constituents = constituents,
                    precedence = ownPrecedence,
                    quantizationDepth = quantizationDepth
                };
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
                    Throw(nextToken, $"Not expected to have token '{nextToken}' of class {nextToken.cls} in the input stream for this method");
                    break;
                case InputCharClass.Unsupported:
                    Throw(nextToken, "Unsupported character");
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
                case InputCharClass.Operator:
                    var isFirstOperator = ownPrecedence == outerPrecedence;

                    if (isFirstOperator)
                    {
                        // We encoutered an operator for the first time, let's set our precedence.

                        var precedence = guide.GetOperatorPrecedence(nextToken);

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

                        var newPrecedence = guide.GetOperatorPrecedence(nextToken);

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

                        var newPrecedence = guide.GetOperatorPrecedence(nextToken);

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
                case InputCharClass.Hash:
                case InputCharClass.Semikolon:
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

    InputToken Increment(ref IEnumerator<InputToken> input)
    {
        var token = input.Current;

        if (!input.MoveNext())
        {
            Throw(token, "Unexpected end of input");
        }

        return input.Current;
    }

    void NotYetImplemented()
    {
        throw new NotImplementedException("Not yet implemented");
    }

    ParsingException Error(InputToken token, String message)
    {
        return new ParsingException(token.GetContextMessage(message));
    }

    void Throw(InputToken token, String message)
    {
        throw Error(token, message);
    }
}
