using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Ptt;


public class Rule
{
    public static Rule TrivialRule { get; }

    // level 1 universial symbols with prerequisite relationships
    // level 2 existential symbols with relationships
    // relationship

    Dictionary<Symbol, RelationshipTail> universials;
    Dictionary<Symbol, RelationshipTail> existentials;

    public required RelationalExpression Relationship { get; init; }


}

public class UnifierGuide
{

}

public class Unifier
{
    Rule rule;

    Dictionary<Symbol, Expression> substitutions;

    Boolean TrySubsitute(AtomExpression atom, Expression target)
    {
        throw new NotImplementedException();
    }

    public Unifier()
    {
        substitutions = new Dictionary<Symbol, Expression>();
        rule = Rule.TrivialRule;
    }

    public Expression? Unify(Rule rule, Expression source, Expression target)
    {
        this.rule = rule;

        try
        {
            return UnifyCore(source, target);
        }
        finally
        {
            this.rule = Rule.TrivialRule;
            substitutions.Clear();
        }
    }

    RelationshipAnnotation GetAnnotation(Expression expression)
    {
        throw new NotImplementedException();
    }

    Boolean HaveSameLengthAndNegations(Span<(Boolean inverted, Expression expr)> lhs, Span<(Boolean inverted, Expression expr)> rhs)
    {
        var n = lhs.Length;

        if (rhs.Length != n) return false;

        for (var i = 0; i < n; ++i)
        {
            if (lhs[i].inverted != rhs[i].inverted) return false;
        }

        return true;
    }

    public Expression? UnifyCore(Expression source, Expression target)
    {
        if (source is AtomExpression atom)
        {
            if (substitutions.TryGetValue(atom.Symbol, out var substitution))
            {
                return UnifyCore(substitution, target);
            }
            else if (target is AtomExpression targetAtom && atom.Symbol == targetAtom.Symbol)
            {
                return targetAtom;
            }
            else if (TrySubsitute(atom, target))
            {
                return target;
            }
            else
            {
                return null;
            }
        }
        else if (source is QuantizationExpression quantization)
        {
            if (target is not QuantizationExpression targetQuantization ||
                quantization.QuantizationSymbol != targetQuantization.QuantizationSymbol)
            {
                return null;
            }

            throw new NotImplementedException();
        }
        else if (source is FunctionalExpression functionalExpr)
        {
            if (target is not FunctionalExpression targetFunctionalExpr ||
                functionalExpr.Functional != targetFunctionalExpr.Functional)
            {
                return null;
            }

            var functional = functionalExpr.Functional;

            /* 1. Constant case (no annotations):
             *    All operands match perfectly. This works also for the unordered
             *    case as they come sorted.
             * 2. Mixed case:
             *    One operand is annotated, substituted and the rest must match perfectly.
             * 3. Complete case:
             *    The functional expression is binary in two annotated expressions,
             *    both are substituted.
             */

            // ordered: check if subsequence
            // unordered: check if subset

            var ops = functionalExpr.Operands;
            var targetOps = targetFunctionalExpr.Operands;

            var annotations = (
                from op in ops
                let annotation = op.expr.Annotation
                where annotation is not null
                select (op, annotation)
            ).ToArray();

            if (annotations.Length == 0)
            {
                if (!HaveSameLengthAndNegations(ops.AsSpan(), targetOps.AsSpan()))
                {
                    return null;
                }

                
            }

            if (annotations.Length > 2)
            {
                throw new AssertionException("Did not expect to have more than two annotations on the same functional expression");
            }
            else if (annotations.Length == 2 && ops.Length > 2)
            {
                throw new AssertionException("Two annotations on the same functional expression must contain the entire expression");
            }            

            if (functional.IsCommutative)
            {
                // We allow unification if and only if either
                // - all constituents are constant, or
                // - a part is marked in the target, or
                // - a part and its complement is marked in the target

                // ...

                throw new NotImplementedException();
            }
            else
            {
                // We allow unification if and only if either
                // - all constituents are constant, or
                // - a prefix or suffix is marked in the target, or
                // - a contiguous part and its contiguous complement is marked in the target

                throw new NotImplementedException();
            }
        }
        else if (source is RelationalExpression relationalExpression)
        {
            if (target is not RelationalExpression targetRelationshipExpression)
            {
                return null;
            }

            if (!relationalExpression.DeconstructRelationship(out var sourceLhs, out var sourceRelation, out var sourceFlags, out var sourceRhs) ||
                !relationalExpression.DeconstructRelationship(out var targetLhs, out var targetRelation, out var targetFlags, out var targetRhs))
            {
                return null;
            }

            if (sourceRelation.Name != targetRelation.Name)
            {
                return null;
            }

            if (sourceFlags.negated != targetFlags.negated)
            {
                return null;
            }

            if (sourceFlags.conversed != targetFlags.conversed)
            {
                var temp = sourceLhs;
                sourceLhs = sourceRhs;
                sourceRhs = temp;
            }

            Expression? resultLhs, resultRhs;

            if ((resultLhs = UnifyCore(sourceLhs, targetLhs)) is not null &&
                (resultRhs = UnifyCore(sourceRhs, targetRhs)) is not null)
            {
                return new RelationalExpression
                {
                    LeftExpression = resultLhs,
                    Tail = [ new RelationshipTail
                    {
                        flags = new RelationshipFlags
                        {
                            negated = sourceFlags.negated,
                            conversed = targetFlags.conversed
                        },
                        relation = sourceRelation,
                        rhs = resultRhs
                    }],
                    IsBoolean = sourceRelation.IsBoolean,
                    Precedence = sourceRelation.Precedence
                };
            }

            // FIXME: backtrack in the other direction

            return null;
        }
        else if (source is RelationalExpression)
        {
            // Unifying multi-relational expressions is not supported

            return null;
        }
        else
        {
            throw new AssertionException($"Unexpected expressionh type {source.GetType()}");
        }
    }
}
