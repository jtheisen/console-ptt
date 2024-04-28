using Ptt;

namespace TestSuite;

[TestClass]
public class ParsingTests
{
    [TestMethod]
    [DataRow("", "")]
    [DataRow("x", "x")]
    [DataRow("∃A set", "∃|A|set")]
    [DataRow("∃ A set", "∃|A|set")]
    [DataRow("(x+bar*z<foo)", "(|x|+|bar|*|z|<|foo|)")]
    [DataRow("""
        foo
        # comment
        bar
        """,
        "foo|bar")]
    public void TestTokenization(String input, String expectedEncoded)
    {
        var parser = new Parser();

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

        var actual = parser
            .Tokenize(lines)
            .Where(t => t.cls != InputCharClass.Space && t.cls != InputCharClass.Comment)
            .Select(t => t.TokenString)
            .ToArray();

        var expected = expectedEncoded.Length > 0 ? expectedEncoded.Split('|') : [];

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("x", "x")]
    [DataRow("x+y", "x+y")]
    [DataRow("x+y-z", "x+y-z")]
    [DataRow("x+y*z", "x+(y*z)")]
    [DataRow("x*y-z", "(x*y)-z")]
    [DataRow("-x", "-x")]
    [DataRow("-x/y", "-(x/y)")]
    [DataRow("/x-y", "(/x)-y")]
    [DataRow("x - /y", "x-(/y)")]
    [DataRow("x / -y", "x/(-y)")]
    [DataRow("x - /y*z", "x-(/y*z)")]
    [DataRow("x / -y+z", "(x/(-y))+z")]
    [DataRow("x / -y^a+z", "(x/(-(y^a)))+z")]
    [DataRow("- - x", null)]
    [DataRow("x - - x", null)]
    [DataRow("x - - x", null)]
    public void TestExpressionParsing(String input, String? expectedEncoded)
    {
        var parser = new Parser();

        var enumerator = parser.Tokenize([input]).Where(t => t.cls.IsSubstantial()).GetEnumerator();

        enumerator.MoveNext();

        if (expectedEncoded is not null)
        {
            var result = parser.ParseExpression(enumerator);

            Assert.AreEqual(expectedEncoded, result.ToString());
        }
        else
        {
            Assert.ThrowsException<ParsingException>(() => parser.ParseExpression(enumerator));
        }
    }
}