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
