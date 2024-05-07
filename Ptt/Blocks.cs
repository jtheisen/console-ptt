namespace Ptt;

public class BlockItem
{
    ContentBlock? parent;

    public BlockItem(ContentBlock? parent)
    {
        this.parent = parent;
    }
}

public class ContentBlock : BlockItem
{
    private readonly DirectiveType type;

    RelationshipsState relationships;

    public RelationshipsState Relationships => relationships;

    Adopter adopter = new Adopter();

    List<BlockItem> items;

    public ContentBlock(ContentBlock parent, DirectiveType type)
        : base(parent)
    {
        relationships = new RelationshipsState(parent?.relationships);
        items = new();
        this.type = type;
    }

    public Expression Adopt(SyntaxExpression expression)
    {
        return adopter.Adopt(expression);
    }

    // All public methods represent things the UI will later also do

    ContentBlock StartSubblock(DirectiveType type)
    {
        var block = new ContentBlock(this, type);

        items.Add(block);

        return block;
    }

    public ContentBlock StartProof(Expression assumptions)
    {
        var proof = StartSubblock(DirectiveType.Take);
        
        proof.relationships.Add(assumptions);

        return proof;
    }

    public ContentBlock StartSection()
    {
        return StartSubblock(DirectiveType.Section);
    }

    public void Assert(RelationalExpression expr, Boolean unproven)
    {
        foreach (var r in expr.GetRelationships())
        {
            var contains = relationships.Contains(r);

            if (contains == unproven)
            {
                throw new AssertionException($"#assert failed: {expr} is {(contains ? "proven" : "unproven")}");
            }
        }
    }

    public void StartChain(Expression expr)
    {

    }

    public void Reason()
    {

    }
}

public class Chain : BlockItem
{
    RelationalExpression chain;

    public Chain(ContentBlock parent, RelationalExpression chain)
        : base(parent)
    {
        this.chain = chain;
    }
}

public static class BlockExtensions
{
    public static void ProcessContent(this ContentBlock block, SyntaxBlockContent content)
    {
        foreach (var item in content.items)
        {
            if (item is SyntaxDirectiveBlock directive)
            {
                block.ProcessDirective(directive);
            }
            else if (item is SyntaxExpression expr)
            {
                block.ProcessExpression(expr);
            }
            else
            {
                throw new AssertionException($"Unknown syntax type '{item.GetType()}'");
            }
        }
    }

    public static void ProcessExpression(this ContentBlock block, SyntaxExpression expr)
    {

    }

    public static void ProcessDirective(this ContentBlock block, SyntaxDirectiveBlock directive)
    {
        switch (directive.type)
        {
            case DirectiveType.Section:
                var section = block.StartSection();
                section.ProcessContent(directive.body);
                break;
            case DirectiveType.Claim:
                throw new NotImplementedException();
            case DirectiveType.Take:
                var assumptions = directive.expr ?? throw new AssertionException($"Proof blocks must contain assumptions");
                var proof = block.StartProof(block.Adopt(assumptions));
                proof.ProcessContent(directive.body);
                break;
            default:
                break;
        }
    }
}
