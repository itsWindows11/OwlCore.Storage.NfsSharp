using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using NfsSharp.Protocol;
using OwlCore.Storage.NfsSharp;

namespace OwlCore.Storage.NfsSharp.Tests;

/// <summary>
/// An in-memory implementation of <see cref="INfsClient"/> used in tests.
/// </summary>
internal sealed class MockNfsClient : INfsClient
{
    // Each key is a normalized path. Value: true = directory, false = file.
    private readonly ConcurrentDictionary<string, bool> _isDirectory = new(StringComparer.Ordinal);
    // File contents keyed by path.
    private readonly ConcurrentDictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    public MockNfsClient()
    {
        // Root directory always exists.
        _isDirectory["/"] = true;
    }

    // --- INfsClient ---

    public Task<NfsFileAttributes> GetAttrAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        path = Normalize(path);

        if (!_isDirectory.TryGetValue(path, out var isDir))
            throw new FileNotFoundException($"No entry at '{path}'.");

        var attrs = new NfsFileAttributes
        {
            Type = isDir ? NfsFileType.Directory : NfsFileType.Regular,
            Mode = isDir ? 0b111_101_101u : 0b110_100_100u, // 755 / 644
            Size = isDir ? 0UL : (ulong)(_files.TryGetValue(path, out var data) ? data.Length : 0),
            Used = isDir ? 0UL : (ulong)(_files.TryGetValue(path, out var used) ? used.Length : 0),
            AccessTime = DateTimeOffset.UtcNow,
            ModifyTime = DateTimeOffset.UtcNow,
            ChangeTime = DateTimeOffset.UtcNow,
        };
        return Task.FromResult(attrs);
    }

    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_isDirectory.ContainsKey(Normalize(path)));
    }

    public Task<Stream> OpenStreamAsync(string path, FileAccess access, bool create, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        path = Normalize(path);

        if (create)
        {
            _isDirectory[path] = false;
            _files[path] = Array.Empty<byte>();
            EnsureParentDirectory(path);
        }
        else if (!_isDirectory.TryGetValue(path, out var isDir) || isDir)
        {
            throw new FileNotFoundException($"File not found at '{path}'.");
        }

        var contents = _files.GetOrAdd(path, Array.Empty<byte>());
        Stream stream = new MockFileStream(path, contents, this);
        return Task.FromResult(stream);
    }

    public async IAsyncEnumerable<NfsDirectoryEntry> ReadDirStreamAsync(string path, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        path = Normalize(path);

        foreach (var key in _isDirectory.Keys)
        {
            if (!IsDirectChild(path, key))
                continue;

            var attrs = await GetAttrAsync(key, cancellationToken);
            var entry = new NfsDirectoryEntry
            {
                Name = global::System.IO.Path.GetFileName(key),
                Attributes = attrs,
            };
            yield return entry;
        }
    }

    public Task MkDirAsync(string path, NfsSetAttributes? attributes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        path = Normalize(path);
        EnsureParentDirectory(path);
        _isDirectory[path] = true;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        path = Normalize(path);
        _isDirectory.TryRemove(path, out _);
        _files.TryRemove(path, out _);
        return Task.CompletedTask;
    }

    public Task RmDirAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        path = Normalize(path);
        _isDirectory.TryRemove(path, out _);
        return Task.CompletedTask;
    }

    public Task RenameAsync(string sourcePath, string destPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        sourcePath = Normalize(sourcePath);
        destPath = Normalize(destPath);

        if (!_isDirectory.TryRemove(sourcePath, out var isDir))
            throw new FileNotFoundException($"Source path '{sourcePath}' not found.");

        _isDirectory[destPath] = isDir;
        EnsureParentDirectory(destPath);

        if (!isDir && _files.TryRemove(sourcePath, out var data))
            _files[destPath] = data;

        return Task.CompletedTask;
    }

    public Task UploadFileFromLocalAsync(string localPath, string remotePath, int parallelism, int chunkSize, IProgress<long>? progress, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Local-to-mock upload is not supported in tests.");
    }

    // --- Internal helpers ---

    internal void SetFileContents(string path, byte[] contents)
    {
        path = Normalize(path);
        _isDirectory[path] = false;
        _files[path] = contents;
        EnsureParentDirectory(path);
    }

    private static string Normalize(string path)
    {
        path = path.TrimEnd('/');
        return path.Length == 0 ? "/" : path;
    }

    private static bool IsDirectChild(string parent, string candidate)
    {
        parent = Normalize(parent);
        if (string.Equals(parent, candidate, StringComparison.Ordinal))
            return false;

        var prefix = parent == "/" ? "/" : parent + "/";
        if (!candidate.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var rest = candidate[prefix.Length..];
        return rest.Length > 0 && !rest.Contains('/');
    }

    private void EnsureParentDirectory(string path)
    {
        var parent = NfsHelpers.GetParentPath(path);
        if (parent is not null && !_isDirectory.ContainsKey(parent))
        {
            _isDirectory[parent] = true;
            EnsureParentDirectory(parent);
        }
    }

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // --- MockFileStream ---

    private sealed class MockFileStream : MemoryStream
    {
        private readonly string _path;
        private readonly MockNfsClient _owner;

        public MockFileStream(string path, byte[] initial, MockNfsClient owner)
            : base(Math.Max(initial.Length, 16))
        {
            _path = path;
            _owner = owner;
            if (initial.Length > 0)
            {
                Write(initial, 0, initial.Length);
                Seek(0, SeekOrigin.Begin);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Flush();
            base.Dispose(disposing);
        }

        public override void Flush()
        {
            _owner._files[_path] = ToArray();
            base.Flush();
        }
    }
}
