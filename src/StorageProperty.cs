namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// A simple, disposable snapshot of a storage property value.
/// Raised <see cref="ValueUpdated"/> is never raised; the value is immutable after construction.
/// </summary>
/// <typeparam name="T">The type of the property value.</typeparam>
internal sealed class StorageProperty<T> : IStorageProperty<T>
{
    /// <summary>
    /// Initializes a new instance of <see cref="StorageProperty{T}"/> with the given value.
    /// </summary>
    public StorageProperty(T value) => Value = value;

    /// <inheritdoc/>
    public T Value { get; }

    /// <inheritdoc/>
#pragma warning disable CS0067 // Event is never used — required by IStorageProperty<T> but this is a static snapshot.
    public event EventHandler<T>? ValueUpdated;
#pragma warning restore CS0067

    /// <inheritdoc/>
    public void Dispose() { }
}
