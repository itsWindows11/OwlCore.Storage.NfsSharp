namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// A simple <see cref="IStoragePropertyWatcher{T}"/> that can be explicitly notified when
/// the property value changes via <see cref="RaiseValueUpdated"/>.
/// </summary>
internal sealed class NfsStoragePropertyWatcher<T>(IStorageProperty<T> property)
    : IStoragePropertyWatcher<T>, INfsPropertyNotifiable, IDisposable, IAsyncDisposable
{
    private bool _disposed;

    /// <inheritdoc/>
    public IStorageProperty<T> Property { get; } = property;

    /// <inheritdoc/>
    public event EventHandler<T>? ValueUpdated;

    /// <summary>
    /// Fires <see cref="ValueUpdated"/> with <paramref name="updatedValue"/> as the event argument,
    /// unless this watcher has been disposed.
    /// </summary>
    internal void RaiseValueUpdated(T updatedValue)
    {
        if (!_disposed)
            ValueUpdated?.Invoke(this, updatedValue);
    }

    /// <inheritdoc cref="INfsPropertyNotifiable.Notify"/>
    void INfsPropertyNotifiable.Notify(object? value) => RaiseValueUpdated((T)value!);

    /// <inheritdoc/>
    public void Dispose() => _disposed = true;

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return default;
    }
}

/// <summary>
/// Non-generic marker/notifier interface so watchers of any type can be stored in a shared registry.
/// </summary>
internal interface INfsPropertyNotifiable
{
    /// <summary>Invoke the watcher's <c>ValueUpdated</c> event (boxing the value).</summary>
    void Notify(object? value);
}
