# SQLite dependency bundle

This folder contains the Windows x64 SQLite libraries for the Unity 6000.3.24f1
proof of concept. Teammates use these checked-in files; no NuGet installation is
needed in Unity or Visual Studio. Do not add another SQLite wrapper or native DLL.

## Pinned versions

| Package | Version | Imported file |
|---|---|---|
| sqlite-net-pcl | 1.11.285 | Managed/SQLite-net.dll |
| SQLitePCLRaw.core | 3.0.3 | Managed/SQLitePCLRaw.core.dll |
| SQLitePCLRaw.provider.e_sqlite3 | 3.0.3 | Managed/SQLitePCLRaw.provider.e_sqlite3.dll |
| SourceGear.sqlite3 | 3.53.3 | x86_64/e_sqlite3.dll |

Managed files come from each package's `lib/netstandard2.0` directory. Native
SQLite comes from `runtimes/win-x64/native/e_sqlite3.dll`. This bundle targets
Windows Editor and Windows x64 players, initially using Mono and .NET Standard 2.1.
The native package version identifies SQLite 3.53.3.

`dependency-inventory.json` records each exact NuGet source URL, package SHA-256,
archive entry, and extracted file SHA-256. Licenses and original package metadata
are in `Licenses/`. sqlite-net uses MIT, SQLitePCLRaw uses Apache-2.0, and the
native package identifies SQLite as public domain. Preserve these notices when
redistributing the libraries with a player.

## Repeat the import

Close Unity first if it has loaded SQLite, then run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Import-Sqlite.ps1
```

The script downloads pinned official NuGet packages into memory, refuses a
package whose SHA-256 differs, and extracts only the four selected DLLs and
notices. Existing `.meta` files are preserved so GUIDs remain stable. It does not
install packages globally or edit Unity's generated project files. SHA-256 pins
detect changed downloads; they do not replace publisher signature verification.

## Plugin Inspector settings

Select `x86_64/e_sqlite3.dll` in Unity's Project window:

1. Disable **Any Platform**.
2. Enable **Editor**, select **Windows**, CPU **x86_64**.
3. Enable **Standalone / Windows x64**; CPU **x86_64**.
4. Disable other platforms, including Windows x86, macOS, and Linux; click Apply.

The supplied DLL metadata expresses those settings. The managed DLLs likewise
enable Windows Editor and Windows x64, with CPU AnyCPU and Validate References.
Verify Unity's interpreted Inspector settings during the first import. Restart
Unity after replacing a native DLL that has already been loaded.

Initialize once before opening a connection:

```csharp
SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
```

No Batteries/config bundle is needed for this Windows-only provider. This is
SQLitePCLRaw version 3: its native package is SourceGear.sqlite3, not the former
SQLitePCLRaw.lib.e_sqlite3 package.

## Framework dependencies and validation

The package metadata declares System.Memory >= 4.6.3. Assembly inspection shows
all three managed DLLs reference `System.Memory, Version=4.0.2.0`. The installed
Unity 6000.3.24f1 .NET Standard 2.1 facade has exactly that assembly identity
under `Editor/Data/NetStandard/compat/2.1.0/shims/netstandard/System.Memory.dll`.
Unity also supplies System.Buffers and Unsafe. Accordingly, **no System.* DLLs
are duplicated here**. NuGet package versions and .NET assembly versions are
different numbering systems.

This identity check is not proof that a Unity player works. The milestone must
still verify Editor import, native loading, create/insert/read, restart
persistence, and the actual Windows x64 built executable. IL2CPP and other
platforms have not been validated. A DllNotFoundException suggests missing
native plugin/platform settings; BadImageFormatException often indicates the
wrong CPU binary; assembly reference errors require checking Unity's selected
API profile before adding any framework DLLs.

Sources:

- https://www.nuget.org/packages/sqlite-net-pcl/1.11.285
- https://github.com/ericsink/SQLitePCL.raw/blob/main/v3.md
- https://docs.unity3d.com/6000.3/Documentation/Manual/plug-in-inspector.html
- https://docs.unity3d.com/6000.3/Documentation/Manual/dotnet-profile-support.html
