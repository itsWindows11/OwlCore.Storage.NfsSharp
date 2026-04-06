using NfsSharp.Protocol;

namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// An <see cref="ILastModifiedAtOffsetProperty"/> implementation that reads the NFS <c>mtime</c> attribute on demand.
/// </summary>
internal sealed class NfsLastModifiedAtOffsetProperty(IStorable owner, INfsClient client, string path)
    : SimpleStorageProperty<DateTimeOffset?>(
        id: owner.Id + "/" + nameof(ILastModifiedAtOffset.LastModifiedAtOffset),
        name: nameof(ILastModifiedAtOffset.LastModifiedAtOffset),
        asyncGetter: async ct =>
        {
            var attrs = await client.GetAttrAsync(path, ct);
            return attrs.ModifyTime;
        }),
    ILastModifiedAtOffsetProperty;
