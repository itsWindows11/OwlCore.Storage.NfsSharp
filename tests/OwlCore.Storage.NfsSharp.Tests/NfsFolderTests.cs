using OwlCore.Storage.CommonTests;

namespace OwlCore.Storage.NfsSharp.Tests;

[TestClass]
public class NfsFolderTests : CommonIModifiableFolderTests
{
    private MockNfsClient _mockClient = null!;

    // The mock NFS server does not automatically update timestamps on file system operations
    // (read, write, folder iteration, create, delete). Only explicit SETATTR calls via
    // SetAttrAsync / UpdateValueAsync change timestamps. Signal this to the common tests
    // so they skip the automatic-update assertions.
    public override PropertyUpdateBehavior LastModifiedAtUpdateBehavior => PropertyUpdateBehavior.Never;
    public override PropertyUpdateBehavior LastAccessedAtUpdateBehavior => PropertyUpdateBehavior.Never;

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

    /// <inheritdoc/>
    public override Task<IFolder?> CreateFolderWithCreatedAtAsync(DateTime createdAt)
    {
        // NFS v3 does not expose a creation time — skip this test.
        return Task.FromResult<IFolder?>(null);
    }

    /// <inheritdoc/>
    public override async Task<IFolder?> CreateFolderWithLastModifiedAtAsync(DateTime lastModifiedAt)
    {
        var rootFolder = await NfsFolder.GetFromNfsPathAsync(_mockClient, "/");
        var testFolder = (NfsFolder)await rootFolder.CreateFolderAsync("owlcorestoragetest");
        var folder = (NfsFolder)await testFolder.CreateFolderAsync(Ulid.NewUlid().ToString());

        _mockClient.SetTimestamps(folder.Path, modifyTime: new DateTimeOffset(lastModifiedAt, TimeSpan.Zero));

        return folder;
    }

    /// <inheritdoc/>
    public override async Task<IFolder?> CreateFolderWithLastAccessedAtAsync(DateTime lastAccessedAt)
    {
        var rootFolder = await NfsFolder.GetFromNfsPathAsync(_mockClient, "/");
        var testFolder = (NfsFolder)await rootFolder.CreateFolderAsync("owlcorestoragetest");
        var folder = (NfsFolder)await testFolder.CreateFolderAsync(Ulid.NewUlid().ToString());

        _mockClient.SetTimestamps(folder.Path, accessTime: new DateTimeOffset(lastAccessedAt, TimeSpan.Zero));

        return folder;
    }

    /// <inheritdoc/>
    public override async Task<IFile?> CreateFileInFolderWithLastModifiedAtAsync(IModifiableFolder folder, DateTime lastModifiedAt)
    {
        var file = (NfsFile)await folder.CreateFileAsync(Ulid.NewUlid().ToString());

        _mockClient.SetTimestamps(file.Path, modifyTime: new DateTimeOffset(lastModifiedAt, TimeSpan.Zero));

        return file;
    }

    /// <inheritdoc/>
    public override async Task<CreateFileInFolderWithTimestampsResult?> CreateFileInFolderWithTimestampsAsync(
        IModifiableFolder folder,
        DateTime? createdAt,
        DateTime? lastModifiedAt,
        DateTime? lastAccessedAt)
    {
        var file = (NfsFile)await folder.CreateFileAsync(Ulid.NewUlid().ToString());

        _mockClient.SetTimestamps(
            file.Path,
            accessTime: lastAccessedAt.HasValue ? new DateTimeOffset(lastAccessedAt.Value, TimeSpan.Zero) : null,
            modifyTime: lastModifiedAt.HasValue ? new DateTimeOffset(lastModifiedAt.Value, TimeSpan.Zero) : null);

        // NFS v3 has no creation time. Report the timestamps we actually set so that the
        // copy/move preservation tests can verify them via the now-writable properties.
        return new CreateFileInFolderWithTimestampsResult(
            CreatedFile: file,
            CreatedAt: null,
            LastModifiedAt: lastModifiedAt,
            LastAccessedAt: lastAccessedAt);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _mockClient.Dispose();
    }
}
