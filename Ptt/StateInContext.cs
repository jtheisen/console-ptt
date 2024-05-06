using System.Collections.Immutable;

namespace Ptt;


public class RelationshipsState
{
    static readonly IEnumerable<Thingy> noThingies = Enumerable.Empty<Thingy>();

    RelationshipsState? parent;

    Dictionary<Thingy, Dictionary<Relation, Dictionary<RelationshipFlags, HashSet<Thingy>>>> dict;

    public RelationshipsState(RelationshipsState? parent = null)
    {
        this.parent = parent;
        dict = new();
    }

    public IEnumerable<Thingy> GetRelated(Thingy lhs, RelationVariant relation)
    {
        return (parent?.GetRelated(lhs, relation) ?? noThingies).Concat(GetOwnRelated(lhs, relation));
    }

    IEnumerable<Thingy> GetOwnRelated(Thingy lhs, RelationVariant relation)
    {
        if (!dict.TryGetValue(lhs, out var relationships)) return noThingies;
        if (!relationships.TryGetValue(relation.relation, out var variants)) return noThingies;
        if (!variants.TryGetValue(relation.flags, out var partners)) return noThingies;
        return partners;
    }

    public Boolean Contains(Thingy lhs, RelationVariant relation, Thingy rhs)
    {
        if (parent is not null && parent.Contains(lhs, relation, rhs)) return true;

        if (!dict.TryGetValue(lhs, out var relationships)) return false;
        if (!relationships.TryGetValue(relation.relation, out var variants)) return false;
        if (!variants.TryGetValue(relation.flags, out var partners)) return false;
        return partners.Contains(rhs);
    }

    public Boolean Add(Thingy lhs, RelationVariant relation, Thingy rhs)
    {
        if (Contains(lhs, relation, rhs)) return false;

        if (!dict.TryGetValue(lhs, out var relationships))
        {
            relationships = dict[lhs] = new();
        }

        if (!relationships.TryGetValue(relation.relation, out var variants))
        {
            variants = relationships[relation.relation] = new();
        }

        if (!variants.TryGetValue(relation.flags, out var partners))
        {
            partners = variants[relation.flags] = new();
        }

        return partners.Add(rhs);
    }
}


public record StateInContext(IImmutableDictionary<Expression, Thingy> Thingies, IImmutableDictionary<Thingy, IImmutableDictionary<Relation, IImmutableDictionary<RelationshipFlags, IImmutableSet<Thingy>>>> Relationships)
{
    // - is a relation there?
    // - what are all related thingies of a given one by one relation?
    // - get all relationships for display

    public Boolean HasRelationship(Thingy lhs, RelationVariant relation, Thingy rhs)
    {
        if (Relationships.TryGetValue(lhs, out var relations))
        {
            if (relations.TryGetValue(relation.relation, out var relationships))
            {
                if (relationships.TryGetValue(relation.flags, out var targets))
                {
                    return targets.Contains(rhs);
                }
            }
        }

        return false;
    }
}

public struct RelationVariant
{
    public required Relation relation;
    public required RelationshipFlags flags;

}

public struct ThingyRelationshipTail
{
    public required RelationVariant relation;
    public required Thingy target;
}

public class Thingy
{
}

