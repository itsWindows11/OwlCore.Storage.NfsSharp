using NfsSharp;
using NfsSharp.Protocol;

namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// An <see cref="IModifiableLastModifiedAtProperty"/> implementation that reads and writes
/// the NFS <c>mtime</c> attribute via SetAttrAsync.
/// </summary>
internal sealed class NfsLastModifiedAtProperty(IStorable owner, INfsClient client, string path)
    : NfsModifiableStorageProperty<DateTime?>(
        id: owner.Id + "/" + nameof(ILastModifiedAt.LastModifiedAt),
        name: nameof(ILastModifiedAt.LastModifiedAt),
        client: client,
        asyncGetter: async ct =>
        {
            var attrs = await client.GetAttrAsync(path, ct);
            return attrs.ModifyTime.UtcDateTime;
        },
        asyncSetter: (value, ct) =>
        {
            if (!value.HasValue)
                throw new ArgumentNullException(nameof(value), "Cannot set last modified time to null.");

            return client.SetAttrAsync(path, new NfsSetAttributes { ModifyTime = new DateTimeOffset(value.Value, TimeSpan.Zero) }, ct);
        }),
    IModifiableLastModifiedAtProperty;
