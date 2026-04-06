using NfsSharp.Protocol;

namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// Abstraction over <see cref="global::NfsSharp.NfsClient"/> used internally by the storage layer.
/// A concrete <see cref="NfsClientAdapter"/> wraps the real client; tests may supply an
/// in-memory implementation.
/// </summary>
public interface INfsClient : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Returns attributes for the file-system object at <paramref name="path"/>.
    /// </summary>
    Task<NfsFileAttributes> GetAttrAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <see langword="true"/> if a file-system object exists at <paramref name="path"/>.
    /// </summary>
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a stream for reading and/or writing the file at <paramref name="path"/>.
    /// </summary>
    Task<Stream> OpenStreamAsync(string path, FileAccess access, bool create, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns an async sequence of directory entries for the directory at <paramref name="path"/>.
    /// </summary>
    IAsyncEnumerable<NfsDirectoryEntry> ReadDirStreamAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a directory at <paramref name="path"/>.
    /// </summary>
    Task MkDirAsync(string path, NfsSetAttributes? attributes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the file at <paramref name="path"/>.
    /// </summary>
    Task RemoveAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the empty directory at <paramref name="path"/>.
    /// </summary>
    Task RmDirAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames / moves the file or directory at <paramref name="sourcePath"/> to <paramref name="destPath"/>.
    /// </summary>
    Task RenameAsync(string sourcePath, string destPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets attributes on the file-system object at <paramref name="path"/>.
    /// </summary>
    Task SetAttrAsync(string path, NfsSetAttributes attrs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a local file to the NFS server.
    /// </summary>
    Task UploadFileFromLocalAsync(string localPath, string remotePath, int parallelism, int chunkSize, IProgress<long>? progress, CancellationToken cancellationToken = default);
}
