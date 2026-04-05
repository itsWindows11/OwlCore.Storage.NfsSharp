using NfsSharp;
using NfsSharp.Protocol;

namespace OwlCore.Storage.NfsSharp;

/// <summary>
/// Shared utility methods for the NFS storage implementation.
/// </summary>
public static class NfsHelpers
{
    /// <summary>
    /// Combines a parent NFS path with a child name, always using forward-slash separators.
    /// </summary>
    /// <param name="parent">The parent path (e.g. <c>/reports</c> or <c>/</c>).</param>
    /// <param name="child">The child name (e.g. <c>q4.csv</c>).</param>
    /// <returns>The combined path (e.g. <c>/reports/q4.csv</c>).</returns>
    public static string CombinePath(string parent, string child)
    {
        var trimmed = parent.TrimEnd('/');
        return trimmed.Length == 0 ? "/" + child : trimmed + "/" + child;
    }

    /// <summary>
    /// Returns the parent path of the given NFS path, or <see langword="null"/> if the path is the root.
    /// </summary>
    /// <param name="path">The path whose parent should be returned.</param>
    /// <returns>The parent path, or <see langword="null"/> if <paramref name="path"/> is the root.</returns>
    public static string? GetParentPath(string path)
    {
        if (path == "/" || path.Length == 0)
            return null;

        var normalized = path.TrimEnd('/');
        var lastSlash = normalized.LastIndexOf('/');

        if (lastSlash <= 0)
            return "/";

        return normalized[..lastSlash];
    }

    /// <summary>
    /// Resolves an NFS path to an <see cref="IStorable"/> by inspecting the item's attributes.
    /// Returns <see langword="null"/> if the path does not exist on the server.
    /// </summary>
    /// <param name="client">The NFS client to use.</param>
    /// <param name="path">The NFS path to resolve.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    /// <returns>An <see cref="NfsFile"/> or <see cref="NfsFolder"/>, or <see langword="null"/> if not found.</returns>
    internal static async Task<IStorable?> GetStorableAsync(INfsClient client, string path, CancellationToken cancellationToken = default)
    {
        if (path == "/")
            return new NfsFolder(client, "/");

        try
        {
            var attrs = await client.GetAttrAsync(path, cancellationToken);

            return attrs.Type switch
            {
                NfsFileType.Directory => new NfsFolder(client, path, attrs),
                NfsFileType.Regular => new NfsFile(client, path, attrs),
                NfsFileType.SymbolicLink => new NfsFile(client, path, attrs),
                _ => throw new NotSupportedException($"NFS file type '{attrs.Type}' is not supported.")
            };
        }
        catch (NfsException ex) when (ex.Status == NfsStatus.NoEnt)
        {
            return null;
        }
    }
}
