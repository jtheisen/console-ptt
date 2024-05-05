using System.Diagnostics;
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

[DebuggerDisplay("{DebugString}")]
public struct InputToken
{
    public InputCharClass cls;
    public String line;
    public Int32 lineI;
    public Int32 colI;
    public Int32 endI;

    public String TokenString => line[colI..endI];
    public ReadOnlySpan<Char> TokenSpan => line.AsSpan()[colI..endI];

    public Boolean TryGetOperatorName([NotNullWhen(true)] out String? opName, out Boolean negated)
    {
        var span = TokenSpan;

        opName = null;
        negated = false;

        if (cls != InputCharClass.Operator || span.Length == 0) return false;

        negated = span[0] == '!';

        opName = span[(negated ? 1 : 0)..].ToString();

        return true;
    }

    public String DebugString => line is not null ? line.Length == 0 ? $":empty {cls}:" : TokenString : $":{cls}:";

    public override String ToString() => DebugString;

    public static implicit operator Boolean(InputToken token) => token.cls != InputCharClass.Unset;

    public void Clear() => cls = InputCharClass.Unset;

    public static implicit operator String(InputToken token) => token.TokenString;

    public String GetContextMessage(String message)
    {
        var prefix = line[..colI];
        var suffix = line[endI..];

        throw new ParsingException($"{message} at {lineI}:{colI}, line: '{prefix}⋮{TokenString}⋮{suffix}'");
    }
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

[Flags]
public enum SyntaxNodeStringificationFlags
{
    None = 0,
    ParenBodies = 1
}

public abstract class SyntaxNode
{
    public abstract Boolean IsEmpty { get; }

    public abstract InputToken GetRepresentativeToken();

    public required Int32 quantizationDepth;

    public Boolean isAnnotation;

    public static implicit operator Boolean(SyntaxNode self) => !self.IsEmpty;

    public abstract String ToString(SyntaxNodeStringificationFlags flags);

    public override String ToString() => ToString(SyntaxNodeStringificationFlags.None);
}

public class SyntaxEmpty : SyntaxNode
{
    public required InputToken token;

    public override Boolean IsEmpty => true;

    public override InputToken GetRepresentativeToken() => token;

    public override String ToString(SyntaxNodeStringificationFlags flags) => "";
}

public class SyntaxChain : SyntaxNode
{
    public required List<(SyntaxNode item, InputToken op)> constituents;

    public required Double precedence;

    public override Boolean IsEmpty => constituents.Count == 0;

    public override String ToString(SyntaxNodeStringificationFlags flags)
    {
        var sb = new StringBuilder();
        foreach (var part in constituents)
        {
            var (item, op) = part;

            if (op)
            {
                sb.Append(op.TokenSpan);
            }

            if (item is SyntaxAtom atom)
            {
                sb.Append(atom.token.TokenSpan);
            }
            else
            {
                sb.Append('(');
                sb.Append(item.ToString(flags));
                sb.Append(')');
            }
        }
        return sb.ToString();
    }

    public override InputToken GetRepresentativeToken() => constituents[0].op;
}

public class SyntaxAtom : SyntaxNode
{
    public required InputToken token;

    public override Boolean IsEmpty => false;

    public override String ToString(SyntaxNodeStringificationFlags flags) => token.ToString();

    public override InputToken GetRepresentativeToken() => token;
}

public class SyntaxQuantization : SyntaxNode
{
    public required InputToken token;

    public required Double precedence;

    public required SyntaxNode head;
    public required SyntaxNode body;

    public Boolean IsBraceExpression => token.cls == InputCharClass.OpeningBrace;

    public override Boolean IsEmpty => false;

    public override String ToString(SyntaxNodeStringificationFlags flags)
    {
        var headString = head.ToString(flags);
        var bodyString = body.ToString(flags);

        if (IsBraceExpression)
        {
            return $"{{ {headString}: {bodyString} }}";
        }
        else if (flags.HasFlag(SyntaxNodeStringificationFlags.ParenBodies))
        {
            return $"{token} {headString}: ({bodyString})";
        }
        else
        {
            return $"{token} {headString}: {bodyString}";
        }
    }

    public override InputToken GetRepresentativeToken() => token;
}
