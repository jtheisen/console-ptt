namespace Ptt;

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
