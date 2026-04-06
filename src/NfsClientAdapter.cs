using NfsSharp;
using NfsSharp.Protocol;

namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// Adapts <see cref="NfsClient"/> to the <see cref="INfsClient"/> interface used by the storage layer.
/// </summary>
public sealed class NfsClientAdapter : INfsClient
{
    private readonly NfsClient _client;

    /// <summary>
    /// Initializes a new instance of <see cref="NfsClientAdapter"/> wrapping <paramref name="client"/>.
    /// </summary>
    public NfsClientAdapter(NfsClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public Task<NfsFileAttributes> GetAttrAsync(string path, CancellationToken cancellationToken = default)
        => _client.GetAttrAsync(path, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
        => _client.ExistsAsync(path, cancellationToken);

    /// <inheritdoc/>
    public async Task<Stream> OpenStreamAsync(string path, FileAccess access, bool create, CancellationToken cancellationToken = default)
        => await _client.OpenFileAsync(path, access, create, cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<NfsDirectoryEntry> ReadDirStreamAsync(string path, CancellationToken cancellationToken = default)
        => _client.ReadDirStreamAsync(path, cancellationToken);

    /// <inheritdoc/>
    public Task MkDirAsync(string path, NfsSetAttributes? attributes, CancellationToken cancellationToken = default)
        => _client.MkDirAsync(path, attributes, cancellationToken);

    /// <inheritdoc/>
    public Task RemoveAsync(string path, CancellationToken cancellationToken = default)
        => _client.RemoveAsync(path, cancellationToken);

    /// <inheritdoc/>
    public Task RmDirAsync(string path, CancellationToken cancellationToken = default)
        => _client.RmDirAsync(path, cancellationToken);

    /// <inheritdoc/>
    public Task RenameAsync(string sourcePath, string destPath, CancellationToken cancellationToken = default)
        => _client.RenameAsync(sourcePath, destPath, cancellationToken);

    /// <inheritdoc/>
    public Task SetAttrAsync(string path, NfsSetAttributes attrs, CancellationToken cancellationToken = default)
        => _client.SetAttrAsync(path, attrs, cancellationToken);

    /// <inheritdoc/>
    public Task UploadFileFromLocalAsync(string localPath, string remotePath, int parallelism, int chunkSize, IProgress<long>? progress, CancellationToken cancellationToken = default)
        => _client.UploadFileFromLocalAsync(localPath, remotePath, parallelism, chunkSize, progress, cancellationToken);

    /// <inheritdoc/>
    public void Dispose() => _client.Dispose();

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _client.DisposeAsync();
}
