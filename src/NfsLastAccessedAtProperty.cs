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
            var attrs = await client.GetAttrAsync(path, ct);
            return attrs.AccessTime.UtcDateTime;
        },
        asyncSetter: (value, ct) =>
        {
            if (!value.HasValue)
                throw new ArgumentNullException(nameof(value), "Cannot set last accessed time to null.");

            return client.SetAttrAsync(path, new NfsSetAttributes { AccessTime = new DateTimeOffset(value.Value, TimeSpan.Zero) }, ct);
        }),
    IModifiableLastAccessedAtProperty;
