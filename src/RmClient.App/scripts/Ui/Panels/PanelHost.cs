using System;
using System.Collections.Generic;
using Godot;
using RmClient.Core.Commands;
using RmClient.Core.Settings;
using RmClient.Core.State;

namespace RmClient.App.Ui.Panels;

/// <summary>
/// Manages mutually exclusive modal panel visibility. Keyboard shortcuts remain in AppRoot.
/// </summary>
public partial class PanelHost : Control
{
    public event Action? ClearRequested;
    public event Action<string>? PanelChanged;

    public const string Scoreboard = "scoreboard";
    public const string Modules = "modules";
    public const string Settings = "settings";
    public const string Supply17 = "supply17";
    public const string Supply42 = "supply42";
    public const string Map = "map";
    public const string Help = "help";
    public const string Exchange = "exchange";
    public const string Rune = "rune";
    public const string Deployment = "deployment";
    public const string Settlement = "settlement";

    public static readonly string[] PanelIds =
    {
        Scoreboard, Modules, Settings, Supply17, Supply42, Map, Help, Exchange, Rune, Deployment, Settlement,
    };

    private static readonly Dictionary<string, string> PanelPaths = new(StringComparer.Ordinal)
    {
        [Scoreboard] = "Scoreboard",
        [Modules] = "Modules",
        [Settings] = "Settings",
        [Supply17] = "Supply17",
        [Supply42] = "Supply42",
        [Map] = "Map",
        [Help] = "Help",
        [Exchange] = "Exchange",
        [Rune] = "Rune",
        [Deployment] = "Deployment",
        [Settlement] = "Settlement",
    };

    private readonly Dictionary<string, TerminalPanel> _panels = new(StringComparer.Ordinal);
    private string _current = string.Empty;
    private TerminalStore? _store;
    private TerminalCommands? _commands;
    private UserSettingsService? _settings;

    public string CurrentPanel => _current;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        foreach (var id in PanelIds)
        {
            var panel = GetNodeOrNull<TerminalPanel>(PanelPaths[id]);
            if (panel is null)
            {
                GD.PushError($"Missing terminal panel: {id}");
                continue;
            }
            _panels[id] = panel;
            var capturedId = id;
            panel.CloseRequested += () => HidePanel(capturedId);
            panel.SetActive(false);
        }

        if (_panels.TryGetValue(Settings, out var settingsPanel) && settingsPanel is SettingsPanel preferences)
        {
            preferences.ClearRequested += () => ClearRequested?.Invoke();
        }

        if (_store is not null && _commands is not null && _settings is not null)
        {
            BindChildren();
        }
        if (!string.IsNullOrEmpty(_current))
        {
            ShowPanel(_current);
        }
    }

    public void Bind(TerminalStore store, TerminalCommands commands, UserSettingsService settings)
    {
        _store = store;
        _commands = commands;
        _settings = settings;
        if (IsNodeReady())
        {
            BindChildren();
        }
    }

    private void BindChildren()
    {
        if (_store is null || _commands is null || _settings is null)
        {
            return;
        }
        foreach (var panel in _panels.Values)
        {
            panel.Bind(_store, _commands, _settings);
        }
    }

    public void TogglePanel(string id)
    {
        if (_current == id)
        {
            HidePanel(id);
        }
        else
        {
            ShowPanel(id);
        }
    }

    public void ShowPanel(string id)
    {
        if (!_panels.ContainsKey(id) || _current == id)
        {
            return;
        }
        if (_panels.TryGetValue(_current, out var activePanel))
        {
            activePanel.SetActive(false);
        }
        _current = id;
        _panels[id].SetActive(true);
        PanelChanged?.Invoke(_current);
    }

    public void HidePanel(string id)
    {
        if (_panels.TryGetValue(id, out var panel))
        {
            panel.SetActive(false);
        }
        if (_current == id)
        {
            _current = string.Empty;
            PanelChanged?.Invoke(_current);
        }
    }

    public void CloseAll()
    {
        foreach (var panel in _panels.Values)
        {
            panel.SetActive(false);
        }
        _current = string.Empty;
        PanelChanged?.Invoke(_current);
    }

    public TerminalPanel? GetPanel(string id)
    {
        return _panels.TryGetValue(id, out var panel) ? panel : null;
    }
}
