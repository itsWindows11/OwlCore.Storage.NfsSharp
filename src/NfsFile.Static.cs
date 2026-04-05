namespace OwlCore.Storage.NfsSharp;

// Helper methods to get files from a path directly.
public partial class NfsFile
{
    /// <summary>
    /// Gets an <see cref="NfsFile"/> from the specified NFS path.
    /// </summary>
    /// <param name="nfsClient">The NFS client to use for retrieval.</param>
    /// <param name="path">The NFS path of the file to retrieve (e.g. <c>/reports/q4.csv</c>).</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    /// <returns>An <see cref="NfsFile"/> that represents the remote file.</returns>
    /// <exception cref="FileNotFoundException">Thrown when no file is found at <paramref name="path"/>.</exception>
    public static async Task<NfsFile> GetFromNfsPathAsync(INfsClient nfsClient, string path, CancellationToken cancellationToken = default)
    {
        var file = await TryGetFromNfsPathAsync(nfsClient, path, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        return file is null
            ? throw new FileNotFoundException($"Cannot find a file on the NFS server at path \"{path}\".")
            : file;
    }

    /// <summary>
    /// Tries to get an <see cref="NfsFile"/> from the specified NFS path.
    /// </summary>
    /// <param name="nfsClient">The NFS client to use for retrieval.</param>
    /// <param name="path">The NFS path of the file to retrieve (e.g. <c>/reports/q4.csv</c>).</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    /// <returns>The <see cref="NfsFile"/> if found; <see langword="null"/> if the path does not exist or is not a file.</returns>
    public static async Task<NfsFile?> TryGetFromNfsPathAsync(INfsClient nfsClient, string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var item = await NfsHelpers.GetStorableAsync(nfsClient, path, cancellationToken);

        return item as NfsFile;
    }
}
