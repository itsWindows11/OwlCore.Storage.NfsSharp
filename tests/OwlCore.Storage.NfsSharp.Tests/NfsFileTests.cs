using OwlCore.Storage.CommonTests;

namespace OwlCore.Storage.NfsSharp.Tests;

[TestClass]
public class NfsFileTests : CommonIFileTests
{
    private MockNfsClient _mockClient = null!;

    [TestInitialize]
    public void Initialize()
    {
        _mockClient = new MockNfsClient();
    }

    public override Task<IFile> CreateFileAsync()
    {
        return GenerateRandomFileAsync(fileSize: 256_000);

        async Task<IFile> GenerateRandomFileAsync(int fileSize)
        {
            var rootFolder = await NfsFolder.GetFromNfsPathAsync(_mockClient, "/");
            var testFolder = await rootFolder.CreateFolderAsync("owlcorestoragetest") as NfsFolder;

            var file = await testFolder!.CreateFileAsync(Ulid.NewUlid().ToString());

            await file.WriteBytesAsync(GenerateRandomData(fileSize));

            return file;
        }

        static byte[] GenerateRandomData(int length)
        {
            var bytes = new byte[length];
            Random.Shared.NextBytes(bytes);
            return bytes;
        }
    }

    [TestCleanup]
    public void Cleanup()
    {
        _mockClient.Dispose();
    }
}
