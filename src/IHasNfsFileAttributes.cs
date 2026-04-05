using NfsSharp.Protocol;

namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// Indicates that an NFS storable item supports reading its NFS file attributes on demand.
/// </summary>
/// <remarks>
/// Attributes are fetched lazily — no network round-trip occurs until this method is called.
/// </remarks>
public interface IHasNfsFileAttributes
{
    /// <summary>
    /// Retrieves the NFS file attributes for this item.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    /// <returns>
    /// An <see cref="IStorageProperty{T}"/> whose <see cref="IStorageProperty{T}.Value"/> holds
    /// the <see cref="NfsFileAttributes"/> for this item.
    /// </returns>
    Task<IStorageProperty<NfsFileAttributes>> GetFileAttributesAsync(CancellationToken cancellationToken = default);
}
