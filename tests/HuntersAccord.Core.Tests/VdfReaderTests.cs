using HuntersAccord.Core.Steam;

namespace HuntersAccord.Core.Tests;

public class VdfReaderTests
{
    [Fact]
    public void Parses_library_folders()
    {
        const string vdf = """
            "libraryfolders"
            {
                "0"
                {
                    "path"		"C:\\Program Files (x86)\\Steam"
                    "apps" { "228980" "123" }
                }
                "1"
                {
                    "path"		"E:\\Steam\\SteamLibrary"
                }
            }
            """;

        var root = VdfReader.Parse(vdf).Child("libraryfolders")!;

        Assert.Equal(@"C:\Program Files (x86)\Steam", root.Child("0")!.Value("path"));
        Assert.Equal(@"E:\Steam\SteamLibrary", root.Child("1")!.Value("path"));
        Assert.Equal("123", root.Child("0")!.Child("apps")!.Value("228980"));
    }
}
