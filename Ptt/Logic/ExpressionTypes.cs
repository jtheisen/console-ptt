﻿namespace Ptt;

public abstract class Expression : IEquatable<Expression>
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

    public Annotation? Annotation { get; set; }

    public InputToken? RepresentativeToken { get; set; }

    public Boolean Equals(Expression? other) => Id == other?.Id;

    public override Int32 GetHashCode() => Id.GetHashCode();

    public override Boolean Equals(Object? obj) => Equals(obj as Expression);

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

public class Annotation
{
    public static readonly Annotation Default = new Annotation();
}

public class RelationshipAnnotation : Annotation
{
    public RelationshipTail Tail { get; init; }
}

public class AtomExpression : Expression
{
    public required Symbol Symbol { get; init; }

    protected override Int32 EstimateRenderLength() => Symbol.Name.Length;

    protected override void Render(SmartStringWriter writer, Boolean useIds)
    {
        writer.Write(useIds ? Symbol.Id : Symbol.Name);
    }
}

public struct OperandsComparer : IComparer<(Boolean inverted, Expression expr)>
{
    public Int32 Compare((Boolean inverted, Expression expr) x, (Boolean inverted, Expression expr) y)
    {
        var dx = x.inverted ? 1 : 0;
        var dy = y.inverted ? 1 : 0;

        var c0 = dx - dy;

        if (c0 != 0) return c0;

        var c1 = String.Compare(x.expr.Id, y.expr.Id);

        return c1;
    }
}

public abstract class SequenceExpression : Expression
{
    public abstract IEnumerable<(String? op, Expression expr)> GetItems(Boolean useIds);

    public abstract Boolean IsUnordered { get; }

    protected override Int32 EstimateRenderLength()
    {
        return GetItems(false).Sum(i => i.expr.EstimatedRenderLength + (i.op?.Length ?? 0) + 2);
    }

    protected override void Render(SmartStringWriter writer, Boolean useIds)
    {
        var ownPrecendence = Precedence;

        var items = GetItems(useIds);

        foreach (var item in items)
        {
            var (op, expr) = item;

            var needParens = ownPrecendence >= expr.Precedence;

            writer.Break(relativeNesting: -op?.Length ?? 0);
            if (needParens) writer.Write("(");
            writer.Open(expr.EstimatedRenderLength);
            if (op is not null) writer.Write(op);
            expr.Write(writer, useIds);
            writer.Close();
            if (needParens) writer.Write(")");
        }
    }
}

public class MultiRelationalExpression : SequenceExpression
{
    public required Expression LeftExpression { get; init; }

    public required RelationshipTail[] Tail { get; init; }

    public required Boolean IsBoolean { get; init; }

    public Boolean IsRelation => Tail.Length == 1;

    public override Boolean IsUnordered => IsRelation && Tail[0].relation.Flags.HasFlag(RelationFlags.Symmetric);

    public override IEnumerable<(String? op, Expression expr)> GetItems(Boolean useIds)
    {
        yield return (null, LeftExpression);

        foreach (var i in Tail)
        {
            yield return (i.relation.GetName(i.conversed, i.negated), i.rhs);
        }
    }
}

public class RelationshipExpression : SequenceExpression
{
    RelationshipTail tail;

    public Expression LeftExpression { get; }

    public RelationshipTail Tail => tail;

    public Expression RightExpression => tail.rhs;

    public override Boolean IsUnordered => tail.relation.Flags.HasFlag(RelationFlags.Symmetric);

    public override IEnumerable<(String? op, Expression expr)> GetItems(Boolean useIds)
    {
        yield return (null, LeftExpression);

        var i = Tail;

        yield return (i.relation.GetName(i.conversed, i.negated), i.rhs);
    }

    public RelationshipExpression(Expression lhs, Relation relation, Boolean negated, Boolean conversed, Expression rhs)
    {
        LeftExpression = lhs;
        tail.relation = relation;
        tail.negated = negated;
        tail.conversed = conversed;
        tail.rhs = rhs;
    }

    public void Deconstruct(out Expression lhs, out Relation relation, out Boolean negated, out Boolean conversed, out Expression rhs)
    {
        lhs = RightExpression;
        negated = tail.negated;
        conversed = tail.conversed;
        relation = tail.relation;
        rhs = tail.rhs;
    }
}

public class FunctionalExpression : SequenceExpression
{
    (Boolean inverted, Expression expr)[]? operands;

    public required Functional Functional { get; init; }

    public required (Boolean inverted, Expression expr)[] OwnOperands { get; init; }

    public override Boolean IsUnordered => Functional.IsCommutative;

    public (Boolean inverted, Expression expr)[] Operands
    {
        get
        {
            if (operands is null)
            {
                var ops = CalculateOperands().ToList();

                ops.Sort(operandsComparer);

                operands = ops.ToArray();
            }
            return operands;
        }
    }

    public IEnumerable<(Boolean inverted, Expression expr)> CalculateOperands()
    {
        var functional = Functional;

        foreach (var operand in OwnOperands)
        {
            var (inverted, expr) = operand;

            if (expr is FunctionalExpression otherFunctional &&
                functional == otherFunctional.Functional &&
                functional.IsAssociative)
            {
                foreach (var item in otherFunctional.Operands)
                {
                    yield return (item.inverted != inverted, item.expr);
                }
            }
            else
            {
                yield return operand;
            }
        }
    }

    static OperandsComparer operandsComparer = default;

    public override IEnumerable<(String? op, Expression expr)> GetItems(Boolean useIds)
    {
        Boolean isSubsequent = false;

        var ops = useIds ? Operands : OwnOperands;
        
        foreach (var o in ops)
        {
            yield return (isSubsequent ? Functional.GetOpName(o.inverted) : null, o.expr);

            isSubsequent = true;
        }
    }
}

public class QuantizationExpression : Expression
{
    public required String QuantizationSymbol { get; init; }

    public required Symbol Symbol { get; init; }

    public required Expression Head { get; init; }

    public required Expression Body { get; init; }

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
