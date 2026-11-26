using System;

namespace RmClient.Core.State;

public enum TeamSide
{
    Red,
    Blue,
}

public enum MapMarkerKind
{
    Attack,
    Defend,
    Alert,
}

public enum RemoteExchangeKind
{
    Ammo,
    Health,
    AirSupport,
    Energy,
}

/// <summary>
/// Normalized arena coordinate in [0, 1] x [0, 1], independent of engine vector types.
/// </summary>
public readonly record struct NormalizedPoint(float X, float Y)
{
    public static NormalizedPoint Zero => new(0f, 0f);

    public bool IsFinite => float.IsFinite(X) && float.IsFinite(Y);

    public bool IsInBounds => IsFinite && X >= 0f && X <= 1f && Y >= 0f && Y <= 1f;

    public NormalizedPoint Mirrored() => new(1f - X, 1f - Y);
}

public static class DomainLabels
{
    public static string ToCode(this TeamSide side) => side == TeamSide.Red ? "red" : "blue";

    public static string ToDisplayTitle(this TeamSide side) => side == TeamSide.Red ? "红方" : "蓝方";

    public static bool TryParseTeamSide(string? value, out TeamSide side)
    {
        if (string.Equals(value, "red", StringComparison.Ordinal))
        {
            side = TeamSide.Red;
            return true;
        }
        if (string.Equals(value, "blue", StringComparison.Ordinal))
        {
            side = TeamSide.Blue;
            return true;
        }
        side = default;
        return false;
    }

    public static string ToCode(this MapMarkerKind kind) => kind switch
    {
        MapMarkerKind.Attack => "attack",
        MapMarkerKind.Defend => "defend",
        MapMarkerKind.Alert => "alert",
        _ => "attack",
    };

    public static bool TryParseMarkerKind(string? value, out MapMarkerKind kind)
    {
        switch (value)
        {
            case "attack":
                kind = MapMarkerKind.Attack;
                return true;
            case "defend":
                kind = MapMarkerKind.Defend;
                return true;
            case "alert":
                kind = MapMarkerKind.Alert;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    public static string ToCode(this RemoteExchangeKind kind) => kind switch
    {
        RemoteExchangeKind.Ammo => "ammo",
        RemoteExchangeKind.Health => "health",
        RemoteExchangeKind.AirSupport => "air_support",
        RemoteExchangeKind.Energy => "energy",
        _ => "ammo",
    };

    public static bool TryParseExchangeKind(string? value, out RemoteExchangeKind kind)
    {
        switch (value)
        {
            case "ammo":
                kind = RemoteExchangeKind.Ammo;
                return true;
            case "health":
                kind = RemoteExchangeKind.Health;
                return true;
            case "air_support":
                kind = RemoteExchangeKind.AirSupport;
                return true;
            case "energy":
                kind = RemoteExchangeKind.Energy;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}
