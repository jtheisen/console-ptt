namespace Ptt;

public class RuntimeContext
{
    public required TestGuide Guide { get; init; }
}

public class BlockItem
{
    protected RuntimeContext runtime;

    protected ContentBlock? parent;

    public BlockItem(RuntimeContext runtime)
        : this(null, runtime)
    {
    }

    public BlockItem(ContentBlock parent)
        : this(parent, null)
    {
    }

    public BlockItem(ContentBlock? parent, RuntimeContext? runtime)
    {
        if (parent is not null)
        {
            this.parent = parent;
            this.runtime = parent.runtime;
        }
        else if (runtime is not null)
        {
            this.runtime = runtime;
        }
        else
        {
            throw new AssertionException("Either parent or runtime must be provided");
        }
    }
}

public class ContentBlock : BlockItem
{
    private readonly DirectiveType type;

    RelationshipsState relationships;

    public RelationshipsState Relationships => relationships;

    Adopter adopter;

    List<BlockItem> items;

    public ContentBlock(RuntimeContext runtime)
        : this(null, DirectiveType.Unknown, runtime)
    {
    }

    public ContentBlock(ContentBlock? parent, DirectiveType type, RuntimeContext? runtime = null)
        : base(parent, runtime)
    {
        relationships = new RelationshipsState(parent?.relationships);
        items = new();
        this.type = type;
        adopter = new Adopter { Guide = this.runtime.Guide };
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

    public void Assert(Expression expr, Boolean unproven)
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
    //public static void ProcessContent(this ContentBlock block, SyntaxBlockContent content)
    //{
    //    foreach (var item in content.items)
    //    {
    //        if (item is SyntaxDirectiveBlock directive)
    //        {
    //            block.ProcessDirective(directive);
    //        }
    //        else if (item is SyntaxExpression expr)
    //        {
    //            block.ProcessExpression(expr);
    //        }
    //        else
    //        {
    //            throw new AssertionException($"Unknown syntax type '{item.GetType()}'");
    //        }
    //    }
    //}

    //public static void ProcessExpression(this ContentBlock block, SyntaxExpression expr)
    //{

    //}

    //public static void ProcessDirective(this ContentBlock block, SyntaxDirectiveBlock directive)
    //{
    //    switch (directive.type)
    //    {
    //        case DirectiveType.Section:
    //            var section = block.StartSection();
    //            section.ProcessContent(directive.body);
    //            break;
    //        case DirectiveType.Claim:
    //            throw new NotImplementedException();
    //        case DirectiveType.Take:
    //            var assumptions = directive.expr ?? throw new AssertionException($"Proof blocks must contain assumptions");
    //            var proof = block.StartProof(block.Adopt(assumptions));
    //            proof.ProcessContent(directive.body);
    //            break;
    //        default:
    //            break;
    //    }
    //}
}
