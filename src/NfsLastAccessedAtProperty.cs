using NfsSharp.Protocol;

namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// An <see cref="ILastAccessedAtProperty"/> implementation that reads the NFS <c>atime</c> attribute on demand.
/// </summary>
internal sealed class NfsLastAccessedAtProperty(IStorable owner, INfsClient client, string path)
    : SimpleStorageProperty<DateTime?>(
        id: owner.Id + "/" + nameof(ILastAccessedAtOffset.LastAccessedAtOffset),
        name: nameof(ILastAccessedAt.LastAccessedAt),
        asyncGetter: async ct =>
        {
            var attrs = await client.GetAttrAsync(path, ct);
            return attrs.AccessTime.UtcDateTime;
        }),
    ILastAccessedAtProperty;
