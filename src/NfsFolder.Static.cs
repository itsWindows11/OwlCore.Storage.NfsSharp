using NfsSharp;

namespace OwlCore.Storage.NfsSharp;

// Helper methods to get folders from a path directly.
public partial class NfsFolder
{
    /// <summary>
    /// Gets an <see cref="NfsFolder"/> from the specified NFS path.
    /// </summary>
    /// <param name="nfsClient">The NFS client to use for retrieval.</param>
    /// <param name="path">The NFS path of the folder to retrieve (e.g. <c>/reports</c> or <c>/</c> for root).</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    /// <returns>An <see cref="NfsFolder"/> that represents the remote directory.</returns>
    /// <exception cref="FileNotFoundException">Thrown when no directory is found at <paramref name="path"/>.</exception>
    public static async Task<NfsFolder> GetFromNfsPathAsync(INfsClient nfsClient, string path, CancellationToken cancellationToken = default)
    {
        var folder = await TryGetFromNfsPathAsync(nfsClient, path, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        return folder is null
            ? throw new FileNotFoundException($"Cannot find a directory on the NFS server at path \"{path}\".")
            : folder;
    }

    /// <summary>
    /// Tries to get an <see cref="NfsFolder"/> from the specified NFS path.
    /// </summary>
    /// <param name="nfsClient">The NFS client to use for retrieval.</param>
    /// <param name="path">The NFS path of the folder to retrieve (e.g. <c>/reports</c> or <c>/</c> for root).</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    /// <returns>The <see cref="NfsFolder"/> if found; <see langword="null"/> if the path does not exist or is not a directory.</returns>
    public static async Task<NfsFolder?> TryGetFromNfsPathAsync(INfsClient nfsClient, string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var item = await NfsHelpers.GetStorableAsync(nfsClient, path, cancellationToken);

        return item as NfsFolder;
    }
}
