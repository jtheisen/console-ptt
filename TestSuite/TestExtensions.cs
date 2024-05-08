using Ptt;

namespace TestSuite;

public static class TestExtensions
{
    public static IReadOnlyCollection<String> SplitLines(this String input)
    {
        var reader = new StringReader(input);

        var lines = new List<String>();

        while (true)
        {
            if (reader.ReadLine() is String line)
            {
                lines.Add(line);
            }
            else
            {
                break;
            }
        }

        return lines;
    }

    public static SyntaxExpression ParseExpression(this ExpressionParser parser, IEnumerable<InputToken> input)
    {
        var enumerator = input.Where(t => t.cls.IsSubstantial()).GetEnumerator();

        enumerator.MoveNext();

        var result = parser.ParseExpression(enumerator, outerPrecedence: Double.MinValue);

        return result;
    }

    public static SyntaxExpression ParseExpression(this ExpressionParser parser, String input)
        => parser.ParseExpression(parser.Tokenize(input.SplitLines()));

    public static ContentBlock ParseDocument(this BlockParser parser, IEnumerable<InputToken> input)
    {
        var enumerator = input.Where(t => t.cls.IsSubstantial()).GetEnumerator();

        enumerator.MoveNext();

        var runtime = new RuntimeContext { Guide = new TestGuide() };

        var rootBlock = new ContentBlock(runtime);

        parser.ParseContent(rootBlock, enumerator);

        return rootBlock;
    }

    public static ContentBlock ParseDocument(this BlockParser parser, String input)
        => parser.ParseDocument(parser.Tokenize(input.SplitLines()));
}
