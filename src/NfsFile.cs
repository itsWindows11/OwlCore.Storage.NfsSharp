namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// An <see cref="IChildFile"/> implementation backed by a file on an NFS server.
/// </summary>
public partial class NfsFile : IChildFile, ILastModifiedAtOffset, ILastAccessedAtOffset
{
    internal readonly INfsClient _nfsClient;

    private IModifiableLastModifiedAtProperty? _lastModifiedAt;
    private IModifiableLastModifiedAtOffsetProperty? _lastModifiedAtOffset;
    private IModifiableLastAccessedAtProperty? _lastAccessedAt;
    private IModifiableLastAccessedAtOffsetProperty? _lastAccessedAtOffset;

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
    ILastModifiedAtProperty ILastModifiedAt.LastModifiedAt =>
        _lastModifiedAt ??= new NfsLastModifiedAtProperty(this, _nfsClient, Path);

    /// <summary>
    /// Gets the last-modified timestamp property, which also supports updating the value.
    /// </summary>
    public IModifiableLastModifiedAtProperty LastModifiedAt =>
        _lastModifiedAt ??= new NfsLastModifiedAtProperty(this, _nfsClient, Path);

    /// <inheritdoc/>
    ILastModifiedAtOffsetProperty ILastModifiedAtOffset.LastModifiedAtOffset =>
        _lastModifiedAtOffset ??= new NfsLastModifiedAtOffsetProperty(this, _nfsClient, Path);

    /// <summary>
    /// Gets the last-modified timestamp (with offset) property, which also supports updating the value.
    /// </summary>
    public IModifiableLastModifiedAtOffsetProperty LastModifiedAtOffset =>
        _lastModifiedAtOffset ??= new NfsLastModifiedAtOffsetProperty(this, _nfsClient, Path);

    /// <inheritdoc/>
    ILastAccessedAtProperty ILastAccessedAt.LastAccessedAt =>
        _lastAccessedAt ??= new NfsLastAccessedAtProperty(this, _nfsClient, Path);

    /// <summary>
    /// Gets the last-accessed timestamp property, which also supports updating the value.
    /// </summary>
    public IModifiableLastAccessedAtProperty LastAccessedAt =>
        _lastAccessedAt ??= new NfsLastAccessedAtProperty(this, _nfsClient, Path);

    /// <inheritdoc/>
    ILastAccessedAtOffsetProperty ILastAccessedAtOffset.LastAccessedAtOffset =>
        _lastAccessedAtOffset ??= new NfsLastAccessedAtOffsetProperty(this, _nfsClient, Path);

    /// <summary>
    /// Gets the last-accessed timestamp (with offset) property, which also supports updating the value.
    /// </summary>
    public IModifiableLastAccessedAtOffsetProperty LastAccessedAtOffset =>
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
    public Task<Stream> OpenStreamAsync(FileAccess accessMode, CancellationToken cancellationToken = default)
    {
        if (accessMode is not (FileAccess.Read or FileAccess.Write or FileAccess.ReadWrite))
            throw new ArgumentOutOfRangeException(nameof(accessMode));

        return _nfsClient.OpenStreamAsync(Path, accessMode, create: false, cancellationToken);
    }
}
