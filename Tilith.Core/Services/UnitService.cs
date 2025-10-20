using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tilith.Core.Models;

namespace Tilith.Core.Services;

public sealed class UnitService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly FrozenDictionary<string, UnitData> _unitsByName;

    public UnitService()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Tilith.Core.Resource.brave_frontier_units.json") ??
                           throw new InvalidOperationException("Embedded resource 'brave_frontier_units.json' not found.");

        var units = JsonSerializer.Deserialize<List<UnitData>>(stream, Options) ??
                    throw new InvalidOperationException("Failed to deserialize brave_frontier_units.json.");

        Units = units.AsReadOnly();
        _unitsByName = units
                       .GroupBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
                       .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
                       .ToFrozenDictionary();
    }

    public IReadOnlyList<UnitData> Units { get; }

    public UnitData? GetByName(string name)
    {
        return _unitsByName.TryGetValue(name, out var unit) ? unit : null;
    }
}