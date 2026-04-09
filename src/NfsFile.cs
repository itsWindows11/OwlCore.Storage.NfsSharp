using NfsSharp;
using NfsSharp.Protocol;

namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// An <see cref="IChildFile"/> implementation backed by a file on an NFS server.
/// </summary>
public partial class NfsFile : IChildFile, ILastModifiedAtOffset, ILastAccessedAtOffset, INfsAttributeOwner
{
    internal readonly INfsClient _nfsClient;

    private NfsFileAttributes? _cachedAttributes;
    private ILastModifiedAtProperty? _lastModifiedAt;
    private ILastModifiedAtOffsetProperty? _lastModifiedAtOffset;
    private ILastAccessedAtProperty? _lastAccessedAt;
    private ILastAccessedAtOffsetProperty? _lastAccessedAtOffset;

    /// <inheritdoc/>
    NfsFileAttributes? INfsAttributeOwner.CachedAttributes
    {
        get => _cachedAttributes;
        set => _cachedAttributes = value;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="NfsFile"/>.
    /// </summary>
    /// <param name="nfsClient">The NFS client to use for file operations.</param>
    /// <param name="path">The absolute path of the file within the NFS export (e.g. <c>/reports/q4.csv</c>).</param>
    public NfsFile(INfsClient nfsClient, string path)
    {
        _nfsClient = nfsClient;
        Path = path;
    }

    /// <summary>
    /// Gets the full NFS path of this file (e.g. <c>/reports/q4.csv</c>).
    /// </summary>
    public string Path { get; }

    /// <inheritdoc/>
    public string Id => Path;

    /// <inheritdoc/>
    public string Name => global::System.IO.Path.GetFileName(Path);

    /// <inheritdoc/>
    public ILastModifiedAtProperty LastModifiedAt =>
        _lastModifiedAt ??= new NfsLastModifiedAtProperty(this, _nfsClient, Path);

    /// <inheritdoc/>
    public ILastModifiedAtOffsetProperty LastModifiedAtOffset =>
        _lastModifiedAtOffset ??= new NfsLastModifiedAtOffsetProperty(this, _nfsClient, Path);

    /// <inheritdoc/>
    public ILastAccessedAtProperty LastAccessedAt =>
        _lastAccessedAt ??= new NfsLastAccessedAtProperty(this, _nfsClient, Path);

    /// <inheritdoc/>
    public ILastAccessedAtOffsetProperty LastAccessedAtOffset =>
        _lastAccessedAtOffset ??= new NfsLastAccessedAtOffsetProperty(this, _nfsClient, Path);

    /// <inheritdoc/>
    public async Task<IFolder?> GetParentAsync(CancellationToken cancellationToken = default)
    {
        var parentPath = NfsHelpers.GetParentPath(Path);

        if (parentPath is null)
            return null;

        return new NfsFolder(_nfsClient, parentPath);
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenStreamAsync(FileAccess accessMode, CancellationToken cancellationToken = default)
    {
        if (accessMode is not (FileAccess.Read or FileAccess.Write or FileAccess.ReadWrite))
            throw new ArgumentOutOfRangeException(nameof(accessMode));

        return await _nfsClient.OpenFileAsync(Path, accessMode, create: false, cancellationToken);
    }
}
