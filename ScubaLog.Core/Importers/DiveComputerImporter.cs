using System.Collections.Generic;
using System.Threading.Tasks;
using ScubaLog.Core.Models;

namespace ScubaLog.Core.Importers;

public enum ImportScope
{
    AllDives,
    NewOnly,
    NotYetImported
}

public sealed record DiveComputerModel(string Manufacturer, string Model, string? Protocol = null);

public interface IDiveComputerImporter
{
    Task<List<Dive>> ImportAsync(DiveComputerModel computer, ImportScope scope);
}

/// <summary>
/// Stub importer that stands in for libdivecomputer integration.
/// Replace with a real implementation once native bindings are wired.
/// </summary>
public class LibDiveComputerImporter : IDiveComputerImporter
{
    private readonly ILibDiveComputerInterop _interop;

    public LibDiveComputerImporter(ILibDiveComputerInterop? interop = null)
    {
        _interop = interop ?? new NoopLibDiveComputerInterop();
    }

    public Task<List<Dive>> ImportAsync(DiveComputerModel computer, ImportScope scope)
    {
        if (!_interop.IsSupported)
            return Task.FromResult(new List<Dive>());

        // TODO: replace with real libdivecomputer calls
        return Task.FromResult(new List<Dive>());
    }
}

public interface ILibDiveComputerInterop
{
    bool IsSupported { get; }
}

internal sealed class NoopLibDiveComputerInterop : ILibDiveComputerInterop
{
    public bool IsSupported => false;
}
