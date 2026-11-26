using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using RmClient.Core.Events;
using RmClient.Core.Events.Runtime;

namespace RmClient.Core.Settings;

public sealed record VideoStreamConfig(
    int Port,
    string BindAddress,
    string SourceAddress,
    string Decoder,
    string Transfer);

/// <summary>
/// Pure C# user preferences model. Loading these values never selects a real control link.
/// </summary>
public sealed class UserSettingsData
{
    public static readonly string[] ValidVideoDecoders = { "auto", "cuda", "d3d11va", "software" };
    public static readonly string[] ValidVideoTransfers = { "auto", "yuv", "rgba", "gpu" };

    public bool ShowCrosshair { get; set; } = true;
    public bool ShowMinimap { get; set; } = true;
    public bool ShowCustomUi { get; set; } = true;
    public float Sensitivity { get; set; } = 36f;
    public float Volume { get; set; } = 50f;
    public float EffectVolume { get; set; } = 50f;
    public int FpsLimit { get; set; } = 60;
    public bool Fullscreen { get; set; }
    public bool ReverseVideo { get; set; }
    public string ControlSource { get; set; } = "官方客户端";

    public bool VideoEnabled { get; set; } = true;
    public int VideoPort { get; set; } = 3334;
    public string VideoBindAddress { get; set; } = "0.0.0.0";
    public string VideoSourceAddress { get; set; } = string.Empty;
    public string VideoDecoder { get; set; } = "auto";
    public string VideoTransfer { get; set; } = "auto";

    public VideoStreamConfig ToVideoConfig() => new(
        VideoPort,
        VideoBindAddress,
        VideoSourceAddress,
        VideoDecoder,
        VideoTransfer);

    public void ApplyRawValues(IReadOnlyDictionary<string, object?> raw)
    {
        ShowCrosshair = ReadBool(raw, "show_crosshair", ShowCrosshair);
        ShowMinimap = ReadBool(raw, "show_minimap", ShowMinimap);
        ShowCustomUi = ReadBool(raw, "show_custom_ui", ShowCustomUi);
        Sensitivity = ReadFloat(raw, "sensitivity", Sensitivity, 0f, 100f);
        Volume = ReadFloat(raw, "volume", Volume, 0f, 100f);
        EffectVolume = ReadFloat(raw, "effect_volume", EffectVolume, 0f, 100f);
        FpsLimit = ReadInt(raw, "fps_limit", FpsLimit, 0, 1000);
        Fullscreen = ReadBool(raw, "fullscreen", Fullscreen);
        ReverseVideo = ReadBool(raw, "reverse_video", ReverseVideo);

        if (raw.TryGetValue("control_source", out var sourceObj) && sourceObj is string sourceRaw)
        {
            var trimmed = sourceRaw.Trim();
            if (trimmed.Length > 0 && trimmed.Length <= 64)
            {
                ControlSource = trimmed;
            }
        }

        VideoEnabled = ReadBool(raw, "video_enabled", VideoEnabled);
        VideoPort = ReadInt(raw, "video_port", VideoPort, 1, 65535);
        VideoBindAddress = ReadIpv4(raw, "video_bind_address", VideoBindAddress, allowEmpty: false);
        VideoSourceAddress = ReadIpv4(raw, "video_source_address", VideoSourceAddress, allowEmpty: true);
        VideoDecoder = ReadChoice(raw, "video_decoder", VideoDecoder, ValidVideoDecoders);
        VideoTransfer = ReadChoice(raw, "video_transfer", VideoTransfer, ValidVideoTransfers);
        if (VideoTransfer == "gpu" && VideoDecoder != "cuda")
        {
            VideoTransfer = "auto";
        }
    }

    public Dictionary<string, object> ToRawValues() => new(StringComparer.Ordinal)
    {
        ["show_crosshair"] = ShowCrosshair,
        ["show_minimap"] = ShowMinimap,
        ["show_custom_ui"] = ShowCustomUi,
        ["sensitivity"] = Sensitivity,
        ["volume"] = Volume,
        ["effect_volume"] = EffectVolume,
        ["fps_limit"] = FpsLimit,
        ["fullscreen"] = Fullscreen,
        ["reverse_video"] = ReverseVideo,
        ["control_source"] = ControlSource,
        ["video_enabled"] = VideoEnabled,
        ["video_port"] = VideoPort,
        ["video_bind_address"] = VideoBindAddress,
        ["video_source_address"] = VideoSourceAddress,
        ["video_decoder"] = VideoDecoder,
        ["video_transfer"] = VideoTransfer,
    };

    public static bool IsValidIpv4(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Contains(':'))
        {
            return false;
        }
        var parts = text.Split('.');
        if (parts.Length != 4)
        {
            return false;
        }
        foreach (var part in parts)
        {
            if (part.Length == 0 || !byte.TryParse(part, out _))
            {
                return false;
            }
        }
        return IPAddress.TryParse(text, out var address)
               && address.AddressFamily == AddressFamily.InterNetwork;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object?> raw, string key, bool fallback)
    {
        return raw.TryGetValue(key, out var val) && val is bool b ? b : fallback;
    }

    private static float ReadFloat(IReadOnlyDictionary<string, object?> raw, string key, float fallback, float min, float max)
    {
        if (!raw.TryGetValue(key, out var val) || val is null)
        {
            return fallback;
        }
        float numeric;
        switch (val)
        {
            case float f:
                numeric = f;
                break;
            case double d:
                numeric = (float)d;
                break;
            case int i:
                numeric = i;
                break;
            case long l:
                numeric = l;
                break;
            default:
                return fallback;
        }
        return float.IsFinite(numeric) ? Math.Clamp(numeric, min, max) : fallback;
    }

    private static int ReadInt(IReadOnlyDictionary<string, object?> raw, string key, int fallback, int min, int max)
    {
        if (!raw.TryGetValue(key, out var val) || val is null)
        {
            return fallback;
        }
        return val switch
        {
            int i => Math.Clamp(i, min, max),
            long l => (int)Math.Clamp(l, min, max),
            _ => fallback,
        };
    }

    private static string ReadIpv4(IReadOnlyDictionary<string, object?> raw, string key, string fallback, bool allowEmpty)
    {
        if (!raw.TryGetValue(key, out var val) || val is not string text)
        {
            return fallback;
        }
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return allowEmpty ? string.Empty : fallback;
        }
        return IsValidIpv4(trimmed) ? trimmed : fallback;
    }

    private static string ReadChoice(IReadOnlyDictionary<string, object?> raw, string key, string fallback, string[] choices)
    {
        if (!raw.TryGetValue(key, out var val) || val is not string text)
        {
            return fallback;
        }
        return choices.Contains(text, StringComparer.Ordinal) ? text : fallback;
    }
}

public interface ISettingsRepository
{
    bool TryLoad(out Dictionary<string, object?> values, out int errorCode);
    int Save(IReadOnlyDictionary<string, object> values);
}

public sealed class InMemorySettingsRepository : ISettingsRepository
{
    private Dictionary<string, object?>? _stored;

    public bool TryLoad(out Dictionary<string, object?> values, out int errorCode)
    {
        if (_stored is null)
        {
            values = new Dictionary<string, object?>(StringComparer.Ordinal);
            errorCode = 7; // File not found equivalent.
            return false;
        }
        values = new Dictionary<string, object?>(_stored, StringComparer.Ordinal);
        errorCode = 0;
        return true;
    }

    public int Save(IReadOnlyDictionary<string, object> values)
    {
        _stored = values.ToDictionary(kv => kv.Key, kv => (object?)kv.Value, StringComparer.Ordinal);
        return 0;
    }
}

public sealed class UserSettingsService
{
    private readonly IEventBus _bus;
    private readonly ISettingsRepository _repository;

    public UserSettingsData Current { get; } = new();

    public UserSettingsService(IEventBus bus, ISettingsRepository? repository = null)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _repository = repository ?? new InMemorySettingsRepository();
    }

    public int Load()
    {
        if (!_repository.TryLoad(out var raw, out var errorCode))
        {
            return errorCode;
        }
        Current.ApplyRawValues(raw);
        NotifyChanged();
        return 0;
    }

    public int Save()
    {
        return _repository.Save(Current.ToRawValues());
    }

    public void NotifyChanged()
    {
        _bus.Publish(new SettingsChangedEvent(Current));
    }
}
