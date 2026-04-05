using System.Runtime.CompilerServices;
using NfsSharp.Protocol;

namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// An <see cref="IModifiableFolder"/> implementation backed by a directory on an NFS server.
/// </summary>
public partial class NfsFolder :
    IModifiableFolder,
    IChildFolder,
    IGetItem,
    IGetFirstByName,
    IGetItemRecursive,
    ICreateRenamedCopyOf,
    IMoveRenamedFrom,
    IHasNfsFileAttributes
{
    internal readonly INfsClient _nfsClient;

    /// <summary>
    /// Initializes a new instance of <see cref="NfsFolder"/>.
    /// </summary>
    /// <param name="nfsClient">The NFS client to use for folder operations.</param>
    /// <param name="path">The absolute path of the folder within the NFS export (e.g. <c>/reports</c> or <c>/</c> for root).</param>
    /// <param name="cachedAttributes">Already-fetched NFS attributes, if available. When non-<see langword="null"/> the first call to <see cref="GetFileAttributesAsync"/> returns immediately without a network round-trip.</param>
    public NfsFolder(INfsClient nfsClient, string path, NfsFileAttributes? cachedAttributes = null)
    {
        _nfsClient = nfsClient;
        Path = path;
        _cachedAttributes = cachedAttributes;
    }

    private NfsFileAttributes? _cachedAttributes;

    /// <summary>
    /// Gets the full NFS path of this folder (e.g. <c>/reports</c>).
    /// </summary>
    public string Path { get; }

    /// <inheritdoc/>
    public string Id => Path;

    /// <inheritdoc/>
    public string Name => Path == "/" ? string.Empty : global::System.IO.Path.GetFileName(Path.TrimEnd('/'));

    /// <inheritdoc/>
    public async Task<IStorageProperty<NfsFileAttributes>> GetFileAttributesAsync(CancellationToken cancellationToken = default)
    {
        _cachedAttributes ??= await _nfsClient.GetAttrAsync(Path, cancellationToken);
        return new StorageProperty<NfsFileAttributes>(_cachedAttributes);
    }

    /// <inheritdoc/>
    public Task<IChildFile> CreateCopyOfAsync(IFile fileToCopy, bool overwrite, CancellationToken cancellationToken, CreateCopyOfDelegate fallback)
    {
        // Route to the renamed overload for code deduplication,
        // adapting the non-rename fallback so the extra newName parameter is discarded.
        return CreateCopyOfAsync(
            fileToCopy,
            overwrite,
            newName: fileToCopy.Name,
            cancellationToken,
            (modifiableFolder, file, ov, _, ct) => fallback(modifiableFolder, file, ov, ct));
    }

    /// <inheritdoc/>
    public Task<IChildFile> MoveFromAsync(IChildFile fileToMove, IModifiableFolder source, bool overwrite, CancellationToken cancellationToken, MoveFromDelegate fallback)
    {
        // Route to the renamed overload for code deduplication,
        // adapting the non-rename fallback so the extra newName parameter is discarded.
        return MoveFromAsync(
            fileToMove,
            source,
            overwrite,
            newName: fileToMove.Name,
            cancellationToken,
            (modifiableFolder, file, src, ov, _, ct) => fallback(modifiableFolder, file, src, ov, ct));
    }

    /// <inheritdoc/>
    public async Task<IChildFile> CreateFileAsync(string name, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        var filePath = NfsHelpers.CombinePath(Path, name);
        var exists = await _nfsClient.ExistsAsync(filePath, cancellationToken);

        if (exists && !overwrite)
        {
            // When the file already exists and overwrite is false, behave as an open operation.
            return new NfsFile(_nfsClient, filePath);
        }

        using var stream = await _nfsClient.OpenStreamAsync(filePath, FileAccess.Write, create: true, cancellationToken);
        stream.SetLength(0);
        await stream.FlushAsync(cancellationToken);

        return new NfsFile(_nfsClient, filePath);
    }

    /// <inheritdoc/>
    public async Task<IChildFolder> CreateFolderAsync(string name, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        var folderPath = NfsHelpers.CombinePath(Path, name);
        var exists = await _nfsClient.ExistsAsync(folderPath, cancellationToken);

        if (overwrite && exists)
        {
            await DeleteFolderRecursiveAsync(_nfsClient, folderPath, cancellationToken);
            exists = false;
        }

        if (!exists)
        {
            await _nfsClient.MkDirAsync(folderPath, null, cancellationToken);
        }

        return new NfsFolder(_nfsClient, folderPath);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(IStorableChild item, CancellationToken cancellationToken = default)
    {
        if (item is IFolder)
        {
            await DeleteFolderRecursiveAsync(_nfsClient, item.Id, cancellationToken);
            return;
        }

        await _nfsClient.RemoveAsync(item.Id, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IStorableChild> GetFirstByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return GetItemAsync(NfsHelpers.CombinePath(Path, name), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IFolderWatcher> GetFolderWatcherAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Folder watching is not supported over NFS.");
    }

    /// <inheritdoc/>
    public async Task<IStorableChild> GetItemAsync(string id, CancellationToken cancellationToken = default)
    {
        var item = await NfsHelpers.GetStorableAsync(_nfsClient, id, cancellationToken)
            ?? throw new FileNotFoundException($"Could not find an item at NFS path \"{id}\".");

        if (item is IChildFolder folder)
            return folder;

        return (IChildFile)item;
    }

    /// <inheritdoc/>
    public Task<IStorableChild> GetItemRecursiveAsync(string id, CancellationToken cancellationToken = default)
    {
        return GetItemAsync(id, cancellationToken);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<IStorableChild> GetItemsAsync(StorableType type = StorableType.All, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (type == StorableType.None)
            throw new ArgumentOutOfRangeException(nameof(type), $"{nameof(StorableType)}.{nameof(StorableType.None)} is not valid here.");

        await foreach (var entry in _nfsClient.ReadDirStreamAsync(Path, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Skip the current and parent directory entries.
            if (entry.Name is "." or "..")
                continue;

            var entryPath = NfsHelpers.CombinePath(Path, entry.Name);
            var attrs = entry.Attributes;

            if (attrs is null)
            {
                try
                {
                    attrs = await _nfsClient.GetAttrAsync(entryPath, cancellationToken);
                }
                catch
                {
                    // If we cannot get attributes, skip the entry.
                    continue;
                }
            }

            var isDirectory = attrs.Type == NfsFileType.Directory;

            if (isDirectory && type.HasFlag(StorableType.Folder))
                yield return new NfsFolder(_nfsClient, entryPath, attrs);
            else if (!isDirectory && type.HasFlag(StorableType.File))
                yield return new NfsFile(_nfsClient, entryPath, attrs);
        }
    }

    /// <inheritdoc/>
    public Task<IFolder?> GetParentAsync(CancellationToken cancellationToken = default)
    {
        var parentPath = NfsHelpers.GetParentPath(Path);

        if (parentPath is null)
            return Task.FromResult<IFolder?>(null);

        return Task.FromResult<IFolder?>(new NfsFolder(_nfsClient, parentPath));
    }

    /// <summary>
    /// Recursively deletes all contents of a folder, then deletes the folder itself.
    /// </summary>
    private static async Task DeleteFolderRecursiveAsync(INfsClient client, string path, CancellationToken cancellationToken)
    {
        await foreach (var entry in client.ReadDirStreamAsync(path, cancellationToken))
        {
            if (entry.Name is "." or "..")
                continue;

            var entryPath = NfsHelpers.CombinePath(path, entry.Name);
            var attrs = entry.Attributes ?? await client.GetAttrAsync(entryPath, cancellationToken);

            if (attrs.Type == NfsFileType.Directory)
                await DeleteFolderRecursiveAsync(client, entryPath, cancellationToken);
            else
                await client.RemoveAsync(entryPath, cancellationToken);
        }

        await client.RmDirAsync(path, cancellationToken);
    }
}
