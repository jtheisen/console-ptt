using Ptt;

namespace TestSuite;

[TestClass]
public class ExpressionTests
{
    [TestMethod]
    public void TestInterchangeability()
    {
        var parser = new Parser();

        var builder = new SyntaxAdopter();

        Expression Parse(String input)
        {
            var syntax = parser.Parse(input);

            var expr = builder.Create(syntax);

            return expr;
        }

        String[][] groups = [
            [ "A" ],
            [ "B" ],
            [ "A*B", "B*A", "A*(B<C)" ],
            [ "A*B*C", "A*(B*C)" ],
            [ "A/B/C", "A*(/B/C)", "A/(B*C)", "/(B*C)*A", "/C/B/A", "(/C/B)/A", "/(C*B)/A" ],
            [ "A+B", "B+A" ],
            [ "A/B", "/B*A" ],
            [ "A-B", "-B+A" ],
            [ "∑ n∊ℕ: /n", "∑ m∊ℕ: /m", "∑ n∊ℕ: (/n<C)" ],
            [
                "∑ n∊ℕ: ∑ m∊ℕ: /n/m",
                "∑ n∊ℕ: ∑ m∊ℕ: /m/n",
                "∑ n∊ℕ: ∑ m∊ℕ: /n*(/m<C)",
                "∑ n∊ℕ: ∑ m∊ℕ: (/n/m<C)",
                "∑ n∊ℕ: (∑ m∊ℕ: /n/m<C)"
            ],
            [ "∑ x∊ℝ: ∑ y∊ℚ: /x/y", "∑ x∊ℝ: ∑ y∊ℚ: /y/x" ],
            [ "∑ x∊ℚ: ∑ y∊ℝ: /x/y" ],
        ];

        var terms = new HashSet<Expression>();

        for (var i = 0; i < groups.Length; ++i)
        {
            var group = groups[i];

            var first = Parse(group[0]);

            for (var j = 1; j < group.Length; ++j)
            {
                var item = group[j];

                Assert.AreEqual(first.Id, Parse(item).Id);
            }

            if (terms.TryGetValue(first, out var other))
            {
                Assert.AreNotEqual(first.Id, other.Id);
            }

            terms.Add(first);
        }
    }

    [TestMethod]
    [DataRow("∀x∀y∀z: x*y+x*z = x*(y+z)", "x*y+x*z = x*(y+z)")]
    [DataRow("∀x∀y∀z: x*y+x*z = x*(y+z)", "a*c*(*b) + d*b*e = b*(a*c+d*e)")]
    [DataRow("∀x: x=x+0", "0=0+0")]
    [DataRow("∀x∀y∀z: x*y+x*z = x*(y+z)", "x*(0+0)=x*0+x*0")]
    [DataRow("∀x: x-x=0", "x*0-x*0=0")]
    public void TestUnification(String rule, String target)
    {
    }
}
