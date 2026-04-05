using NfsSharp.Protocol;

namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// An <see cref="IChildFile"/> implementation backed by a file on an NFS server.
/// </summary>
public partial class NfsFile : IChildFile, IHasNfsFileAttributes
{
    internal readonly INfsClient _nfsClient;

    /// <summary>
    /// Initializes a new instance of <see cref="NfsFile"/>.
    /// </summary>
    /// <param name="nfsClient">The NFS client to use for file operations.</param>
    /// <param name="path">The absolute path of the file within the NFS export (e.g. <c>/reports/q4.csv</c>).</param>
    /// <param name="cachedAttributes">Already-fetched NFS attributes, if available. When non-<see langword="null"/> the first call to <see cref="GetFileAttributesAsync"/> returns immediately without a network round-trip.</param>
    public NfsFile(INfsClient nfsClient, string path, NfsFileAttributes? cachedAttributes = null)
    {
        _nfsClient = nfsClient;
        Path = path;
        _cachedAttributes = cachedAttributes;
    }

    private NfsFileAttributes? _cachedAttributes;

    /// <summary>
    /// Gets the full NFS path of this file (e.g. <c>/reports/q4.csv</c>).
    /// </summary>
    public string Path { get; }

    /// <inheritdoc/>
    public string Id => Path;

    /// <inheritdoc/>
    public string Name => global::System.IO.Path.GetFileName(Path);

    /// <inheritdoc/>
    public async Task<IStorageProperty<NfsFileAttributes>> GetFileAttributesAsync(CancellationToken cancellationToken = default)
    {
        _cachedAttributes ??= await _nfsClient.GetAttrAsync(Path, cancellationToken);
        return new StorageProperty<NfsFileAttributes>(_cachedAttributes);
    }

    /// <inheritdoc/>
    public async Task<IFolder?> GetParentAsync(CancellationToken cancellationToken = default)
    {
        var parentPath = NfsHelpers.GetParentPath(Path);

        if (parentPath is null)
            return null;

        return new NfsFolder(_nfsClient, parentPath);
    }

    /// <inheritdoc/>
    public Task<Stream> OpenStreamAsync(FileAccess accessMode, CancellationToken cancellationToken = default)
    {
        if (accessMode is not (FileAccess.Read or FileAccess.Write or FileAccess.ReadWrite))
            throw new ArgumentOutOfRangeException(nameof(accessMode));

        return _nfsClient.OpenStreamAsync(Path, accessMode, create: false, cancellationToken);
    }
}
