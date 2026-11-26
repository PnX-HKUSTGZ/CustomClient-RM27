using System;
using System.Collections.Generic;
using Godot;
using RmClient.Core.Settings;

namespace RmClient.App.Settings;

/// <summary>
/// Godot ConfigFile persistence adapter for UserSettingsService.
/// </summary>
public sealed class GodotSettingsRepository : ISettingsRepository
{
    public const string DefaultPath = "user://terminal_settings.cfg";
    public const string Section = "preferences";

    private readonly string _path;

    public GodotSettingsRepository(string path = DefaultPath)
    {
        _path = path;
    }

    public bool TryLoad(out Dictionary<string, object?> values, out int errorCode)
    {
        values = new Dictionary<string, object?>(StringComparer.Ordinal);
        var config = new ConfigFile();
        var err = config.Load(_path);
        if (err != Error.Ok)
        {
            errorCode = (int)err;
            return false;
        }

        if (config.HasSection(Section))
        {
            foreach (var key in config.GetSectionKeys(Section))
            {
                values[key] = UnwrapVariant(config.GetValue(Section, key));
            }
        }

        errorCode = 0;
        return true;
    }

    public int Save(IReadOnlyDictionary<string, object> values)
    {
        var config = new ConfigFile();
        foreach (var pair in values)
        {
            config.SetValue(Section, pair.Key, WrapVariant(pair.Value));
        }
        return (int)config.Save(_path);
    }

    private static Variant WrapVariant(object value) => value switch
    {
        bool b => Variant.From(b),
        int i => Variant.From(i),
        long l => Variant.From(l),
        float f => Variant.From(f),
        double d => Variant.From(d),
        string s => Variant.From(s),
        _ => Variant.From(value.ToString() ?? string.Empty),
    };

    private static object? UnwrapVariant(Variant value) => value.VariantType switch
    {
        Variant.Type.Bool => value.AsBool(),
        Variant.Type.Int => (int)value.AsInt64(),
        Variant.Type.Float => (float)value.AsDouble(),
        Variant.Type.String => value.AsString(),
        _ => null,
    };
}
