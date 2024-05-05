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

    public static SyntaxExpression ParseExpression(this Parser parser, IEnumerable<InputToken> input)
    {
        var enumerator = input.Where(t => t.cls.IsSubstantial()).GetEnumerator();

        enumerator.MoveNext();

        var result = parser.ParseExpression(enumerator, outerPrecedence: Double.MinValue);

        return result;
    }

    public static SyntaxExpression ParseExpression(this Parser parser, String input)
        => parser.ParseExpression(parser.Tokenize(input.SplitLines()));

    public static SyntaxDirectiveBlock ParseDocument(this Parser parser, IEnumerable<InputToken> input)
    {
        var enumerator = input.Where(t => t.cls.IsSubstantial()).GetEnumerator();

        enumerator.MoveNext();

        var result = parser.ParseDirectiveBlock(enumerator);

        return result;
    }

    public static SyntaxDirectiveBlock ParseDocument(this Parser parser, String input)
        => parser.ParseDocument(parser.Tokenize(input.SplitLines()));

}
