using Ptt;

namespace TestSuite;

[TestClass]
public class FileTests
{
    static Dictionary<String, String> filePaths;

    static FileTests()
    {
        var files = Directory.GetFiles("tests");

        filePaths = files.ToDictionary(f => Path.GetFileNameWithoutExtension(f), f => f);
    }

    static public IEnumerable<String[]> FileNames => filePaths.Keys.Select(fp => new[] { fp });

    [TestMethod]
    [DynamicData("FileNames")]
    public void TestFiles(String fileName)
    {
        var filePath = filePaths[fileName];

        var content = File.ReadAllText(filePath);

        var parser = new Parser();

        parser.ParseDocument(content);
    }
}
