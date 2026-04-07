using NfsSharp;

namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// A property class for NFS storage items that implements both <see cref="IMutableStorageProperty{T}"/>
/// (with watcher support) and <see cref="IModifiableStorageProperty{T}"/> (with value-setting support).
/// When <see cref="IModifiableStorageProperty{T}.UpdateValueAsync"/> is called, all live watchers
/// for this property — including those on other <see cref="NfsFile"/> or <see cref="NfsFolder"/>
/// instances backed by the same path — are notified via a per-client registry.
/// </summary>
internal abstract class NfsModifiableStorageProperty<T> : SimpleMutableStorageProperty<T>, IModifiableStorageProperty<T>
{
    private readonly INfsClient _client;
    private readonly Func<T, CancellationToken, Task> _asyncSetter;

    /// <param name="id">A unique identifier for this property.</param>
    /// <param name="name">The display name of this property.</param>
    /// <param name="client">The NFS client this property belongs to.</param>
    /// <param name="asyncGetter">Retrieves the current value from the NFS server.</param>
    /// <param name="asyncSetter">Sends the new value to the NFS server.</param>
    protected NfsModifiableStorageProperty(
        string id,
        string name,
        INfsClient client,
        Func<CancellationToken, Task<T>> asyncGetter,
        Func<T, CancellationToken, Task> asyncSetter)
        : base(id, name, asyncGetter)
    {
        _client = client;
        _asyncSetter = asyncSetter;
    }

    /// <inheritdoc/>
    public override Task<IStoragePropertyWatcher<T>> GetWatcherAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var watcher = new NfsStoragePropertyWatcher<T>(this);
        NfsPropertyRegistry.Register(_client, Id, watcher);
        return Task.FromResult<IStoragePropertyWatcher<T>>(watcher);
    }

    /// <inheritdoc/>
    async Task IModifiableStorageProperty<T>.UpdateValueAsync(T newValue, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _asyncSetter(newValue, cancellationToken);
        // Notify all watchers for this property ID on this client (cross-instance support).
        NfsPropertyRegistry.NotifyAll(_client, Id, newValue);
    }
}
