using NfsSharp;
using NfsSharp.Protocol;

namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// An <see cref="IChildFile"/> implementation backed by a file on an NFS server.
/// </summary>
public partial class NfsFile : IChildFile
{
    internal readonly NfsClient _nfsClient;

    /// <summary>
    /// Initializes a new instance of <see cref="NfsFile"/>.
    /// </summary>
    /// <param name="nfsClient">The NFS client to use for file operations.</param>
    /// <param name="path">The absolute path of the file within the NFS export (e.g. <c>/reports/q4.csv</c>).</param>
    /// <param name="attributes">The file attributes, if already known. May be <see langword="null"/>.</param>
    public NfsFile(NfsClient nfsClient, string path, NfsFileAttributes? attributes = null)
    {
        _nfsClient = nfsClient;
        Path = path;
        Attributes = attributes;
    }

    /// <summary>
    /// Gets the last-known NFS file attributes for this file, if available.
    /// </summary>
    public NfsFileAttributes? Attributes { get; }

    /// <summary>
    /// Gets the full NFS path of this file (e.g. <c>/reports/q4.csv</c>).
    /// </summary>
    public string Path { get; }

    /// <inheritdoc/>
    public string Id => Path;

    /// <inheritdoc/>
    public string Name => global::System.IO.Path.GetFileName(Path);

    /// <inheritdoc/>
    public async Task<IFolder?> GetParentAsync(CancellationToken cancellationToken = default)
    {
        var parentPath = NfsHelpers.GetParentPath(Path);

        if (parentPath is null)
            return null;

        var attrs = await _nfsClient.GetAttrAsync(parentPath, cancellationToken);
        return new NfsFolder(_nfsClient, parentPath, attrs);
    }

    /// <inheritdoc/>
    public Task<Stream> OpenStreamAsync(FileAccess accessMode, CancellationToken cancellationToken = default)
    {
        if (accessMode is not (FileAccess.Read or FileAccess.Write or FileAccess.ReadWrite))
            throw new ArgumentOutOfRangeException(nameof(accessMode));

        // NfsStream derives from Stream, but Task<NfsStream> is not assignable to Task<Stream>
        // (generics are invariant in C#). ContinueWith performs the widening without a state machine.
        return _nfsClient.OpenFileAsync(Path, accessMode, create: false, cancellationToken)
            .ContinueWith(
                static t => (Stream)t.GetAwaiter().GetResult(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }
}
