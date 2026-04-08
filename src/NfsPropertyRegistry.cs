using NfsSharp;
using System.Runtime.CompilerServices;

namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// A per-client registry that allows watchers on different instances of the same property
/// (same NFS client + same property ID) to be notified together, enabling cross-instance
/// property change notifications.
/// </summary>
internal static class NfsPropertyRegistry
{
    // Key: INfsClient instance → (propertyId → list of weak-referenced watchers).
    // ConditionalWeakTable ensures the inner dict is cleaned up when the client is GC'd.
    private static readonly ConditionalWeakTable<INfsClient, Dictionary<string, List<WeakReference<INfsPropertyNotifiable>>>> _registry = new();

    /// <summary>
    /// Registers a watcher so that cross-instance notifications delivered via
    /// <see cref="NotifyAll"/> will also reach it.
    /// </summary>
    internal static void Register(INfsClient client, string propertyId, INfsPropertyNotifiable watcher)
    {
        var dict = _registry.GetOrCreateValue(client);
        lock (dict)
        {
            if (!dict.TryGetValue(propertyId, out var list))
                dict[propertyId] = list = [];
            list.Add(new WeakReference<INfsPropertyNotifiable>(watcher));
        }
    }

    /// <summary>
    /// Notifies all live watchers registered under <paramref name="propertyId"/> for the given client.
    /// Dead weak references are pruned during the traversal.
    /// </summary>
    internal static void NotifyAll(INfsClient client, string propertyId, object? boxedValue)
    {
        if (!_registry.TryGetValue(client, out var dict))
            return;

        List<WeakReference<INfsPropertyNotifiable>>? list;
        lock (dict)
        {
            if (!dict.TryGetValue(propertyId, out list))
                return;
        }

        List<WeakReference<INfsPropertyNotifiable>>? toRemove = null;
        lock (list)
        {
            foreach (var wr in list)
            {
                if (wr.TryGetTarget(out var notifiable))
                    notifiable.Notify(boxedValue);
                else
                    (toRemove ??= []).Add(wr);
            }

            if (toRemove is not null)
                foreach (var wr in toRemove)
                    list.Remove(wr);
        }
    }
}
