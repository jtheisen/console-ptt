using System.Diagnostics.CodeAnalysis;

namespace Ptt;

public interface IParserGuide
{
    Double GetOperatorPrecedence(InputToken op);
    Boolean IsSymbolLetter(Char c, out Double precedence);
    Boolean IsQuantizationSymbol(ReadOnlySpan<Char> op, out Double precedence);
    Boolean IsBooleanQuantizationSymbol(ReadOnlySpan<Char> op);
}

public class Symbol
{
    public required String Name { get; set; }

    public required String Id { get; init; }
}

public class OperatorConfiguration
{
    public Double Precedence { get; set; }

    public Boolean IsBoolean => Precedence < 0;
}

public class Functional : OperatorConfiguration
{
    public String DefaultOp { get; }

    public String? InvertedOp { get; }

    public required Boolean IsAssociative { get; init; }

    public required Boolean IsCommutative { get; init; }

    public String GetOpName(Boolean inverted)
        => inverted ? InvertedOp ?? throw new Exception("No inverted operator") : DefaultOp;

    public IEnumerable<String> GetOperators()
    {
        yield return DefaultOp;
        if (InvertedOp is not null)
        {
            yield return InvertedOp;
        }
    }

    public Functional(String defaultOp, String? invertedOp = null)
    {
        DefaultOp = defaultOp;
        InvertedOp = invertedOp;
    }
}

public enum RelationFlags
{
    Nothing = 0,

    Transitive = 1,
    Reflexive = 2,
    Symmetric = 4,

    Equivalence = Transitive | Reflexive | Symmetric
}

public class Relation : OperatorConfiguration
{
    public required String Name { get; init; }

    public required String Reversed { get; init; }

    public required RelationFlags Flags { get; init; }

    public String GetName(RelationshipFlags flags)
    {
        return GetName(flags.conversed, flags.negated);
    }

    public String GetName(Boolean conversed, Boolean negated)
    {
        if (negated)
        {
            return $"!{GetName(conversed, false)}";
        }
        else
        {
            return conversed ? Reversed : Name;
        }
    }
}

public struct RelationshipFlags
{
    public Boolean conversed;

    public Boolean negated;

    public Boolean target;
}

public struct RelationshipTail
{
    public Relation relation;

    public RelationshipFlags flags;

    public Expression rhs;
}

public interface IAdoptionGuide
{
    Boolean TryResolveOperator(String name, [NotNullWhen(true)] ref OperatorConfiguration? configuration);
}

public class TestGuide : IParserGuide, IAdoptionGuide
{
    static String BooleanPrecedenceOrder = "⇒⇐⇔ ,∨∧ =";
    static String DomainPrecedenceOrder = "+- */ ^";
    static String BooleanRelationSymbols = "⇒⇐⇔⇔";
    static String DomainRelationSymbols = "==  <>≤≥  ⊂⊃  ⊆⊇  ∊∍";

    static String SetSymbols = "ℕℤℚℝℂ";

    static String QuantizationCharacters = "∀∃";

    static String SymbolLetters =
        "∀∃∑∏";
    static String SymbolLetterPrecedencePairings =
        "∨∨+*";

    Dictionary<Char, Double> precedences;

    Dictionary<String, OperatorConfiguration> operators;

    Dictionary<String, Symbol> symbols;

    public Double BooleanRelationPrecedence { get; }

    public TestGuide()
    {
        precedences = new Dictionary<Char, Double>();

        operators = new Dictionary<String, OperatorConfiguration>();

        symbols = new Dictionary<String, Symbol>();

        {
            var precedence = 0;
            foreach (var symbolGroup in DomainPrecedenceOrder.Split())
            {
                ++precedence;

                foreach (var chr in symbolGroup)
                {
                    precedences[chr] = precedence;
                }
            }
        }

        {
            var symbolGroups = BooleanPrecedenceOrder.Split();
            for (var i = 0; i < symbolGroups.Length; ++i)
            {
                foreach (var chr in symbolGroups[i])
                {
                    precedences[chr] = -symbolGroups.Length + i;
                }
            }
        }

        AddFunctional(new Functional("*", "/") { IsAssociative = true, IsCommutative = true });
        AddFunctional(new Functional("+", "-") { IsAssociative = true, IsCommutative = true });

        BooleanRelationPrecedence = GetOperatorPrecedence('⇒');

        AddRelations(BooleanRelationSymbols, BooleanRelationPrecedence);
        AddRelations(DomainRelationSymbols, 0);

        AddSymbols();
    }

    Double? GetOperatorPrecedenceOrNot(Char chr)
    {
        if (DomainRelationSymbols.IndexOf(chr) != -1)
        {
            return 0;
        }

        if (precedences.TryGetValue(chr, out var precedence))
        {
            return precedence;
        }

        return null;
    }

    Double GetOperatorPrecedence(Char chr)
    {
        return GetOperatorPrecedenceOrNot(chr) ?? throw new AssertionException("Unknown operator");
    }

    public Double GetOperatorPrecedence(String op)
    {
        if (op.Length == 1)
        {
            var chr = op[0];

            if (GetOperatorPrecedenceOrNot(chr) is Double precedence)
            {
                return precedence;
            }
        }

        throw new Exception($"Unknown operator {new String(op)}");
    }

    public Double GetOperatorPrecedence(InputToken inputToken)
    {
        if (!inputToken.TryGetOperatorName(out var op, out _))
        {
            throw new AssertionException("Token not an operator");
        }

        if (op.Length == 1)
        {
            var chr = op[0];

            if (GetOperatorPrecedenceOrNot(chr) is Double precedence)
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

            if (GetOperatorPrecedenceOrNot(pairing) is Double p)
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

    void AddSymbol(String name)
    {
        symbols[name] = new Symbol { Id = name, Name = name };
    }

    void AddSymbols()
    {
        AddSymbol("A");
        AddSymbol("B");
        AddSymbol("C");
        AddSymbol("D");

        foreach (var symbol in SetSymbols)
        {
            AddSymbol(symbol.ToString());
        }
    }

    public Boolean TryGetSymbol(String name, [NotNullWhen(true)] out Symbol? symbol)
    {
        return symbols.TryGetValue(name, out symbol);
    }

    void AddOperator(String op, OperatorConfiguration configuration)
    {
        if (!operators.TryAdd(op, configuration))
        {
            throw new Exception($"Already have an operator {op} defined");
        }
    }

    void AddFunctional(Functional functional)
    {
        Double? precedence = null;

        foreach (var op in functional.GetOperators())
        {
            var p = GetOperatorPrecedence(op);

            if (precedence is Double existingPrecedence)
            {
                if (existingPrecedence != p)
                {
                    throw new Exception("Different precedences for functional operators");
                }
            }
            else
            {
                precedence = functional.Precedence = p;
            }

            AddOperator(op, functional);
        }
    }

    public Boolean TryResolveOperator(String name, [NotNullWhen(true)] ref OperatorConfiguration? configuration)
    {
        return operators.TryGetValue(name, out configuration);
    }

    void AddRelations(String symbolPairs, Double precedence)
    {
        for (var i = 0; i < symbolPairs.Length; i += 2)
        {
            var c0 = symbolPairs[i];
            var c1 = symbolPairs[i + 1];

            var s0 = c0 == ' ';
            var s1 = c1 == ' ';

            if (s0 != s1)
            {
                throw new Exception("Unexpected relation pair");
            }

            if (s0) continue;

            var f0 = GetFlagsForRelationChar(c0);
            var f1 = GetFlagsForRelationChar(c1);

            if (f0 != f1)
            {
                throw new Exception("Unexpectedly got different flags for parts of relationship character pair");
            }

            var n0 = c0.ToString();
            var n1 = c1.ToString();

            var r = new Relation { Flags = f0, Name = n0, Reversed = n1, Precedence = precedence };

            AddOperator(n0, r);

            if (n0 != n1)
            {
                AddOperator(n1, r);
            }
        }
    }

    static RelationFlags GetFlagsForRelationChar(Char c)
    {
        switch (c)
        {
            case '⇔':
            case '=':
                return RelationFlags.Equivalence;
            case '<':
            case '>':
            case '⊂':
            case '⊃':
                return RelationFlags.Transitive;
            case '⇒':
            case '⇐':
            case '≤':
            case '≥':
            case '⊆':
            case '⊇':
                return RelationFlags.Transitive | RelationFlags.Reflexive;
            case '∊':
            case '∍':
                return RelationFlags.Nothing;
            default:
                throw new Exception($"Unknown relation character '{c}'");
        }
    }
}
