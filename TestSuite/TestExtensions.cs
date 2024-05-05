using Ptt;

namespace TestSuite;

public static class TestExtensions
{
    public static SyntaxExpression Parse(this Parser parser, IEnumerable<InputToken> input)
    {
        var enumerator = input.Where(t => t.cls.IsSubstantial()).GetEnumerator();

        enumerator.MoveNext();

        var result = parser.ParseExpression(enumerator);

        return result;
    }

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

    public static SyntaxExpression Parse(this Parser parser, String input)
        => parser.Parse(parser.Tokenize(input.SplitLines()));

}
