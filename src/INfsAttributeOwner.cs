using NfsSharp.Protocol;

namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// Implemented by NFS storage items (<see cref="NfsFile"/> and <see cref="NfsFolder"/>) to
/// expose a per-instance attribute cache, avoiding redundant <c>GetAttrAsync</c> network calls
/// when multiple properties are read or updated on the same item.
/// </summary>
internal interface INfsAttributeOwner
{
    /// <summary>
    /// Gets or sets the cached NFS file attributes for this item.
    /// <see langword="null"/> when the attributes have not yet been fetched or have been invalidated.
    /// </summary>
    NfsFileAttributes? CachedAttributes { get; set; }
}
