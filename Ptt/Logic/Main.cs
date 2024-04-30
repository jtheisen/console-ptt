namespace Ptt;

public class Symbol
{
    public required String Name { get; set; }

    public required String Id { get; init; }
}

public class Term : IEquatable<Term>
{
    public required String Id { get; init; }

    public Boolean Equals(Term? other) => Id == other?.Id;

    public override Int32 GetHashCode() => Id.GetHashCode();

    public override Boolean Equals(Object? obj) => Equals(obj as Term);
}

public class Chain : Term
{
    public required (Term, Operator)[] Items { get; init; }
}

public class Quantization : Term
{
    public required Symbol Symbol { get; init; }

    public required Term Head { get; init; }

    public required Term Body { get; init; }
}

public class Atom : Term
{
    public required Symbol Symbol { get; init; }
}


public class ContextBuilder
{
    TestContext guide = new TestContext();

    Dictionary<String, Symbol> symbols = new Dictionary<String, Symbol>();

    Symbol GetSymbol(ParsedAtom atom)
    {
        var name = atom.token.TokenString;

        if (symbols.TryGetValue(name, out var symbol))
        {
            return symbol;
        }
        else
        {
            throw Error(atom.token, $"Can't find definition for '{name}'");
        }
    }

    Symbol AddSymbol(ParsedAtom atom)
    {
        var name = atom.token.TokenString;

        var symbol = new Symbol { Id = name, Name = name };

        if (symbols.TryAdd(name, symbol))
        {
            return symbol;
        }
        else
        {
            throw Error(atom.token, "Symbol name conflicts with existing symbol name");
        }
    }

    void RemoveSymbol(Symbol symbol)
    {
        if (!symbols.Remove(symbol.Name))
        {
            throw new Exception($"Can't remove nonexisting symbol with name '{symbol.Name}'");
        }
    }

    Atom Create(ParsedAtom atom)
    {
        return new Atom { Id = atom.token.TokenString, Symbol = GetSymbol(atom) };
    }

    Chain Create(ParsedChain chain)
    {
        var constituents = chain.constituents;

        var n = constituents.Count;

        if (n < 2)
        {
            throw new Exception("Assertion failure: Expected to have at least two operands");
        }

        var items = new (Term term, Operator op)[n];

        Operator? defaultOp = null;

        for (var i = n - 1; i >= 0; --i)
        {
            var (term, opName) = constituents[i];
            ref var item = ref items[i];

            Operator? op;

            if (i > 0 || opName)
            {
                if (!opName)
                {
                    throw Error(chain.GetRepresentativeToken(), $"Chain item #{i} has no operator token");
                }
                else if (!guide.TryGetOperator(opName.TokenString, out op))
                {
                    throw Error(opName, $"Can't resolve operator '{opName.TokenString}'");
                }
                else
                {
                    defaultOp = op.Groupoid.DefaultOp;
                }
            }
            else
            {
                if (defaultOp is null)
                {
                    throw new Exception("Assertion failure: Expected to have a default operator by now");
                }

                op = defaultOp;
            }

            item.term = CreateGeneral(term);
            item.op = op;
        }

        // FIXME: id
        return new Chain { Id = "", Items = items };
    }

    Quantization Create(ParsedQuantization quantization)
    {
        if (quantization.head is ParsedChain syntaxTerm)
        {
            var syntaxHead = syntaxTerm;

            if (syntaxTerm.precedence < 0)
            {
                var firstNonCommaI = syntaxTerm.constituents.FindIndex(c => c.op.cls != InputCharClass.Comma);

                if (firstNonCommaI >= 0)
                {
                    var firstNonCommaOp = syntaxTerm.constituents[firstNonCommaI].op;

                    throw Error(firstNonCommaOp, "Only commas are allowed to group multiple relationships together");
                }

                var firstConstituent = syntaxTerm.constituents[0];

                if (firstConstituent.item is ParsedChain innerRelationship)
                {
                    syntaxTerm = innerRelationship;
                }
                else
                {
                    throw Error(firstConstituent.op, "Preceding term must be a relationship");
                }
            }

            if (syntaxTerm.precedence != 0)
            {
                throw Error(syntaxTerm.constituents[0].op, "Term must be a relation");
            }
            else if (syntaxTerm.constituents.Count > 2)
            {
                throw Error(syntaxTerm.constituents[0].op, "Term must be a simple relation");
            }
            
            if (syntaxTerm.constituents[0].item is ParsedAtom atom)
            {
                var symbol = AddSymbol(atom);

                try
                {
                    var head = CreateGeneral(syntaxHead);

                    var body = CreateGeneral(quantization.body);

                    var id = $"{quantization.token.TokenString}{head.Id}:{body.Id}";

                    return new Quantization { Id = id, Symbol = symbol, Head = head, Body = body };
                }
                finally
                {
                    RemoveSymbol(symbol);
                }
            }
            else
            {
                throw Error(syntaxTerm.constituents[1].op, "Left side of first relationship must be the to-be-quantized symbol");
            }
        }
        else
        {
            throw Error(quantization.GetRepresentativeToken(), "Body of quantization must be a relationship or list of relationships");
        }

        // Make compiler happy.
        throw new Exception();
    }

    Term CreateGeneral(ParsedResult source) => source switch
    {
        ParsedAtom atom => Create(atom),
        ParsedQuantization quantization => Create(quantization),
        ParsedChain chain => Create(chain),
        _ => throw new Exception($"Unexpected syntax node type {source.GetType()}")
    };

    Exception Error(InputToken token, String message)
    {
        return new ParsingException(token.GetContextMessage(message));
    }
}

public static class Extensions
{
    
}