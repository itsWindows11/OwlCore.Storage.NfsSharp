using NfsSharp;
using NfsSharp.Protocol;

namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// An <see cref="IModifiableLastAccessedAtOffsetProperty"/> implementation that reads and writes
/// the NFS <c>atime</c> attribute via SetAttrAsync.
/// </summary>
internal sealed class NfsLastAccessedAtOffsetProperty(IStorable owner, INfsClient client, string path)
    : NfsModifiableStorageProperty<DateTimeOffset?>(
        id: owner.Id + "/" + nameof(ILastAccessedAtOffset.LastAccessedAtOffset),
        name: nameof(ILastAccessedAtOffset.LastAccessedAtOffset),
        client: client,
        asyncGetter: async ct =>
        {
            var attrOwner = (INfsAttributeOwner)owner;
            attrOwner.CachedAttributes ??= await client.GetAttrAsync(path, ct);
            return attrOwner.CachedAttributes.AccessTime;
        },
        asyncSetter: async (value, ct) =>
        {
            if (!value.HasValue)
                throw new ArgumentNullException(nameof(value), "Cannot set last accessed time to null.");

            await client.SetAttrAsync(path, new NfsSetAttributes { AccessTime = value.Value }, ct);

            ((INfsAttributeOwner)owner).CachedAttributes = null;
        }),
    IModifiableLastAccessedAtOffsetProperty;
