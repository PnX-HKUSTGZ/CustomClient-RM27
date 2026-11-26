using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using RmClient.Core.Settings;
using GodotDict = Godot.Collections.Dictionary;

namespace RmClient.App.Video;

public sealed record VideoLinkStatus(
    bool Available,
    bool Running,
    bool Live,
    string Label,
    string State,
    string Error,
    string Note,
    string Source,
    string Decoder,
    string Mode,
    string Fallback,
    int Width,
    int Height,
    float DecodeFps,
    float Fps,
    int FrameAgeMs,
    int Packets,
    int Port);

/// <summary>
/// Image-transmission adapter around the rm_video GDExtension
/// (addons/rm_video, from PnX-HKUSTGZ/rm_udp_streamer).
/// RmVideoSession owns UDP sockets, reassembly, and HEVC decoding on native threads;
/// this node only configures, polls, and stops it on the main thread.
/// </summary>
public partial class VideoLink : Node
{
    public event Action<VideoLinkStatus>? StatusChanged;

    public const string SessionClass = "RmVideoSession";
    public const string SurfaceClass = "RmVideoSurface";
    public const float PollSeconds = 0.25f;
    public const int StopDeadlineMs = 5000;
    public const int LiveFrameAgeMs = 1000;

    public const string ParameterCachePath = "user://hevc-parameters.json";
    public const string BundledParameterSource = "192.168.12.1";
    public const string BundledParameters =
        "AAAAAUABDAH//wFgAAADALAAAAMAAAMAnCwJAAAAAUIBAQFgAAADALAAAAMAAAMAnKADwIARB8vLkkKVkuP/AAEAAWoCAgIIAAADAAgAAAMB5iAAAAABRAHg98BMkA==";

    private GodotObject? _session;
    private readonly List<Control> _surfaces = new();
    private VideoStreamConfig? _config;
    private bool _running;
    private long _stoppingSince = -1;
    private double _elapsed;
    private ulong _rateClockMs;
    private int _lastDecoded;
    private int _lastSubmitted;
    private float _decodeFps;
    private float _submitFps;
    private int _parametersRevision;
    private string _parameterNote = string.Empty;
    private VideoLinkStatus _status;

    public VideoLink()
    {
        Name = "VideoLink";
        ProcessMode = ProcessModeEnum.Always;
        if (IsAvailable())
        {
            _session = ClassDB.Instantiate(SessionClass).AsGodotObject();
        }
        _status = CreateIdleStatus(IsAvailable() ? "未启用" : "视频组件未加载");
    }

    public static bool IsAvailable()
    {
        return ClassDB.ClassExists(SessionClass) && ClassDB.ClassExists(SurfaceClass);
    }

    public bool IsRunning => _running;

    public bool IsStopping => _stoppingSince >= 0;

    public VideoLinkStatus CurrentStatus => _status;

    public VideoStreamConfig? CurrentConfig => _config;

    public Control? CreateSurface()
    {
        if (_session is null)
        {
            return null;
        }
        var surface = ClassDB.Instantiate(SurfaceClass).AsGodotObject() as Control;
        if (surface is null)
        {
            return null;
        }
        surface.MouseFilter = Control.MouseFilterEnum.Ignore;
        surface.Call("set_session", _session);
        _surfaces.Add(surface);
        return surface;
    }

    public bool Start(VideoStreamConfig config)
    {
        if (_session is null)
        {
            Publish(CreateIdleStatus("视频组件未加载"));
            return false;
        }
        if (_running || _stoppingSince >= 0)
        {
            Publish(CreateIdleStatus("上一次接收仍在停止"));
            return false;
        }
        var native = BuildNativeConfig(config, out var validationError);
        if (!string.IsNullOrEmpty(validationError))
        {
            Publish(CreateIdleStatus("配置无效", validationError));
            return false;
        }

        _config = config;
        ApplyParameterCache(native);
        foreach (var surface in _surfaces)
        {
            if (IsInstanceValid(surface))
            {
                surface.Call("clear");
            }
        }

        _parametersRevision = 0;
        _lastDecoded = 0;
        _lastSubmitted = 0;
        _decodeFps = 0f;
        _submitFps = 0f;
        _rateClockMs = Time.GetTicksMsec();
        _running = _session.Call("start", native).AsBool();
        Poll();
        return _running;
    }

    public void Stop()
    {
        if (_session is null || !_running)
        {
            return;
        }
        foreach (var surface in _surfaces)
        {
            if (IsInstanceValid(surface))
            {
                surface.Call("clear");
            }
        }
        _session.Call("request_stop");
        _running = false;
        _stoppingSince = (long)Time.GetTicksMsec();
        Poll();
    }

    public override void _Process(double delta)
    {
        if (_session is null)
        {
            return;
        }
        _elapsed += delta;
        if (_elapsed < PollSeconds)
        {
            return;
        }
        _elapsed = 0.0;
        if (_running || _stoppingSince >= 0)
        {
            Poll();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete && _session is not null)
        {
            _session.Call("request_stop");
        }
    }

    private void Poll()
    {
        if (_session is null)
        {
            return;
        }
        _session.Call("poll_stop");
        var data = _session.Call("snapshot", false).AsGodotDictionary();

        if (_stoppingSince >= 0)
        {
            var waited = (long)Time.GetTicksMsec() - _stoppingSince;
            var stopTerminal = GetBool(data, "stop_terminal");
            if (stopTerminal || waited >= StopDeadlineMs)
            {
                _stoppingSince = -1;
                var note = stopTerminal ? string.Empty : "停止超时，资源保留至进程退出";
                var err = !string.IsNullOrEmpty(note) ? note : GetString(data, "render_retirement_error");
                Publish(CreateIdleStatus("已停止", err));
                return;
            }
        }

        UpdateRates(data);
        SaveLearnedParameters(GetInt(data, "hevc_parameters_revision"));

        var age = GetInt(data, "frame_age_ms", -1);
        var state = GetString(data, "state");
        var error = GetString(data, "error");
        var live = _running && age >= 0 && age < LiveFrameAgeMs && state != "Faulted";
        var packets = GetInt(data, "packets");
        var boundPort = GetInt(data, "bound_port", _config?.Port ?? 3334);

        string label;
        if (!_running && _stoppingSince >= 0)
        {
            label = "正在停止";
        }
        else if (state == "Faulted" || (!string.IsNullOrEmpty(error) && !live))
        {
            label = "故障";
        }
        else if (live)
        {
            label = "接收中";
        }
        else if (packets == 0)
        {
            label = $"等待数据 · UDP {boundPort}";
        }
        else if (age < 0)
        {
            label = "等待关键帧 / 参数";
        }
        else
        {
            label = "信号中断";
        }

        Publish(new VideoLinkStatus(
            Available: true,
            Running: _running,
            Live: live,
            Label: label,
            State: state,
            Error: error,
            Note: _parameterNote,
            Source: GetString(data, "source"),
            Decoder: GetString(data, "decoder"),
            Mode: GetString(data, "mode"),
            Fallback: GetString(data, "fallback"),
            Width: GetInt(data, "width"),
            Height: GetInt(data, "height"),
            DecodeFps: _decodeFps,
            Fps: _submitFps,
            FrameAgeMs: age,
            Packets: packets,
            Port: GetInt(data, "bound_port")));
    }

    private void UpdateRates(GodotDict data)
    {
        var now = Time.GetTicksMsec();
        var span = (now - _rateClockMs) / 1000.0f;
        if (span < 0.9f)
        {
            return;
        }
        var decoded = GetInt(data, "decoded");
        var submitted = GetInt(data, "submitted_total");
        _decodeFps = MathF.Max(decoded - _lastDecoded, 0) / span;
        _submitFps = MathF.Max(submitted - _lastSubmitted, 0) / span;
        _lastDecoded = decoded;
        _lastSubmitted = submitted;
        _rateClockMs = now;
    }

    private void Publish(VideoLinkStatus next)
    {
        if (next == _status)
        {
            return;
        }
        _status = next;
        StatusChanged?.Invoke(_status);
    }

    private VideoLinkStatus CreateIdleStatus(string label, string error = "") => new(
        Available: _session is not null,
        Running: false,
        Live: false,
        Label: label,
        State: "Stopped",
        Error: error,
        Note: _parameterNote,
        Source: string.Empty,
        Decoder: string.Empty,
        Mode: string.Empty,
        Fallback: string.Empty,
        Width: 0,
        Height: 0,
        DecodeFps: 0f,
        Fps: 0f,
        FrameAgeMs: -1,
        Packets: 0,
        Port: 0);

    private static GodotDict BuildNativeConfig(VideoStreamConfig config, out string error)
    {
        error = string.Empty;
        var bind = (config.BindAddress ?? "0.0.0.0").Trim();
        var source = (config.SourceAddress ?? string.Empty).Trim();
        var decoder = config.Decoder ?? "auto";
        var transfer = config.Transfer ?? "auto";

        if (config.Port < 1 || config.Port > 65535)
        {
            error = "端口须为 1–65535";
            return new GodotDict();
        }
        if (!UserSettingsData.IsValidIpv4(bind))
        {
            error = "监听地址须为数字 IPv4";
            return new GodotDict();
        }
        if (!string.IsNullOrEmpty(source) && !UserSettingsData.IsValidIpv4(source))
        {
            error = "来源地址须为数字 IPv4";
            return new GodotDict();
        }
        if (Array.IndexOf(UserSettingsData.ValidVideoDecoders, decoder) < 0 && decoder != "vaapi")
        {
            error = $"未知解码器 {decoder}";
            return new GodotDict();
        }
        if (Array.IndexOf(UserSettingsData.ValidVideoTransfers, transfer) < 0)
        {
            error = $"未知传输方式 {transfer}";
            return new GodotDict();
        }
        if (transfer == "gpu" && decoder != "cuda")
        {
            error = "GPU 直通需要明确选择 CUDA 解码";
            return new GodotDict();
        }

        return new GodotDict
        {
            ["version"] = 1,
            ["bind_address"] = bind,
            ["port"] = config.Port,
            ["decoder"] = decoder,
            ["device"] = "0",
            ["transfer"] = transfer,
            ["diagnostics"] = false,
            ["source_address"] = source,
            ["source_port"] = 0,
        };
    }

    private void ApplyParameterCache(GodotDict native)
    {
        if (_session is null)
        {
            return;
        }
        _parameterNote = string.Empty;
        var source = BundledParameterSource;
        var bytes = Marshalls.Base64ToRaw(BundledParameters);

        if (FileAccess.FileExists(ParameterCachePath))
        {
            if (TryReadParameterCache(out var cachedSource, out var cachedBytes, out var readError))
            {
                source = cachedSource;
                bytes = cachedBytes;
            }
            else
            {
                _parameterNote = $"未加载参数缓存，使用内置参数：{readError}";
            }
        }

        var problem = _session.Call("validate_hevc_parameters", bytes).AsString();
        if (string.IsNullOrEmpty(problem))
        {
            native["hevc_parameters"] = bytes;
            native["hevc_parameters_source"] = source;
        }
    }

    private bool TryReadParameterCache(out string source, out byte[] bytes, out string error)
    {
        source = string.Empty;
        bytes = Array.Empty<byte>();
        error = string.Empty;

        var text = FileAccess.GetFileAsString(ParameterCachePath);
        if (string.IsNullOrEmpty(text) || text.Length > 524288)
        {
            error = "文件为空或过大";
            return false;
        }
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("schema", out var schemaProp)
                || schemaProp.GetInt32() != 1)
            {
                error = "版本无效";
                return false;
            }
            var src = root.TryGetProperty("source_address", out var srcProp) ? srcProp.GetString() ?? string.Empty : string.Empty;
            if (!UserSettingsData.IsValidIpv4(src) || src == "0.0.0.0")
            {
                error = "发送端地址无效";
                return false;
            }
            var paramB64 = root.TryGetProperty("parameters", out var paramProp) ? paramProp.GetString() ?? string.Empty : string.Empty;
            var sha = root.TryGetProperty("sha256", out var shaProp) ? (shaProp.GetString() ?? string.Empty).ToLowerInvariant() : string.Empty;
            var raw = Marshalls.Base64ToRaw(paramB64);
            if (raw.Length == 0 || ComputeSha256Hex(raw) != sha)
            {
                error = "校验失败";
                return false;
            }
            if (_session is not null)
            {
                var problem = _session.Call("validate_hevc_parameters", raw).AsString();
                if (!string.IsNullOrEmpty(problem))
                {
                    error = problem;
                    return false;
                }
            }
            source = src;
            bytes = raw;
            return true;
        }
        catch (Exception)
        {
            error = "解析失败";
            return false;
        }
    }

    private void SaveLearnedParameters(int revision)
    {
        if (_session is null || revision <= 0 || revision == _parametersRevision)
        {
            return;
        }
        _parametersRevision = revision;
        var learned = _session.Call("hevc_parameters").AsGodotDictionary();
        var bytes = learned.TryGetValue("parameters", out var rawVar) ? rawVar.AsByteArray() : Array.Empty<byte>();
        var source = GetString(learned, "source_address");
        if (bytes.Length == 0 || string.IsNullOrEmpty(source) || source == "0.0.0.0")
        {
            return;
        }
        if (!string.IsNullOrEmpty(_session.Call("validate_hevc_parameters", bytes).AsString()))
        {
            return;
        }

        var record = new Dictionary<string, object>
        {
            ["schema"] = 1,
            ["source_address"] = source,
            ["parameters"] = Marshalls.RawToBase64(bytes),
            ["sha256"] = ComputeSha256Hex(bytes),
            ["saved_at_utc"] = Time.GetDatetimeStringFromSystem(utc: true),
        };

        var tempPath = ParameterCachePath + ".tmp";
        using var file = FileAccess.Open(tempPath, FileAccess.ModeFlags.Write);
        if (file is null)
        {
            _parameterNote = $"视频参数未保存：{FileAccess.GetOpenError()}";
            return;
        }
        file.StoreString(JsonSerializer.Serialize(record));
        file.Close();

        var err = DirAccess.RenameAbsolute(
            ProjectSettings.GlobalizePath(tempPath),
            ProjectSettings.GlobalizePath(ParameterCachePath));
        _parameterNote = err == Error.Ok ? string.Empty : $"视频参数未保存：{err}";
    }

    private static string ComputeSha256Hex(byte[] bytes)
    {
        var context = new HashingContext();
        context.Start(HashingContext.HashType.Sha256);
        context.Update(bytes);
        return context.Finish().HexEncode();
    }

    private static bool GetBool(GodotDict dict, string key, bool fallback = false)
    {
        return dict.TryGetValue(key, out var val) && val.VariantType == Variant.Type.Bool ? val.AsBool() : fallback;
    }

    private static int GetInt(GodotDict dict, string key, int fallback = 0)
    {
        return dict.TryGetValue(key, out var val) && val.VariantType == Variant.Type.Int ? (int)val.AsInt64() : fallback;
    }

    private static string GetString(GodotDict dict, string key, string fallback = "")
    {
        return dict.TryGetValue(key, out var val) && val.VariantType == Variant.Type.String ? val.AsString() : fallback;
    }
}
