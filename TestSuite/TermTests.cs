using Ptt;

namespace TestSuite;

[TestClass]
public class TermTests
{
    [TestMethod]
    public void TestSameness()
    {
        var parser = new Parser();

        var builder = new ContextBuilder();

        Term Parse(String input)
        {
            var syntax = parser.Parse(input);

            var term = builder.Create(syntax);

            return term;
        }

        String[][] groups = [
            [ "A" ],
            [ "B" ],
            [ "A*B", "B*A" ],
            [ "A+B", "B+A" ],
            [ "A/B", "/B*A" ],
            [ "A-B", "-B+A" ],
            [ "∑ n∊ℕ: /n", "∑ m∊ℕ: /m" ],
            [ "∑ n∊ℕ: ∑ m∊ℕ: /n/m", "∑ n∊ℕ: ∑ m∊ℕ: /m/n" ],
            [ "∑ x∊ℝ: ∑ y∊ℚ: /x/y", "∑ x∊ℝ: ∑ y∊ℚ: /y/x" ],
            [ "∑ x∊ℚ: ∑ y∊ℝ: /x/y" ],
        ];

        var terms = new HashSet<Term>();

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
}
