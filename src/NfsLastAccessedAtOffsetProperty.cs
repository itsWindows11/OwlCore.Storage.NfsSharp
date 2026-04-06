using NfsSharp.Protocol;

namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// An <see cref="ILastAccessedAtOffsetProperty"/> implementation that reads the NFS <c>atime</c> attribute on demand.
/// </summary>
internal sealed class NfsLastAccessedAtOffsetProperty(IStorable owner, INfsClient client, string path)
    : SimpleStorageProperty<DateTimeOffset?>(
        id: owner.Id + "/" + nameof(ILastAccessedAtOffset.LastAccessedAtOffset),
        name: nameof(ILastAccessedAtOffset.LastAccessedAtOffset),
        asyncGetter: async ct =>
        {
            var attrs = await client.GetAttrAsync(path, ct);
            return attrs.AccessTime;
        }),
    ILastAccessedAtOffsetProperty;
