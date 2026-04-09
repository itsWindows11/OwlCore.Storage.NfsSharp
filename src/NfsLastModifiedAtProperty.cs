using NfsSharp;
using NfsSharp.Protocol;

namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// An <see cref="IModifiableLastModifiedAtProperty"/> implementation that reads and writes
/// the NFS <c>mtime</c> attribute via SetAttrAsync.
/// </summary>
internal sealed class NfsLastModifiedAtProperty(INfsAttributeOwner owner, INfsClient client, string path)
    : NfsModifiableStorageProperty<DateTime?>(
        id: owner.Id + "/" + nameof(ILastModifiedAt.LastModifiedAt),
        name: nameof(ILastModifiedAt.LastModifiedAt),
        client: client,
        asyncGetter: async ct =>
        {
            owner.CachedAttributes ??= await client.GetAttrAsync(path, ct);
            return owner.CachedAttributes.ModifyTime.UtcDateTime;
        },
        asyncSetter: async (value, ct) =>
        {
            if (!value.HasValue)
                throw new ArgumentNullException(nameof(value), "Cannot set last modified time to null.");

            var newTime = new DateTimeOffset(value.Value, TimeSpan.Zero);
            await client.SetAttrAsync(path, new NfsSetAttributes { ModifyTime = newTime }, ct);

            owner.CachedAttributes = null;
        }),
    IModifiableLastModifiedAtProperty;
