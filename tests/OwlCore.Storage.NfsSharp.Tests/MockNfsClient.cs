using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using global::NfsSharp;
using global::NfsSharp.Protocol;

namespace OwlCore.Storage.NfsSharp.Tests;

/// <summary>
/// An in-memory implementation of <see cref="global::NfsSharp.INfsClient"/> used in tests.
/// </summary>
internal sealed class MockNfsClient : global::NfsSharp.INfsClient
{
    private static readonly NfsFileHandle s_stubHandle = new NfsFileHandle(new byte[1]);

    // Reflected internals of NfsSharp used to create NfsStream via reflection.
    private static readonly Type s_INfsProtocolClientType =
        typeof(NfsStream).Assembly.GetType("NfsSharp.Protocol.INfsProtocolClient")!;
    private static readonly Type s_NfsReadResultType =
        typeof(NfsStream).Assembly.GetType("NfsSharp.Protocol.NfsReadResult")!;
    private static readonly ConstructorInfo s_NfsReadResultCtor =
        s_NfsReadResultType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(c => c.GetParameters().Length == 2);
    private static readonly ConstructorInfo s_NfsStreamCtor =
        typeof(NfsStream).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(c => c.GetParameters().Length == 5);
    private static readonly MethodInfo s_DispatchProxyCreate =
        typeof(DispatchProxy).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == "Create" && m.IsGenericMethod && m.GetGenericArguments().Length == 2);
    private static readonly MethodInfo s_TaskFromResult =
        typeof(Task).GetMethod("FromResult")!;
    private static readonly MethodInfo s_TaskFromExceptionGeneric =
        typeof(Task).GetMethods().First(m => m.Name == "FromException" && m.IsGenericMethodDefinition);

    // Each key is a normalized path. Value: true = directory, false = file.
    private readonly ConcurrentDictionary<string, bool> _isDirectory = new(StringComparer.Ordinal);
    // File contents keyed by path.
    private readonly ConcurrentDictionary<string, byte[]> _files = new(StringComparer.Ordinal);
    // Per-path timestamps (AccessTime, ModifyTime).
    private readonly ConcurrentDictionary<string, (DateTimeOffset Access, DateTimeOffset Modify)> _timestamps = new(StringComparer.Ordinal);

    public MockNfsClient()
    {
        // Root directory always exists.
        _isDirectory["/"] = true;
    }

    // --- INfsClient properties ---

    public NfsVersion NegotiatedVersion => NfsVersion.V3;
    public NfsFileHandle RootHandle => s_stubHandle;
    public string? ExportPath => "/";

    // --- Connection ---

    public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;

    // --- Attributes ---

    public Task<NfsFileAttributes> GetAttrAsync(string path, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        path = Normalize(path);

        if (!_isDirectory.TryGetValue(path, out var isDir))
            throw new FileNotFoundException($"No entry at '{path}'.");

        _timestamps.TryGetValue(path, out var ts);
        var accessTime = ts.Access == default ? DateTimeOffset.UtcNow : ts.Access;
        var modifyTime = ts.Modify == default ? DateTimeOffset.UtcNow : ts.Modify;

        var attrs = new NfsFileAttributes
        {
            Type = isDir ? NfsFileType.Directory : NfsFileType.Regular,
            Mode = isDir ? 0b111_101_101u : 0b110_100_100u, // 755 / 644
            Size = isDir ? 0UL : (ulong)(_files.TryGetValue(path, out var data) ? data.Length : 0),
            Used = isDir ? 0UL : (ulong)(_files.TryGetValue(path, out var used) ? used.Length : 0),
            AccessTime = accessTime,
            ModifyTime = modifyTime,
            ChangeTime = modifyTime,
        };
        return Task.FromResult(attrs);
    }

    public Task<NfsFileAttributes> GetAttrAsync(NfsFileHandle handle, CancellationToken ct = default)
        => throw new NotSupportedException("Handle-based GetAttr is not supported in tests.");

    public Task SetAttrAsync(string path, NfsSetAttributes attrs, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        path = Normalize(path);

        _timestamps.AddOrUpdate(
            path,
            _ => (attrs.AccessTime ?? DateTimeOffset.UtcNow, attrs.ModifyTime ?? DateTimeOffset.UtcNow),
            (_, existing) => (attrs.AccessTime ?? existing.Access, attrs.ModifyTime ?? existing.Modify));

        return Task.CompletedTask;
    }

    public Task SetAttrAsync(NfsFileHandle handle, NfsSetAttributes attrs, CancellationToken ct = default)
        => throw new NotSupportedException("Handle-based SetAttr is not supported in tests.");

    // --- Existence ---

    public Task<bool> ExistsAsync(string path, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_isDirectory.ContainsKey(Normalize(path)));
    }

    public Task<bool> ExistsAsync(NfsFileHandle handle, CancellationToken ct = default)
        => throw new NotSupportedException("Handle-based Exists is not supported in tests.");

    // --- Namespace ---

    public Task<(NfsFileHandle Handle, NfsFileAttributes Attributes)> LookupAsync(string path, CancellationToken ct = default)
        => throw new NotSupportedException("Lookup is not supported in tests.");

    public async Task<IReadOnlyList<NfsDirectoryEntry>> ReadDirAsync(string path, CancellationToken ct = default)
    {
        var list = new List<NfsDirectoryEntry>();
        await foreach (var entry in ReadDirStreamAsync(path, ct))
            list.Add(entry);
        return list;
    }

    public Task<IReadOnlyList<NfsDirectoryEntry>> ReadDirAsync(NfsFileHandle handle, CancellationToken ct = default)
        => throw new NotSupportedException("Handle-based ReadDir is not supported in tests.");

    public async IAsyncEnumerable<NfsDirectoryEntry> ReadDirStreamAsync(string path, [EnumeratorCancellation] CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        path = Normalize(path);

        foreach (var key in _isDirectory.Keys)
        {
            if (!IsDirectChild(path, key))
                continue;

            var attrs = await GetAttrAsync(key, ct);
            var entry = new NfsDirectoryEntry
            {
                Name = global::System.IO.Path.GetFileName(key),
                Attributes = attrs,
            };
            yield return entry;
        }
    }

    public IAsyncEnumerable<NfsDirectoryEntry> ReadDirStreamAsync(NfsFileHandle handle, CancellationToken ct = default)
        => throw new NotSupportedException("Handle-based ReadDirStream is not supported in tests.");

    public IAsyncEnumerable<NfsDirectoryEntry> ReadDirRecursiveAsync(string path, CancellationToken ct = default)
        => throw new NotSupportedException("ReadDirRecursive is not supported in tests.");

    public IAsyncEnumerable<NfsDirectoryEntry> ReadDirRecursiveAsync(NfsFileHandle handle, string baseRelativePath = "", CancellationToken ct = default)
        => throw new NotSupportedException("ReadDirRecursive is not supported in tests.");

    public Task<string> ReadLinkAsync(string path, CancellationToken ct = default)
        => throw new NotSupportedException("ReadLink is not supported in tests.");

    public Task RemoveAsync(string path, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        path = Normalize(path);
        _isDirectory.TryRemove(path, out _);
        _files.TryRemove(path, out _);
        _timestamps.TryRemove(path, out _);
        return Task.CompletedTask;
    }

    public Task RmDirAsync(string path, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        path = Normalize(path);
        _isDirectory.TryRemove(path, out _);
        _timestamps.TryRemove(path, out _);
        return Task.CompletedTask;
    }

    public Task<NfsFileHandle> MkDirAsync(string path, NfsSetAttributes? attrs = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        path = Normalize(path);
        EnsureParentDirectory(path);
        _isDirectory[path] = true;
        return Task.FromResult(s_stubHandle);
    }

    public Task RenameAsync(string sourcePath, string destPath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        sourcePath = Normalize(sourcePath);
        destPath = Normalize(destPath);

        if (!_isDirectory.TryRemove(sourcePath, out var isDir))
            throw new FileNotFoundException($"Source path '{sourcePath}' not found.");

        _isDirectory[destPath] = isDir;
        EnsureParentDirectory(destPath);

        if (!isDir && _files.TryRemove(sourcePath, out var data))
            _files[destPath] = data;

        if (_timestamps.TryRemove(sourcePath, out var ts))
            _timestamps[destPath] = ts;

        return Task.CompletedTask;
    }

    public Task LinkAsync(string targetPath, string linkPath, CancellationToken ct = default)
        => throw new NotSupportedException("Link is not supported in tests.");

    public Task SymLinkAsync(string linkPath, string linkTarget, CancellationToken ct = default)
        => throw new NotSupportedException("SymLink is not supported in tests.");

    public Task<NfsFsStat> FsStatAsync(CancellationToken ct = default)
        => throw new NotSupportedException("FsStat is not supported in tests.");

    public Task<IReadOnlyList<string>> ListExportsAsync(CancellationToken ct = default)
        => throw new NotSupportedException("ListExports is not supported in tests.");

    // --- Stream-based file access ---

    /// <remarks>
    /// Constructs a <see cref="NfsStream"/> using reflection to invoke its internal constructor.
    /// The <c>INfsProtocolClient</c> parameter (also internal) is satisfied via a
    /// <see cref="DispatchProxy"/>-based proxy created reflectively at runtime.
    /// </remarks>
    public Task<NfsStream> OpenFileAsync(string path, FileAccess access = FileAccess.ReadWrite, bool create = false, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
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
        var mockStream = new MockFileStream(path, contents, this);

        // Create a DispatchProxy that implements the internal INfsProtocolClient interface.
        // DispatchProxy.Create<T, TProxy>() is called via reflection so that the internal T
        // can be used without requiring compile-time visibility.
        var proxyObj = s_DispatchProxyCreate
            .MakeGenericMethod(s_INfsProtocolClientType, typeof(MockNfsProtocolDispatchProxy))
            .Invoke(null, null)!;
        var proxy = (MockNfsProtocolDispatchProxy)proxyObj;
        proxy.BackingStream = mockStream;
        proxy.Owner = this;
        proxy.FilePath = path;

        bool readable = (access & FileAccess.Read) != 0;
        bool writable = (access & FileAccess.Write) != 0;

        // Use reflection to call the internal NfsStream(INfsProtocolClient, NfsFileHandle, long, bool, bool) ctor.
        var stream = (NfsStream)s_NfsStreamCtor.Invoke(
            new object[] { proxyObj, s_stubHandle, mockStream.Length, readable, writable });

        return Task.FromResult(stream);
    }

    // --- Parallel local file transfer fast paths ---

    public Task DownloadFileToLocalAsync(string remotePath, string localPath, int degreeOfParallelism = 4, int chunkSize = 4 * 1024 * 1024, IProgress<long>? progress = null, CancellationToken ct = default)
        => throw new NotSupportedException("Local download is not supported in tests.");

    public Task UploadFileFromLocalAsync(string localPath, string remotePath, int degreeOfParallelism = 4, int chunkSize = 4 * 1024 * 1024, IProgress<long>? progress = null, CancellationToken ct = default)
        => throw new NotSupportedException("Local-to-mock upload is not supported in tests.");

    // --- Internal helpers ---

    internal void SetFileContents(string path, byte[] contents)
    {
        path = Normalize(path);
        _isDirectory[path] = false;
        _files[path] = contents;
        EnsureParentDirectory(path);
    }

    /// <summary>
    /// Sets specific timestamps for an existing entry (used by test factory methods).
    /// </summary>
    internal void SetTimestamps(string path, DateTimeOffset? accessTime = null, DateTimeOffset? modifyTime = null)
    {
        path = Normalize(path);
        _timestamps.AddOrUpdate(
            path,
            _ => (accessTime ?? DateTimeOffset.UtcNow, modifyTime ?? DateTimeOffset.UtcNow),
            (_, existing) => (accessTime ?? existing.Access, modifyTime ?? existing.Modify));
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
            : base(initial.Length > 0 ? initial.Length : 4096)
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

    // --- MockNfsProtocolDispatchProxy ---

    /// <summary>
    /// A <see cref="DispatchProxy"/> that is created reflectively to implement the internal
    /// <c>NfsSharp.Protocol.INfsProtocolClient</c> interface, backed by a <see cref="MockFileStream"/>.
    /// Only the methods invoked by <see cref="NfsStream"/> are implemented;
    /// all other interface methods throw <see cref="NotSupportedException"/>.
    /// </summary>
    private class MockNfsProtocolDispatchProxy : DispatchProxy
    {
        internal MockFileStream BackingStream = null!;
        internal MockNfsClient Owner = null!;
        internal string FilePath = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var name = targetMethod!.Name;
            var ct = args is not null && args.Length > 0 && args[^1] is CancellationToken tok
                ? tok : CancellationToken.None;
            ct.ThrowIfCancellationRequested();

            switch (name)
            {
                case "ReadAsync":
                {
                    long offset = (long)args![1]!;
                    int count = (int)args[2]!;
                    BackingStream.Position = offset;
                    var buf = new byte[count];
                    int read = BackingStream.Read(buf, 0, count);
                    var data = read == count ? buf : buf[..read];
                    bool eof = BackingStream.Position >= BackingStream.Length;
                    // Construct internal NfsReadResult(byte[] data, bool eof) via reflection.
                    var result = s_NfsReadResultCtor.Invoke(new object[] { data, eof });
                    return s_TaskFromResult.MakeGenericMethod(s_NfsReadResultType).Invoke(null, new[] { result });
                }
                case "WriteAsync":
                {
                    long offset = (long)args![1]!;
                    var data = (byte[])args[2]!;
                    int dataOffset = (int)args[3]!;
                    int count = (int)args[4]!;
                    BackingStream.Position = offset;
                    BackingStream.Write(data, dataOffset, count);
                    return Task.FromResult(count);
                }
                case "CommitAsync":
                    BackingStream.Flush();
                    return Task.CompletedTask;

                case "GetAttrAsync":
                    return Owner.GetAttrAsync(FilePath, ct);

                case "SetAttrAsync":
                {
                    var attrs = (NfsSetAttributes)args![1]!;
                    if (attrs.Size.HasValue)
                    {
                        BackingStream.SetLength((long)attrs.Size.Value);
                        Owner._files[FilePath] = BackingStream.ToArray();
                    }
                    return Task.CompletedTask;
                }
                case "Dispose":
                    BackingStream.Dispose();
                    return null;

                default:
                {
                    var returnType = targetMethod.ReturnType;
                    if (returnType == typeof(void))
                        return null;
                    if (returnType == typeof(Task))
                        return Task.FromException(new NotSupportedException($"{name} not supported in tests."));
                    if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                    {
                        return s_TaskFromExceptionGeneric
                            .MakeGenericMethod(returnType.GenericTypeArguments[0])
                            .Invoke(null, new object[] { new NotSupportedException($"{name} not supported in tests.") });
                    }
                    throw new NotSupportedException($"{name} not supported in tests.");
                }
            }
        }
    }
}
