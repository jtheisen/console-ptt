namespace Ptt;

public class RelationshipsState
{
    static readonly IEnumerable<Expression> noThingies = Enumerable.Empty<Expression>();

    RelationshipsState? parent;

    Dictionary<Expression, Dictionary<Relation, Dictionary<RelationshipFlags, HashSet<Expression>>>> dict;

    public RelationshipsState(RelationshipsState? parent = null)
    {
        this.parent = parent;
        dict = new();
    }

    public IEnumerable<Expression> GetRelated(Expression lhs, RelationVariant relation)
    {
        return (parent?.GetRelated(lhs, relation) ?? noThingies).Concat(GetOwnRelated(lhs, relation));
    }

    IEnumerable<Expression> GetOwnRelated(Expression lhs, RelationVariant relation)
    {
        if (!dict.TryGetValue(lhs, out var relationships)) return noThingies;
        if (!relationships.TryGetValue(relation.relation, out var variants)) return noThingies;
        if (!variants.TryGetValue(relation.flags, out var partners)) return noThingies;
        return partners;
    }

    public Boolean Contains(Relationship r)
    {
        if (parent is not null && parent.Contains(r)) return true;

        if (!dict.TryGetValue(r.lhs, out var relationships)) return false;
        if (!relationships.TryGetValue(r.relation.relation, out var variants)) return false;
        if (!variants.TryGetValue(r.relation.flags, out var partners)) return false;
        return partners.Contains(r.rhs);
    }

    public Boolean Add(Relationship r)
    {
        if (Contains(r)) return false;

        if (!dict.TryGetValue(r.lhs, out var relationships))
        {
            relationships = dict[r.lhs] = new();
        }

        if (!relationships.TryGetValue(r.relation.relation, out var variants))
        {
            variants = relationships[r.relation.relation] = new();
        }

        if (!variants.TryGetValue(r.relation.flags, out var partners))
        {
            partners = variants[r.relation.flags] = new();
        }

        return partners.Add(r.rhs);
    }
}

public struct RelationVariant
{
    public required Relation relation;
    public required RelationshipFlags flags;

}

public struct ExpressionRelationshipTail
{
    public required RelationVariant relation;
    public required Expression target;
}

public struct Relationship
{
    public Expression lhs;
    public RelationVariant relation;
    public Expression rhs;

    public static implicit operator Relationship((Expression lhs, RelationVariant relation, Expression rhs) r)
    {
        return new Relationship { lhs = r.lhs, relation = r.relation, rhs = r.rhs };
    }

    public void Deconstruct(out Expression lhs, out RelationVariant relation, out Expression rhs)
    {
        lhs = this.lhs;
        relation = this.relation;
        rhs = this.rhs;
    }
}

public static class StateExtensions
{
    public static IEnumerable<Relationship> GetDirectRelationships(this RelationalExpression relational)
    {
        var lhs = relational.LeftExpression;

        foreach (var item in relational.Tail)
        {
            yield return (lhs, new RelationVariant { relation = item.relation, flags = item.flags }, item.rhs);

            lhs = item.rhs;
        }
    }

    public static IEnumerable<Relationship> GetRelationships(this Expression expr)
    {
        // FIXME: many more

        if (expr is RelationalExpression relational)
        {
            foreach (var r in relational.GetDirectRelationships())
            {
                yield return (r.lhs, r.relation, r.rhs);
            }
        }
    }

    public static void Add(this RelationshipsState state, Expression expr)
    {
        foreach (var r in expr.GetRelationships())
        {
            state.Add(r);
        }
    }
}