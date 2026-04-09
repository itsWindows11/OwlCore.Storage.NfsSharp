using NfsSharp;
using NfsSharp.Protocol;

namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// An <see cref="IModifiableLastAccessedAtOffsetProperty"/> implementation that reads and writes
/// the NFS <c>atime</c> attribute via SetAttrAsync.
/// </summary>
internal sealed class NfsLastAccessedAtOffsetProperty(INfsAttributeOwner owner, INfsClient client, string path)
    : NfsModifiableStorageProperty<DateTimeOffset?>(
        id: owner.Id + "/" + nameof(ILastAccessedAtOffset.LastAccessedAtOffset),
        name: nameof(ILastAccessedAtOffset.LastAccessedAtOffset),
        client: client,
        asyncGetter: async ct =>
        {
            owner.Attributes ??= await client.GetAttrAsync(path, ct);
            return owner.Attributes.AccessTime;
        },
        asyncSetter: async (value, ct) =>
        {
            if (!value.HasValue)
                throw new ArgumentNullException(nameof(value), "Cannot set last accessed time to null.");

            await client.SetAttrAsync(path, new NfsSetAttributes { AccessTime = value.Value }, ct);

            owner.Attributes = null;
        }),
    IModifiableLastAccessedAtOffsetProperty;
