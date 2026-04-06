using NfsSharp.Protocol;

namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// An <see cref="ILastModifiedAtProperty"/> implementation that reads the NFS <c>mtime</c> attribute on demand.
/// </summary>
internal sealed class NfsLastModifiedAtProperty(IStorable owner, INfsClient client, string path)
    : SimpleStorageProperty<DateTime?>(
        id: owner.Id + "/" + nameof(ILastModifiedAtOffset.LastModifiedAtOffset),
        name: nameof(ILastModifiedAt.LastModifiedAt),
        asyncGetter: async ct =>
        {
            var attrs = await client.GetAttrAsync(path, ct);
            return attrs.ModifyTime.UtcDateTime;
        }),
    ILastModifiedAtProperty;
