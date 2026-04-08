using OwlCore.Storage.CommonTests;

namespace OwlCore.Storage.NfsSharp.Tests;

[TestClass]
public class NfsFileTests : CommonIFileTests
{
    private MockNfsClient _mockClient = null!;

    // The mock NFS server does not automatically update timestamps on file system operations.
    // Only explicit SETATTR calls via SetAttrAsync / UpdateValueAsync change timestamps.
    public override PropertyUpdateBehavior LastModifiedAtUpdateBehavior => PropertyUpdateBehavior.Never;
    public override PropertyUpdateBehavior LastAccessedAtUpdateBehavior => PropertyUpdateBehavior.Never;

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

    /// <inheritdoc/>
    public override Task<IFile?> CreateFileWithCreatedAtAsync(DateTime createdAt)
    {
        // NFS v3 does not expose a creation time — skip this test.
        return Task.FromResult<IFile?>(null);
    }

    /// <inheritdoc/>
    public override async Task<IFile?> CreateFileWithLastModifiedAtAsync(DateTime lastModifiedAt)
    {
        var rootFolder = await NfsFolder.GetFromNfsPathAsync(_mockClient, "/");
        var testFolder = (NfsFolder)await rootFolder.CreateFolderAsync("owlcorestoragetest");
        var file = (NfsFile)await testFolder.CreateFileAsync(Ulid.NewUlid().ToString());

        _mockClient.SetTimestamps(file.Path, modifyTime: new DateTimeOffset(lastModifiedAt, TimeSpan.Zero));

        return file;
    }

    /// <inheritdoc/>
    public override async Task<IFile?> CreateFileWithLastAccessedAtAsync(DateTime lastAccessedAt)
    {
        var rootFolder = await NfsFolder.GetFromNfsPathAsync(_mockClient, "/");
        var testFolder = (NfsFolder)await rootFolder.CreateFolderAsync("owlcorestoragetest");
        var file = (NfsFile)await testFolder.CreateFileAsync(Ulid.NewUlid().ToString());

        _mockClient.SetTimestamps(file.Path, accessTime: new DateTimeOffset(lastAccessedAt, TimeSpan.Zero));

        return file;
    }

    [TestCleanup]
    public void Cleanup()
    {
        _mockClient.Dispose();
    }
}
