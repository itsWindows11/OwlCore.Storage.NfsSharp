using NfsSharp;
using NfsSharp.Protocol;

namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// An <see cref="IModifiableLastAccessedAtProperty"/> implementation that reads and writes
/// the NFS <c>atime</c> attribute via SetAttrAsync.
/// </summary>
internal sealed class NfsLastAccessedAtProperty(IStorable owner, INfsClient client, string path)
    : NfsModifiableStorageProperty<DateTime?>(
        id: owner.Id + "/" + nameof(ILastAccessedAt.LastAccessedAt),
        name: nameof(ILastAccessedAt.LastAccessedAt),
        client: client,
        asyncGetter: async ct =>
        {
            var attrOwner = (INfsAttributeOwner)owner;
            attrOwner.CachedAttributes ??= await client.GetAttrAsync(path, ct);
            return attrOwner.CachedAttributes.AccessTime.UtcDateTime;
        },
        asyncSetter: async (value, ct) =>
        {
            if (!value.HasValue)
                throw new ArgumentNullException(nameof(value), "Cannot set last accessed time to null.");

            var newTime = new DateTimeOffset(value.Value, TimeSpan.Zero);
            await client.SetAttrAsync(path, new NfsSetAttributes { AccessTime = newTime }, ct);

            ((INfsAttributeOwner)owner).CachedAttributes = null;
        }),
    IModifiableLastAccessedAtProperty;
