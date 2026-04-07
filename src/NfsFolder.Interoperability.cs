using NfsSharp;
using NfsSharp.Protocol;

namespace OwlCore.Storage.NfsSharp;

// Fast-paths for copy/move operations involving NFS and local (System.IO) files.
public partial class NfsFolder
{
    /// <inheritdoc cref="ICreateRenamedCopyOf.CreateCopyOfAsync"/>
    public Task<IChildFile> CreateCopyOfAsync(IFile fileToCopy, bool overwrite, string newName, CancellationToken cancellationToken, CreateRenamedCopyOfDelegate fallback)
    {
        var destPath = NfsHelpers.CombinePath(Path, newName);

        // Fast-path: NFS-to-NFS on the same client — use a direct stream copy.
        if (fileToCopy is NfsFile nfsFile && ReferenceEquals(nfsFile._nfsClient, _nfsClient))
            return NfsToNfsCopyAsync(nfsFile.Path, destPath, overwrite, cancellationToken);

        // Fast-path: local System.IO file → NFS — use the parallel upload fast-path.
        if (global::System.IO.File.Exists(fileToCopy.Id))
            return LocalToNfsCopyAsync(fileToCopy.Id, destPath, overwrite, cancellationToken);

        // Fallback: let OwlCore.Storage handle the copy via streams.
        return fallback(this, fileToCopy, overwrite, newName, cancellationToken);
    }

    /// <inheritdoc cref="IMoveRenamedFrom.MoveFromAsync"/>
    public Task<IChildFile> MoveFromAsync(IChildFile fileToMove, IModifiableFolder source, bool overwrite, string newName, CancellationToken cancellationToken, MoveRenamedFromDelegate fallback)
    {
        var destPath = NfsHelpers.CombinePath(Path, newName);

        // Fast-path: NFS-to-NFS on the same client — use an atomic server-side rename.
        if (fileToMove is NfsFile nfsFile && ReferenceEquals(nfsFile._nfsClient, _nfsClient))
            return NfsToNfsMoveAsync(nfsFile.Path, destPath, overwrite, cancellationToken);

        // Fast-path: local System.IO file → NFS — upload then delete the source.
        if (global::System.IO.File.Exists(fileToMove.Id))
            return LocalToNfsMoveAsync(fileToMove.Id, destPath, overwrite, cancellationToken);

        // Fallback: let OwlCore.Storage handle the move via streams.
        return fallback(this, fileToMove, source, overwrite, newName, cancellationToken);
    }

    private async Task<IChildFile> NfsToNfsCopyAsync(string sourcePath, string destPath, bool overwrite, CancellationToken cancellationToken)
    {
        if (!overwrite && await _nfsClient.ExistsAsync(destPath, cancellationToken))
            throw new FileAlreadyExistsException("Destination file already exists.");

        // Preserve LastModifiedAt before copying (real NFS servers copy with SETATTR after write).
        NfsFileAttributes? srcAttrs = null;
        try { srcAttrs = await _nfsClient.GetAttrAsync(sourcePath, cancellationToken); } catch { }

        await using var src = await _nfsClient.OpenFileAsync(sourcePath, FileAccess.Read, create: false, cancellationToken);
        await using var dst = await _nfsClient.OpenFileAsync(destPath, FileAccess.Write, create: true, cancellationToken);
        await src.CopyToAsync(dst, 81920, cancellationToken);
        await dst.FlushAsync(cancellationToken);

        // Restore source LastModifiedAt on the destination (like cp --preserve=timestamps).
        if (srcAttrs is not null)
        {
            try
            {
                await _nfsClient.SetAttrAsync(destPath, new NfsSetAttributes { ModifyTime = srcAttrs.ModifyTime }, cancellationToken);
            }
            catch { /* SETATTR is best-effort */ }
        }

        return new NfsFile(_nfsClient, destPath);
    }

    private async Task<IChildFile> LocalToNfsCopyAsync(string localPath, string destPath, bool overwrite, CancellationToken cancellationToken)
    {
        if (!overwrite && await _nfsClient.ExistsAsync(destPath, cancellationToken))
            throw new FileAlreadyExistsException("Destination file already exists.");

        await _nfsClient.UploadFileFromLocalAsync(localPath, destPath, 4, 4 * 1024 * 1024, null, cancellationToken);

        return new NfsFile(_nfsClient, destPath);
    }

    private async Task<IChildFile> NfsToNfsMoveAsync(string sourcePath, string destPath, bool overwrite, CancellationToken cancellationToken)
    {
        if (!overwrite && await _nfsClient.ExistsAsync(destPath, cancellationToken))
            throw new FileAlreadyExistsException("Destination file already exists.");

        await _nfsClient.RenameAsync(sourcePath, destPath, cancellationToken);

        return new NfsFile(_nfsClient, destPath);
    }

    private async Task<IChildFile> LocalToNfsMoveAsync(string localPath, string destPath, bool overwrite, CancellationToken cancellationToken)
    {
        if (!overwrite && await _nfsClient.ExistsAsync(destPath, cancellationToken))
            throw new FileAlreadyExistsException("Destination file already exists.");

        await _nfsClient.UploadFileFromLocalAsync(localPath, destPath, 4, 4 * 1024 * 1024, null, cancellationToken);
        global::System.IO.File.Delete(localPath);

        return new NfsFile(_nfsClient, destPath);
    }
}
