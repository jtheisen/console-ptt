using System.Diagnostics.CodeAnalysis;

namespace Ptt;

public interface IParserGuide
{
    Double GetPrecedence(ReadOnlySpan<Char> op);
    Boolean IsSymbolLetter(Char c, out Double precedence);
    Boolean IsQuantizationSymbol(ReadOnlySpan<Char> op, out Double precedence);
    Boolean IsBooleanQuantizationSymbol(ReadOnlySpan<Char> op);
}

public class Magma
{
    public Operator DefaultOp { get; }

    public Operator? NegatedOp { get; }

    public required Boolean IsAssociative { get; init; }

    public required Boolean IsUnordered { get; init; }

    public Double Precedence { get; set; }

    public IEnumerable<Operator> GetOperators()
    {
        yield return DefaultOp;
        if (NegatedOp is not null)
        {
            yield return NegatedOp;
        }
    }

    public Magma(String defaultOp, String? negatedOp = null)
    {
        DefaultOp = new Operator { Magma = this, Name = defaultOp };

        if (negatedOp is not null)
        {
            NegatedOp = new Operator { Magma = this, Name = negatedOp };
        }
    }
}

public class Operator
{
    public required String Name { get; init; }

    public required Magma Magma { get; init; }
}

public interface IContext
{
    Boolean TryGetOperator(String name, [NotNullWhen(true)] out Operator? op);
}

public class TestContext : IParserGuide, IContext
{
    static String BooleanSymbols = "⇒⇐⇔ ,∨∧ =";
    static String DomainSymbols = "+- */ ^";
    static String RelationSymbols = "<>≤≥ ⊂⊃ ⊆⊇ ∊∍ ∈∋ ϵ";

    static String QuantizationCharacters = "∀∃";

    static String SymbolLetters =
        "∀∃∑∏";
    static String SymbolLetterPrecedencePairings =
        "∨∨+*";

    Dictionary<Char, Double> precedences;

    Dictionary<String, Operator> operators;

    public TestContext()
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

        operators = new Dictionary<String, Operator>();

        AddMagma(new Magma("*", "/") { IsAssociative = true, IsUnordered = true });
        AddMagma(new Magma("+", "-") { IsAssociative = true, IsUnordered = true });
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

    // End of methods for parsing

    void AddMagma(Magma magma)
    {
        Double? precedence = null;

        foreach (var op in magma.GetOperators())
        {
            var p = GetPrecedence(op.Name.AsSpan());

            if (precedence is Double existingPrecedence)
            {
                if (existingPrecedence != p)
                {
                    throw new Exception("Different precedences for magma operators");
                }
            }
            else
            {
                precedence = magma.Precedence = p;
            }

            operators[op.Name] = op;
        }
    }

    public Boolean TryGetOperator(String name, [NotNullWhen(true)] out Operator? op)
    {
        return operators.TryGetValue(name, out op);
    }
}
