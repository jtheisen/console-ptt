using System.Text;

namespace Ptt;

public class Symbol
{
    public required String Name { get; set; }

    public required String Id { get; init; }
}

public abstract class Term : IEquatable<Term>
{
    String? id;

    public String Id
    {
        get
        {
            if (id is null)
            {
                var writer = new SmartStringWriter();
                Render(writer, useIds: true);
                id = writer.GetResult();
            }
            return id;
        }
    }

    Int32? estimatedRenderLength;

    public Int32 EstimatedRenderLength
    {
        get
        {
            if (estimatedRenderLength is null)
            {
                estimatedRenderLength = EstimateRenderLength();
            }

            return estimatedRenderLength.Value;
        }
    }

    public Boolean Equals(Term? other) => Id == other?.Id;

    public override Int32 GetHashCode() => Id.GetHashCode();

    public override Boolean Equals(Object? obj) => Equals(obj as Term);

    public override String ToString()
    {
        var writer = new SmartStringWriter();
        Write(writer, useIds: false);
        return writer.GetResult();
    }

    public void Write(SmartStringWriter writer, Boolean useIds)
    {
        if (useIds)
        {
            writer.Write(Id);
        }
        else
        {
            Render(writer, useIds);
        }
    }

    protected abstract Int32 EstimateRenderLength();

    protected abstract void Render(SmartStringWriter writer, Boolean useIds);
}

public class ChainTerm : Term
{
    public required Groupoid Groupoid { get; init; }

    public required (Term term, Operator op)[] Items { get; init; }

    protected override Int32 EstimateRenderLength()
    {
        return Items.Sum(i => i.term.EstimatedRenderLength + i.op.Name.Length + 2);
    }

    protected override void Render(SmartStringWriter writer, Boolean useIds)
    {
        var n = Items.Length;

        var ownPrecendence = Groupoid.Precedence;

        for (var i = 0; i < n; i++)
        {
            var (term, op) = Items[i];

            var needParens = ownPrecendence >= op.Groupoid.Precedence;

            writer.Break(relativeNesting: -op.Name.Length);
            if (needParens) writer.Write("(");
            writer.Open(term.EstimatedRenderLength);
            writer.Write(op.Name);
            term.Write(writer, useIds);
            writer.Close();
            if (needParens) writer.Write(")");
        }
    }
}

public class QuantizationTerm : Term
{
    public required Symbol Symbol { get; init; }

    public required Term Head { get; init; }

    public required Term Body { get; init; }

    protected override Int32 EstimateRenderLength() => Symbol.Name.Length + Head.EstimatedRenderLength + Body.EstimatedRenderLength + 2;

    protected override void Render(SmartStringWriter writer, Boolean useIds)
    {
        writer.Write(useIds ? Symbol.Id : Symbol.Name);

        writer.WriteSpace();

        writer.Open(Head.EstimatedRenderLength);
        Head.Write(writer, useIds);
        writer.Close();

        writer.Write(":");

        writer.Open(Body.EstimatedRenderLength);
        Body.Write(writer, useIds);
        writer.Close();
    }
}

public class AtomTerm : Term
{
    public required Symbol Symbol { get; init; }

    protected override Int32 EstimateRenderLength() => Symbol.Name.Length;

    protected override void Render(SmartStringWriter writer, Boolean useIds)
    {
        writer.Write(useIds ? Symbol.Id : Symbol.Name);
    }
}


public class ContextBuilder
{
    TestContext guide = new TestContext();

    Dictionary<String, Symbol> symbols = new Dictionary<String, Symbol>();

    Symbol GetSymbol(SyntaxAtom atom)
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

    Symbol AddSymbol(SyntaxAtom atom, Int32 quantizationDepth)
    {
        var name = atom.token.TokenString;

        var symbol = new Symbol { Id = $"x{quantizationDepth}", Name = name };

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

    AtomTerm Create(SyntaxAtom atom)
    {
        var symbol = GetSymbol(atom);

        return new AtomTerm { Symbol = symbol };
    }

    ChainTerm Create(SyntaxChain chain)
    {
        var constituents = chain.constituents;

        var n = constituents.Count;

        if (n < 2)
        {
            throw new Exception("Assertion failure: Expected to have at least two operands");
        }

        var items = new (Term term, Operator op)[n];

        Groupoid? groupoid = null;
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
                else if (defaultOp is null)
                {
                    groupoid = op.Groupoid;
                    defaultOp = groupoid.DefaultOp;
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

        if (groupoid is null)
        {
            throw new Exception("Assertion failure: Expected to have a groupoid by now");
        }

        return new ChainTerm { Items = items, Groupoid = groupoid };
    }

    QuantizationTerm Create(SyntaxQuantization quantization)
    {
        if (quantization.head is SyntaxChain syntaxTerm)
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

                if (firstConstituent.item is SyntaxChain innerRelationship)
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
            
            if (syntaxTerm.constituents[0].item is SyntaxAtom atom)
            {
                var symbol = AddSymbol(atom, quantization.body.quantizationDepth);

                try
                {
                    var head = CreateGeneral(syntaxHead);

                    var body = CreateGeneral(quantization.body);

                    return new QuantizationTerm { Symbol = symbol, Head = head, Body = body };
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

    Term CreateGeneral(SyntaxNode source) => source switch
    {
        SyntaxAtom atom => Create(atom),
        SyntaxQuantization quantization => Create(quantization),
        SyntaxChain chain => Create(chain),
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