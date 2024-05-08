using Ptt;

namespace TestSuite;

[TestClass]
public class FileTests
{
    static String[] fileNames = [
        "snaps"
    ];

    static Dictionary<String, String> filePaths;

    static FileTests()
    {
        var files = Directory.GetFiles("tests");

        filePaths = files.ToDictionary(f => Path.GetFileNameWithoutExtension(f), f => f);

        foreach (var f in filePaths.Keys)
        {
            if (!fileNames.Contains(f))
            {
                filePaths.Remove(f);
            }
        }
    }

    static public IEnumerable<String[]> FileNames => filePaths.Keys.Select(fp => new[] { fp });

    [TestMethod]
    [DynamicData("FileNames")]
    public void TestFiles(String fileName)
    {
        var filePath = filePaths[fileName];

        var content = File.ReadAllText(filePath);

        var guide = new TestGuide();

        var parser = new BlockParser { Guide = guide };

        parser.ParseDocument(content);
    }
}
