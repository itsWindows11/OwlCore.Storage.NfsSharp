# OwlCore.Storage.NfsSharp

[![NuGet](https://img.shields.io/nuget/v/OwlCore.Storage.NfsSharp.svg?label=NuGet)](https://www.nuget.org/packages/OwlCore.Storage.NfsSharp)
[![NuGet Downloads](https://img.shields.io/nuget/dt/OwlCore.Storage.NfsSharp.svg)](https://www.nuget.org/packages/OwlCore.Storage.NfsSharp)
[![CI](https://github.com/itsWindows11/OwlCore.Storage.NfsSharp/actions/workflows/ci.yml/badge.svg)](https://github.com/itsWindows11/OwlCore.Storage.NfsSharp/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

An [OwlCore.Storage](https://github.com/Arlodotexe/OwlCore.Storage)-based library that provides a full-featured implementation for the NFS file system, built on top of [NfsSharp](https://github.com/itsWindows11/NfsSharp).

---

## Features

- Full `IModifiableFolder` implementation — create, delete, enumerate, copy, and move files and folders over NFS
- `IFile` with readable, writable, and read-write `NfsStream` access (fully seekable)
- **Fast-path for same-server moves** — uses an atomic NFS `RENAME` RPC instead of copy+delete
- **Fast-path for local ↔ NFS transfers** — uses `NfsClient.UploadFileFromLocalAsync` / `DownloadFileToLocalAsync` with parallel chunk I/O
- Static `GetFromNfsPathAsync` / `TryGetFromNfsPathAsync` helpers on both `NfsFile` and `NfsFolder`
- Full XML documentation on every public member
- Multi-target: **net10.0**, **net9.0**, **net8.0**, and **netstandard2.0**

---

## Installation

```
dotnet add package OwlCore.Storage.NfsSharp
```

Or search for **OwlCore.Storage.NfsSharp** in the NuGet Package Manager UI in Visual Studio.

---

## Usage

```csharp
using NfsSharp;
using OwlCore.Storage.NfsSharp;

// Connect to the NFS server and mount an export.
await using var nfs = new NfsClient("nfs.example.com", "/exports/data");
await nfs.ConnectAsync();

// Get the root folder of the mount.
var root = await NfsFolder.GetFromNfsPathAsync(nfs, "/");

// List files and sub-folders.
await foreach (var item in root.GetItemsAsync())
    Console.WriteLine(item.Name);

// Create a file and write to it.
var file = await root.CreateFileAsync("hello.txt");
await using var stream = await file.OpenStreamAsync(FileAccess.Write);
await using var writer = new StreamWriter(stream);
await writer.WriteLineAsync("Hello from OwlCore.Storage.NfsSharp!");
await stream.FlushAsync();

// Open an existing file for reading.
var existing = await NfsFile.GetFromNfsPathAsync(nfs, "/reports/q4.csv");
await using var readStream = await existing.OpenStreamAsync(FileAccess.Read);
using var reader = new StreamReader(readStream);
Console.WriteLine(await reader.ReadToEndAsync());
```

---

## Running Tests

In order to run tests, create a `.runsettings` file in the `tests/OwlCore.Storage.NfsSharp.Tests` project with the following content:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
    <RunConfiguration>
        <EnvironmentVariables>
            <NFS_SERVER>nfs.example.com</NFS_SERVER>
            <NFS_EXPORT_PATH>/exports/data</NFS_EXPORT_PATH>
        </EnvironmentVariables>
    </RunConfiguration>
</RunSettings>
```

Set the properties with their respective values, then run the tests. Note that tests require an NFS server with read and write permissions on the specified export.

---

## Versioning

Version numbering follows the Semantic Versioning approach. However, if the major version is `0`, the code is considered alpha and breaking changes may occur as a minor update.

## License

All OwlCore code is licensed under the MIT License. See the [LICENSE](LICENSE) file for more details.

