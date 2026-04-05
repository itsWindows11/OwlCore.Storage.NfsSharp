using OwlCore.Storage.CommonTests;

namespace OwlCore.Storage.NfsSharp.Tests;

[TestClass]
public class NfsFolderTests : CommonIModifiableFolderTests
{
    private MockNfsClient _mockClient = null!;

    [TestInitialize]
    public void Initialize()
    {
        _mockClient = new MockNfsClient();
    }

    public override async Task<IModifiableFolder> CreateModifiableFolderAsync()
    {
        var rootFolder = await NfsFolder.GetFromNfsPathAsync(_mockClient, "/");
        var testFolder = await rootFolder.CreateFolderAsync("owlcorestoragetest") as NfsFolder;

        var name = Ulid.NewUlid().ToString();

        foreach (var ch in Path.GetInvalidFileNameChars())
            name = name.Replace(ch, '_');

        var childFolder = await testFolder!.CreateFolderAsync(name);

        Assert.IsNotNull(childFolder);

        return (childFolder as IModifiableFolder)!;
    }

    public override async Task<IModifiableFolder> CreateModifiableFolderWithItems(int fileCount, int folderCount)
    {
        var rootFolder = await NfsFolder.GetFromNfsPathAsync(_mockClient, "/");
        var testFolder = await rootFolder.CreateFolderAsync("owlcorestoragetest") as NfsFolder;

        var name = Ulid.NewUlid().ToString();

        foreach (var ch in Path.GetInvalidFileNameChars())
            name = name.Replace(ch, '_');

        var childFolder = await testFolder!.CreateFolderAsync(name) as IModifiableFolder;

        Assert.IsNotNull(childFolder);

        for (var i = 0; i < fileCount; i++)
        {
            var file = await childFolder.CreateFileAsync($"{name}_{i}.txt");
            Assert.IsNotNull(file);
        }

        for (var i = 0; i < folderCount; i++)
        {
            var subFolder = await childFolder.CreateFolderAsync($"{name}_{i}");
            Assert.IsNotNull(subFolder);
        }

        return childFolder;
    }

    [TestCleanup]
    public void Cleanup()
    {
        _mockClient.Dispose();
    }
}
