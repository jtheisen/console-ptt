﻿using Ptt;

namespace TestSuite;

[TestClass]
public class ParsingTests
{
    [TestMethod]
    [DataRow("", "")]
    [DataRow("x", "x")]
    [DataRow("x*y", "x|*|y")]
    [DataRow("((", "(|(")]
    [DataRow("foo**bar", "foo|*|*|bar")]
    [DataRow("∃A set", "∃|A|set")]
    [DataRow("∃ A set", "∃|A|set")]
    [DataRow("(x+bar*z<foo)", "(|x|+|bar|*|z|<|foo|)")]
    [DataRow("""
        foo
        # comment
        bar
        """,
        "foo|bar")]
    public void TestTokenization(String input, String expected)
    {
        var guide = new TestGuide();

        var parser = new ExpressionParser { Guide = guide };

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

        var actualTokens = parser
            .Tokenize(lines)
            .Where(t => t.cls.IsSubstantial(includeEof: false))
            .ToArray();

        var actualParts = actualTokens
            .Select(t => t.ToString())
            .ToArray();

        var actual = String.Join("|", actualParts);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("x", "x")]
    [DataRow("(x)", "x")]
    [DataRow("((x))", "x")]
    [DataRow("x,y", "x,y")]
    [DataRow("x<y", "x<y")]
    [DataRow("x!<y", "x!<y")]
    [DataRow("x+y", "x+y")]
    [DataRow("x+y-z", "x+y-z")]
    [DataRow("x+y*z", "x+(y*z)")]
    [DataRow("x*y-z", "(x*y)-z")]
    [DataRow("-x", "-x")]
    [DataRow("-x/y", "-(x/y)")]
    [DataRow("/x-y", "(/x)-y")]
    [DataRow("x-/y", "x-(/y)")]
    [DataRow("x/-y", "x/(-y)")]
    [DataRow("x-/y*z", "x-(/y*z)")]
    [DataRow("x/-y+z", "(x/(-y))+z")]
    [DataRow("x^-y*z", "(x^(-y))*z")]
    [DataRow("x/-y^a+z", "(x/(-(y^a)))+z")]
    [DataRow("x^/-y", "x^(/(-y))")]
    [DataRow("--x", null)]
    [DataRow("x--x", null)]
    [DataRow("x - - x", null)]
    [DataRow("+-x", null)]
    [DataRow("x+-x", null)]
    [DataRow("x + - x", null)]

    [DataRow("∑ n∊N /n^x", null)]
    [DataRow("∑ n∊N: ∑ m∊N: /n/m", "∑ n∊N: (∑ m∊N: (/n/m))", SyntaxNodeStringificationFlags.ParenBodies)]
    [DataRow("∑ n∊N: ∑ m∊N: n*m", "∑ n∊N: ∑ m∊N: n*m")]
    [DataRow("∑ n∊N: /n + ∑ m∊N: /m", "(∑ n∊N: /n)+(∑ m∊N: /m)")]
    [DataRow("∑ n∊N: (/n + ∑ m∊N: /m)", "∑ n∊N: (/n)+(∑ m∊N: /m)")]
    [DataRow("∑ n∊N: /n * ∑ m∊N: /m", "∑ n∊N: /n*(∑ m∊N: /m)")]
    [DataRow("∑ n∊N: /n^x", "∑ n∊N: /(n^x)")]
    [DataRow("∀ x∊X ∃ y∊Y: x<y", "∀ x∊X: ∃ y∊Y: x<y")]
    [DataRow("∀ x∊X,X⊆Y ∃ y∊Y: x<y", "∀ (x∊X),(X⊆Y): ∃ y∊Y: x<y")]

    [DataRow("{ n∊N: n>m }", "{ n∊N: n>m }")]
    
    //[DataRow("", "")]
    //[DataRow("", "")]
    public void TestExpressionParsing(String input, String? expectedEncoded, SyntaxNodeStringificationFlags? flags = null)
    {
        var guide = new TestGuide();

        var parser = new ExpressionParser { Guide = guide };

        var enumerator = parser.Tokenize([input]).Where(t => t.cls.IsSubstantial()).GetEnumerator();

        enumerator.MoveNext();

        if (expectedEncoded is not null)
        {
            var result = parser.ParseExpression(enumerator, outerPrecedence: Double.MinValue);

            Assert.AreEqual(expectedEncoded, result.ToString(flags ?? SyntaxNodeStringificationFlags.None));
        }
        else
        {
            Assert.ThrowsException<ParsingException>(() => parser.ParseExpression(enumerator, outerPrecedence: Double.MinValue));
        }
    }
}