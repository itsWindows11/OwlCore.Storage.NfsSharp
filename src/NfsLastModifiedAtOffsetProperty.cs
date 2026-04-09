using NfsSharp;
using NfsSharp.Protocol;

namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// An <see cref="IModifiableLastModifiedAtOffsetProperty"/> implementation that reads and writes
/// the NFS <c>mtime</c> attribute via SetAttrAsync.
/// </summary>
internal sealed class NfsLastModifiedAtOffsetProperty(INfsAttributeOwner owner, INfsClient client, string path)
    : NfsModifiableStorageProperty<DateTimeOffset?>(
        id: owner.Id + "/" + nameof(ILastModifiedAtOffset.LastModifiedAtOffset),
        name: nameof(ILastModifiedAtOffset.LastModifiedAtOffset),
        client: client,
        asyncGetter: async ct =>
        {
            owner.Attributes ??= await client.GetAttrAsync(path, ct);
            return owner.Attributes.ModifyTime;
        },
        asyncSetter: async (value, ct) =>
        {
            if (!value.HasValue)
                throw new ArgumentNullException(nameof(value), "Cannot set last modified time to null.");

            await client.SetAttrAsync(path, new NfsSetAttributes { ModifyTime = value.Value }, ct);

            owner.Attributes = null;
        }),
    IModifiableLastModifiedAtOffsetProperty;
