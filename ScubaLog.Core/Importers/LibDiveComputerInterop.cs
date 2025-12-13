using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ScubaLog.Core.Importers;

/// <summary>
/// Placeholder interop for libdivecomputer with a platform-aware resolver.
/// Replace TODO sections with actual P/Invoke signatures.
/// </summary>
public sealed class LibDiveComputerInterop : ILibDiveComputerInterop
{
    private const string LibraryName = "libdivecomputer";
    private static bool _initialized;
    private static bool _loadSucceeded;

    public LibDiveComputerInterop()
    {
        EnsureLoaded();
    }

    public bool IsSupported => _loadSucceeded;

    private static void EnsureLoaded()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            NativeLibrary.SetDllImportResolver(typeof(LibDiveComputerInterop).Assembly, Resolve);
            // Trigger a no-op load to validate presence (replace with a real symbol later)
            _loadSucceeded = TryLoadLibrary();
        }
        catch
        {
            _loadSucceeded = false;
        }
    }

    private static bool TryLoadLibrary()
    {
        // Attempt to load the base library name; resolver will kick in
        try
        {
            IntPtr handle;
            if (NativeLibrary.TryLoad(LibraryName, typeof(LibDiveComputerInterop).Assembly, null, out handle))
            {
                NativeLibrary.Free(handle);
                return true;
            }
        }
        catch
        {
        }
        return false;
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!libraryName.Equals(LibraryName, StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;

        var baseDir = AppContext.BaseDirectory;
        string rid = GetRid();
        var candidate = Path.Combine(baseDir, "runtimes", rid, "native", GetPlatformLibraryName());
        if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var handle))
            return handle;

        return IntPtr.Zero;
    }

    private static string GetPlatformLibraryName()
    {
        if (OperatingSystem.IsWindows()) return $"{LibraryName}.dll";
        if (OperatingSystem.IsMacOS()) return $"{LibraryName}.dylib";
        return $"lib{LibraryName}.so";
    }

    private static string GetRid()
    {
        if (OperatingSystem.IsMacOS())
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        if (OperatingSystem.IsWindows())
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64";
        if (OperatingSystem.IsLinux())
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
        return "unknown";
    }
}
