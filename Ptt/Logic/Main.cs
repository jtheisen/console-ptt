namespace Ptt;

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

    public required Double Precedence { get; init; }

    public TermAnnotation? Annotation { get; set; }

    public InputToken? RepresentativeToken { get; set; }

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

public class TermAnnotation
{
    public RelationshipTail Tail { get; init; }
}

public struct OperandsComparer : IComparer<(Boolean inverted, Term term)>
{
    public Int32 Compare((Boolean inverted, Term term) x, (Boolean inverted, Term term) y)
    {
        var dx = x.inverted ? 1 : 0;
        var dy = y.inverted ? 1 : 0;

        var c0 = dx - dy;

        if (c0 != 0) return c0;

        var c1 = String.Compare(x.term.Id, y.term.Id);

        return c1;
    }
}

public abstract class SequenceTerm : Term
{
    public abstract IEnumerable<(String? op, Term term)> Items { get; }

    public abstract Boolean IsUnordered { get; }

    protected override Int32 EstimateRenderLength()
    {
        return Items.Sum(i => i.term.EstimatedRenderLength + (i.op?.Length ?? 0) + 2);
    }
}

public class RelationalTerm : SequenceTerm
{
    public required Term FirstTerm { get; init; }

    public required RelationshipTail[] Tail { get; init; }

    public required Boolean IsBoolean { get; init; }

    public Boolean IsRelation => Tail.Length == 1;

    public override Boolean IsUnordered => IsRelation && Tail[0].relation.Flags.HasFlag(RelationFlags.Symmetric);

    public override IEnumerable<(String? op, Term term)> Items
    {
        get
        {
            yield return (null, FirstTerm);

            foreach (var i in Tail)
            {
                yield return (i.relation.GetName(i.conversed, i.negated), i.rhs);
            }
        }
    }

    protected override void Render(SmartStringWriter writer, Boolean useIds)
    {
        var ownPrecendence = Precedence;

        var items = Items;

        foreach (var item in items)
        {
            var (op, term) = item;

            var needParens = ownPrecendence >= term.Precedence;

            writer.Break(relativeNesting: -op?.Length ?? 0);
            if (needParens) writer.Write("(");
            writer.Open(term.EstimatedRenderLength);
            if (op is not null) writer.Write(op);
            term.Write(writer, useIds);
            writer.Close();
            if (needParens) writer.Write(")");
        }
    }
}

public class FunctionalTerm : SequenceTerm
{
    public required Functional Functional { get; init; }

    public required (Boolean inverted, Term term)[] Operands { get; init; }

    public override Boolean IsUnordered => Functional.IsCommutative;

    public override IEnumerable<(String? op, Term term)> Items
        => from o in Operands select ((String? op, Term term))(Functional.GetOpName(o.inverted), o.term);

    static OperandsComparer operandsComparer;

    protected override void Render(SmartStringWriter writer, Boolean useIds)
    {
        var ownPrecendence = Precedence;

        var operands = Operands.ToList();

        var functional = Functional;

        if (useIds && IsUnordered)
        {
            operands.Sort(operandsComparer);
        }

        var isSubsequent = false;

        foreach (var operand in operands)
        {
            var (inverted, term) = operand;

            var isCollapsible =
                !inverted &&
                term is FunctionalTerm otherFunctional &&
                Functional == otherFunctional.Functional &&
                Functional.IsAssociative
                ;

            var needParens = !isCollapsible && ownPrecendence >= term.Precedence;

            var op = functional.GetOpName(inverted);

            writer.Break(relativeNesting: -op.Length);

            if (needParens) writer.Write("(");
            writer.Open(term.EstimatedRenderLength);
            if (isSubsequent) writer.Write(op);
            term.Write(writer, useIds);
            writer.Close();
            if (needParens) writer.Write(")");

            isSubsequent = true;
        }
    }
}

public class QuantizationTerm : Term
{
    public required String QuantizationSymbol { get; init; }

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

    Boolean TryGetSymbol(String name, [NotNullWhen(true)] out Symbol? symbol)
    {
        return guide.TryGetSymbol(name, out symbol) || symbols.TryGetValue(name, out symbol);
    }


    Symbol GetSymbol(SyntaxAtom atom)
    {
        var name = atom.token.TokenString;

        if (TryGetSymbol(name, out var symbol))
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

        if (TryGetSymbol(name, out var _))
        {
            throw Error(atom.token, "Symbol name conflicts with existing symbol name");
        }
        else
        {
            var symbol = new Symbol { Id = $"x{quantizationDepth}", Name = name };

            symbols.Add(name, symbol);

            return symbol;
        }
    }

    void RemoveSymbol(Symbol symbol)
    {
        if (!symbols.Remove(symbol.Name))
        {
            throw new Exception($"Can't remove nonexisting symbol with name '{symbol.Name}'");
        }
    }

    AtomTerm CreateAtom(SyntaxAtom atom)
    {
        var symbol = GetSymbol(atom);

        return new AtomTerm { Symbol = symbol, Precedence = Double.MaxValue };
    }

    struct IntermediateSequenceItem
    {
        public InputToken opToken;
        public Term? term;
        public OperatorConfiguration? configuration;
        public Boolean inverted;
        public Boolean conversed;
        public Boolean negated;
    }

    Term UnwrapAnnotation(Term term)
    {
        if (TryGetAnnoation(term, out var lhs, out var tail))
        {
            lhs.Annotation = new TermAnnotation
            {
                Tail = tail
            };

            return lhs;
        }
        else
        {
            throw new AssertionException("Expected an annotation to unwrap");
        }
    }

    Boolean TryGetAnnoation(Term term, [NotNullWhen(true)] out Term? lhs, out RelationshipTail tail)
    {
        if (term is RelationalTerm relational)
        {
            var relationshipTail = relational.Tail;

            if (relationshipTail.Length != 1)
            {
                throw Error(term.RepresentativeToken, "Annotations can't have multiple relations");
            }

            lhs = relational.FirstTerm;
            tail = relationshipTail[0];

            return true;
        }
        else
        {
            lhs = null;
            tail = default;

            return false;
        }
    }

    // Do we still need this?
    SequenceTerm UnwrapSequenceAnnotations(SequenceTerm sequence)
    {
        // This function modifies the sequence.

        if (sequence is FunctionalTerm functional)
        {
            var operands = functional.Operands;

            var n = operands.Length;

            for (var i = 0; i < n; ++i)
            {
                ref var operand = ref operands[i];

                if (TryGetAnnoation(operand.term, out var lhs, out var tail))
                {
                    if (operand.inverted)
                    {
                        throw Error(operand.term.RepresentativeToken, "Annoation can't be made on inverted operator");
                    }

                    operands[i].term = lhs;
                }
            }
        }

        return sequence;
    }

    SequenceTerm CreateSequence(SyntaxChain chain)
    {
        var constituents = chain.constituents;

        var n = constituents.Count;

        var items = new IntermediateSequenceItem[n];

        Functional? functional = null;

        Int32 haveFunctional = 0, haveRelation = 0;

        for (var i = 0; i < n; ++i)
        {
            var (term, opToken) = constituents[i];
            ref var item = ref items[i];

            item.opToken = opToken;

            if (opToken)
            {
                if (!opToken.TryGetOperatorName(out var opName, out item.negated))
                {
                    throw new AssertionException($"Can't get operator name from token {opToken}");
                }

                if (!guide.TryResolveOperator(opName, ref item.configuration))
                {
                    throw Error(opToken, $"Can't resolve operator '{opName}'");
                }
                
                if (item.configuration is Functional someFunctional)
                {
                    if (item.negated)
                    {
                        throw Error(opToken, "Functional operators can't be negated ('!' is invalid here)");
                    }

                    if (functional is not null && !ReferenceEquals(someFunctional, functional))
                    {
                        throw Error(chain.GetRepresentativeToken(), "Sequence uses operators from different functionals");
                    }

                    functional = someFunctional;

                    haveFunctional = 1;

                    if (opName != functional.InvertedOp)
                    {
                        if (opName != functional.DefaultOp)
                        {
                            throw new Exception($"Internal error: Unexpected operator '{opName}'");
                        }
                    }
                    else
                    {
                        item.inverted = true;
                    }
                }
                else if (item.configuration is Relation relation)
                {
                    haveRelation = 1;

                    if (opName != relation.Reversed)
                    {
                        if (opName != relation.Name)
                        {
                            throw new Exception($"Internal error: Unexpected relation '{opName}'");
                        }
                    }
                    else
                    {
                        item.conversed = true;
                    }
                }
                else
                {
                    throw new Exception($"Internal error: Unexpected configuration type '{item.configuration.GetType()}'");
                }
            }

            item.term = Create(term);
        }

        if (haveFunctional + haveRelation > 1)
        {
            throw Error(chain.GetRepresentativeToken(), "Sequence mixes relations with functional operators");
        }

        if (functional is not null)
        {
            var operands = new (Boolean inverted, Term term)[n];

            for (var i = 0; i < n; ++i)
            {
                ref var item = ref items[i];
                ref var target = ref operands[i];

                if (!functional.IsBoolean && item.term is RelationalTerm)
                {
                    // A relational term inside a domain functional one is an annotation

                    item.term = UnwrapAnnotation(item.term);

                    if (item.term is RelationalTerm)
                    {
                        throw Error(item.term.RepresentativeToken, "Relational sequences can't be nested twice");
                    }
                }

                target.term = item.term!;
                target.inverted = item.inverted;
            }

            return new FunctionalTerm { Functional = functional, Operands = operands, Precedence = functional.Precedence };
        }
        else if (haveRelation > 0)
        {
            var tail = new RelationshipTail[n - 1];

            Double precedence = Double.MinValue;

            for (var i = 1; i < n; ++i)
            {
                ref var item = ref items[i];
                ref var target = ref tail[i - 1];

                if (item.configuration is not Relation relation)
                {
                    throw Error(item.opToken, "Operator is not a relation");
                }

                target.rhs = item.term!;
                target.relation = relation;
                target.conversed = item.conversed;

                if (precedence == Double.MinValue)
                {
                    precedence = relation.Precedence;
                }
                else if (precedence != relation.Precedence)
                {
                    throw Error(item.opToken, "Boolean and domain relations can't be mixed in the same chain");
                }
            }

            return new RelationalTerm
            {
                FirstTerm = items[0].term!,
                Tail = tail,
                Precedence = precedence,
                IsBoolean = precedence == guide.BooleanRelationPrecedence
            };
        }
        else
        {
            throw new AssertionException("Have no sequence flavor");
        }
    }

    QuantizationTerm CreateQuantization(SyntaxQuantization quantization)
    {
        if (!guide.IsQuantizationSymbol(quantization.token.TokenSpan, out var precedence))
        {
            throw Error(quantization.token, "Symbol is not a quantization");
        }

        if (quantization.head is not SyntaxChain syntaxTerm)
        {
            throw Error(quantization.GetRepresentativeToken(), "Body of quantization must be a relationship or list of relationships");
        }

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
                var head = Create(syntaxHead);

                var body = Create(quantization.body);

                if (precedence > 0 && body is RelationalTerm relationalTerm)
                {
                    // Relations in a domain quantification's body are annotations.

                    body = UnwrapAnnotation(relationalTerm);
                }

                return new QuantizationTerm
                {
                    QuantizationSymbol = quantization.token.TokenString,
                    Precedence = precedence,
                    Symbol = symbol,
                    Head = head,
                    Body = body
                };
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

    public Term Create(SyntaxNode source) => source switch
    {
        SyntaxAtom atom => CreateAtom(atom),
        SyntaxQuantization quantization => CreateQuantization(quantization),
        SyntaxChain chain => CreateSequence(chain),
        _ => throw new Exception($"Unexpected syntax node type {source.GetType()}")
    };

    Exception Error(InputToken? tokenOrNot, String message)
    {
        if (tokenOrNot is InputToken token)
        {
            return new ParsingException(token.GetContextMessage(message));
        }
        else
        {
            return new ParsingException(message);
        }
    }
}
