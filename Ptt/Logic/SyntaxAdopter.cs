namespace Ptt;

public class SyntaxAdopter
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

    AtomExpression CreateAtom(SyntaxAtom atom)
    {
        var symbol = GetSymbol(atom);

        return new AtomExpression { Symbol = symbol, Precedence = Double.MaxValue };
    }

    struct IntermediateSequenceItem
    {
        public InputToken opToken;
        public Expression? expr;
        public OperatorConfiguration? configuration;
        public Boolean inverted;
        public Boolean conversed;
        public Boolean negated;
    }

    Expression UnwrapAnnotation(Expression expr)
    {
        if (TryGetAnnotation(expr, out var lhs, out var tail))
        {
            lhs.Annotation = new RelationshipAnnotation
            {
                Tail = tail
            };

            return lhs;
        }
        else
        {
            expr.Annotation = Annotation.Default;

            return expr;
        }
    }

    Boolean TryGetAnnotation(Expression expr, [NotNullWhen(true)] out Expression? lhs, out RelationshipTail tail)
    {
        if (expr is RelationalExpression relational)
        {
            if (relational.Tail.Length > 1)
            {
                throw Error(expr.RepresentativeToken, "Annotations can't be relational sequences with more than two operands");
            }

            var relationshipTail = relational.Tail[0];

            lhs = relational.LeftExpression;
            tail = relationshipTail;

            return true;
        }
        else
        {
            lhs = null;
            tail = default;

            return false;
        }
    }

    SequenceExpression CreateSequence(SyntaxChain chain)
    {
        var constituents = chain.constituents;

        var n = constituents.Count;

        var items = new IntermediateSequenceItem[n];

        Functional? functional = null;

        Int32 haveFunctional = 0, haveRelation = 0;

        for (var i = 0; i < n; ++i)
        {
            var (expr, opToken) = constituents[i];
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

            item.expr = Create(expr);
        }

        if (haveFunctional + haveRelation > 1)
        {
            throw Error(chain.GetRepresentativeToken(), "Sequence mixes relations with functional operators");
        }

        if (functional is not null)
        {
            var operands = new (Boolean inverted, Expression expr)[n];

            for (var i = 0; i < n; ++i)
            {
                ref var item = ref items[i];
                ref var target = ref operands[i];

                if (!functional.IsBoolean && item.expr is RelationalExpression relationalExpression)
                {
                    // A relational expression inside a domain functional one is an annotation

                    if (relationalExpression.Tail.Length > 1)
                    {
                        throw Error(relationalExpression.RepresentativeToken, "Annotations can't be relational sequences with more than two operands");
                    }

                    item.expr = UnwrapAnnotation(item.expr);

                    if (item.expr is RelationalExpression)
                    {
                        throw Error(item.expr.RepresentativeToken, "Relational sequences can't be nested twice");
                    }
                }

                target.expr = item.expr!;
                target.inverted = item.inverted;
            }

            return new FunctionalExpression { Functional = functional, OwnOperands = operands, Precedence = functional.Precedence };
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

                target.rhs = item.expr!;
                target.relation = relation;
                target.flags = new RelationshipFlags { negated = item.negated, conversed = item.conversed };

                if (precedence == Double.MinValue)
                {
                    precedence = relation.Precedence;
                }
                else if (precedence != relation.Precedence)
                {
                    throw Error(item.opToken, "Boolean and domain relations can't be mixed in the same chain");
                }
            }

            return new RelationalExpression
            {
                LeftExpression = items[0].expr!,
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

    QuantizationExpression CreateQuantization(SyntaxQuantization quantization)
    {
        if (!guide.IsQuantizationSymbol(quantization.token.TokenSpan, out var precedence))
        {
            throw Error(quantization.token, "Symbol is not a quantization");
        }

        if (quantization.head is not SyntaxChain syntaxExpression)
        {
            throw Error(quantization.GetRepresentativeToken(), "Body of quantization must be a relationship or list of relationships");
        }

        var syntaxHead = syntaxExpression;

        if (syntaxExpression.precedence < 0)
        {
            var firstNonCommaI = syntaxExpression.constituents.FindIndex(c => c.op.TokenSpan[0] != ',');

            if (firstNonCommaI >= 0)
            {
                var firstNonCommaOp = syntaxExpression.constituents[firstNonCommaI].op;

                throw Error(firstNonCommaOp, "Only commas are allowed to group multiple relationships together");
            }

            var firstConstituent = syntaxExpression.constituents[0];

            if (firstConstituent.item is SyntaxChain innerRelationship)
            {
                syntaxExpression = innerRelationship;
            }
            else
            {
                throw Error(firstConstituent.op, "Preceding expression must be a relationship");
            }
        }

        if (syntaxExpression.precedence != 0)
        {
            throw Error(syntaxExpression.constituents[0].op, "Expression must be a relation");
        }
        else if (syntaxExpression.constituents.Count > 2)
        {
            throw Error(syntaxExpression.constituents[0].op, "Expression must be a simple relation");
        }

        if (syntaxExpression.constituents[0].item is SyntaxAtom atom)
        {
            var symbol = AddSymbol(atom, quantization.body.quantizationDepth);

            try
            {
                var head = Create(syntaxHead);

                var body = Create(quantization.body);

                if (precedence > 0 && body is RelationalExpression relationalExpression)
                {
                    // Relations in a domain quantification's body are annotations.

                    body = UnwrapAnnotation(relationalExpression);
                }

                return new QuantizationExpression
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
            throw Error(syntaxExpression.constituents[1].op, "Left side of first relationship must be the to-be-quantized symbol");
        }
    }

    public Expression Create(SyntaxNode source)
    {
        Expression result = source switch
        {
            SyntaxAtom atom => CreateAtom(atom),
            SyntaxQuantization quantization => CreateQuantization(quantization),
            SyntaxChain chain => CreateSequence(chain),
            _ => throw new Exception($"Unexpected syntax node type {source.GetType()}")
        };

        if (source.isAnnotation)
        {
            // This is the specifically marked version, likely without a relationship.

            result = UnwrapAnnotation(result);
        }

        return result;
    }

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
