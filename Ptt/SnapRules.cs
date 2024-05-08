namespace Ptt;

public interface ISnapRuleContext
{
    IEnumerable<Expression> GetRelated(Expression lhs, RelationVariant relation);

    void Relate(Expression lhs, RelationVariant relation, Expression rhs);
}

public class SnapRules : ISnapRules
{
    List<SnapRule> rules = new List<SnapRule>();

    public void Add(SnapRule rule)
    {
        rules.Add(rule);
    }

    public IEnumerable<SnapRule> GetRules(Relationship relationship)
    {
        return rules;
    }
}

public interface ISnapRules
{
    IEnumerable<SnapRule> GetRules(Relationship relationship);
}

public record SnapRule(Func<RelationshipsState, Relationship, Relationship> Rule);

public abstract class AbstractSnapRule
{
    public abstract void Apply(ISnapRuleContext context, Relationship relationship);
}

public class SimpleSnapRule : AbstractSnapRule
{
    public required RelationVariant Condition { get; init; }
    public required RelationVariant Consequence { get; init; }

    public override void Apply(ISnapRuleContext context, Relationship relationship)
    {
        if (relationship.relation.Equals(Condition))
        {
            context.Relate(relationship.lhs, Consequence, relationship.rhs);
        }
    }
}

public class TransitiveSnapRule : AbstractSnapRule
{
    public required RelationVariant Left { get; init; }

    public required RelationVariant Right { get; init; }

    public required RelationVariant Target { get; init; }

    public override void Apply(ISnapRuleContext context, Relationship relationship)
    {
        if (relationship.relation.Equals(Left))
        {
            foreach (var related in context.GetRelated(relationship.rhs, Right))
            {
                context.Relate(relationship.rhs, Target, related);
            }
        }

        if (relationship.relation.Equals(Right))
        {
            foreach (var related in context.GetRelated(relationship.lhs, Left))
            {
                context.Relate(related, Target, relationship.lhs);
            }
        }
    }
}
