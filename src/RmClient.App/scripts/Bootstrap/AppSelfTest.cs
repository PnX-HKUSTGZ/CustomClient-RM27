using System;
using System.Collections.Generic;
using Godot;
using RmClient.App.Ui.Hud;
using RmClient.App.Ui.Panels;
using RmClient.Core.State;

namespace RmClient.App.Bootstrap;

/// <summary>
/// Headless/visible scene-tree integration verifier invoked via `-- --static-demo --self-test`.
/// Validates scene bindings, shortcut routing, all 11 panels, confirmation flows, map markers,
/// settings projection, and rm_video extension availability without any GDScript files.
/// </summary>
public static class AppSelfTest
{
    public static void RunAndQuit(AppRoot root)
    {
        var checks = 0;
        var failures = new List<string>();

        void Check(bool condition, string description)
        {
            checks++;
            if (!condition)
            {
                failures.Add(description);
                GD.PushError($"[SelfTest FAIL] {description}");
            }
        }

        var hud = root.GetNode<BattleHud>("%BattleHud");
        var panels = root.GetNode<PanelHost>("%PanelHost");
        var customUi = root.GetNode<CustomUiLayer>("%CustomUiLayer");

        // 1. Initial scene state & bindings
        Check(root.Store is not null && root.Commands is not null && root.Settings is not null, "Core services initialized");
        Check(string.IsNullOrEmpty(panels.CurrentPanel), "PanelHost starts closed");
        Check(hud.GetNode<Label>("%RedBaseHealth").Text == "5000", "HUD binds red base health");
        Check(hud.GetNode<Label>("%Credits").Text.Contains("650"), "HUD binds initial credits");
        Check(hud.GetNode<Label>("%Time").Text == "1:56", "HUD binds initial 116s clock");
        Check(hud.Portrait.RobotId == 3 && hud.Portrait.Side == "red", "Portrait binds initial red infantry 3");

        // 2. All 11 panels mutually exclusive
        foreach (var id in PanelHost.PanelIds)
        {
            panels.ShowPanel(id);
            var visibleCount = 0;
            foreach (var child in panels.GetChildren())
            {
                if (child is TerminalPanel tp && tp.Visible)
                {
                    visibleCount++;
                }
            }
            Check(visibleCount == 1 && panels.CurrentPanel == id, $"Single visible panel: {id}");
        }
        panels.CloseAll();
        Check(string.IsNullOrEmpty(panels.CurrentPanel), "CloseAll hides active panel");

        // 3. Shortcut routing: toggle panel + held panel restores previous panel + Esc closes
        SendKey(root, Key.P, pressed: true);
        Check(panels.CurrentPanel == PanelHost.Settings, "Key P opens settings panel");
        SendKey(root, Key.Tab, pressed: true);
        Check(panels.CurrentPanel == PanelHost.Scoreboard, "Hold Tab opens scoreboard over settings");
        SendKey(root, Key.Tab, pressed: false);
        Check(panels.CurrentPanel == PanelHost.Settings, "Release Tab restores settings panel");
        SendKey(root, Key.Quoteleft, pressed: true);
        Check(panels.CurrentPanel == PanelHost.Modules, "Hold ~ opens modules panel");
        SendKey(root, Key.Quoteleft, pressed: false);
        Check(panels.CurrentPanel == PanelHost.Settings, "Release ~ restores settings panel");
        SendKey(root, Key.F12, pressed: true);
        Check(panels.CurrentPanel == PanelHost.Help, "Hold F12 opens help panel");
        SendKey(root, Key.F12, pressed: false);
        Check(panels.CurrentPanel == PanelHost.Settings, "Release F12 restores settings panel");
        SendKey(root, Key.Escape, pressed: true);
        Check(string.IsNullOrEmpty(panels.CurrentPanel), "Esc closes panel");

        // 4. Supply17 two-step confirmation flow + event-driven HUD update
        SendKey(root, Key.O, pressed: true);
        Check(panels.CurrentPanel == PanelHost.Supply17, "Key O opens supply17");
        var supply17 = panels.GetPanel(PanelHost.Supply17) as SupplyPanel;
        Check(supply17 is not null && supply17.Caliber == 17, "Supply17 panel exported Caliber == 17");
        if (supply17 is not null)
        {
            var creditsBefore = root.Store!.State.Credits;
            var ammoBefore = root.Store.State.GetActiveRobot()!.Ammo;
            PressButton(supply17, "+20");
            PressButton(supply17, "兑换 · 需确认");
            Check(FindButton(supply17, "确认兑换") is not null, "Supply17 opens confirmation dialog");
            Check(root.Store.State.Credits == creditsBefore, "Unconfirmed purchase does not charge");
            PressButton(supply17, "取消");
            Check(root.Store.State.Credits == creditsBefore, "Cancelled purchase leaves credits unchanged");
            PressButton(supply17, "兑换 · 需确认");
            PressButton(supply17, "确认兑换");
            Check(root.Store.State.Credits == creditsBefore - 20, "Confirmed 17mm purchase deducts 20 credits");
            Check(root.Store.State.GetActiveRobot()!.Ammo == ammoBefore + 20, "Confirmed 17mm purchase adds 20 ammo");
            Check(hud.GetNode<Label>("%Ammo").Text == (ammoBefore + 20).ToString(), "HUD updates ammo via event bus");
            Check(hud.GetNode<Label>("%Credits").Text.Contains((creditsBefore - 20).ToString()), "HUD updates credits via event bus");
        }

        // 5. Supply42 export check
        SendKey(root, Key.I, pressed: true);
        Check(panels.CurrentPanel == PanelHost.Supply42, "Key I opens supply42 when map closed");
        var supply42 = panels.GetPanel(PanelHost.Supply42) as SupplyPanel;
        Check(supply42 is not null && supply42.Caliber == 42, "Supply42 panel exported Caliber == 42");

        // 6. Map panel markers + I shortcut suppression while map active
        SendKey(root, Key.M, pressed: true);
        Check(panels.CurrentPanel == PanelHost.Map, "Key M opens map panel");
        SendKey(root, Key.I, pressed: true);
        Check(panels.CurrentPanel == PanelHost.Map, "Key I does not switch to supply42 while map is open");
        var mapPanel = panels.GetPanel(PanelHost.Map) as MapPanel;
        if (mapPanel is not null)
        {
            Check(mapPanel.ArenaMapView.AddMarker(new Vector2(0.4f, 0.5f), "attack"), "ArenaMap adds valid marker");
            Check(mapPanel.ArenaMapView.MarkerCount == 1, "ArenaMap tracks marker count");
            Check(mapPanel.ArenaMapView.UndoLastMarker(), "ArenaMap undoes last marker");
            Check(mapPanel.ArenaMapView.MarkerCount == 0, "ArenaMap marker count returns to 0");
        }

        // 7. Stage panels (Rune / Deployment) exported ActionName check
        SendKey(root, Key.F, pressed: true);
        Check(panels.CurrentPanel == PanelHost.Rune, "Key F opens rune panel");
        var runePanel = panels.GetPanel(PanelHost.Rune) as StagePanel;
        Check(runePanel is not null && runePanel.ActionName == "rune", "RunePanel exported ActionName == rune");
        SendKey(root, Key.K, pressed: true);
        Check(panels.CurrentPanel == PanelHost.Deployment, "Key K opens deployment panel");
        var deployPanel = panels.GetPanel(PanelHost.Deployment) as StagePanel;
        Check(deployPanel is not null && deployPanel.ActionName == "deployment", "DeploymentPanel exported ActionName == deployment");

        // 8. Settings visibility projection & CustomUiLayer
        root.Settings!.Current.ShowCrosshair = false;
        root.Settings.Current.ShowMinimap = false;
        root.Settings.Current.ShowCustomUi = false;
        root.Settings.NotifyChanged();
        Check(!hud.Crosshair.Visible && !hud.Minimap.Visible && !customUi.Visible, "SettingsChangedEvent projects HUD/CustomUi visibility");

        // 9. Reset demo via F5
        SendKey(root, Key.F5, pressed: true);
        Check(root.Store!.State.Credits == 650 && root.Store.State.ActiveRobotId == 3, "F5 resets demo state");

        // 10. Native rm_video GDExtension registered
        Check(Video.VideoLink.IsAvailable(), "rm_video GDExtension classes RmVideoSession & RmVideoSurface available");

        panels.CloseAll();
        GD.Print($"Godot C# SelfTest summary: {checks - failures.Count}/{checks} passed, {failures.Count} failed.");
        root.GetTree().Quit(failures.Count == 0 ? 0 : 1);
    }

    private static void SendKey(AppRoot root, Key key, bool pressed)
    {
        var ev = new InputEventKey
        {
            Keycode = key,
            PhysicalKeycode = key,
            Pressed = pressed,
        };
        root._Input(ev);
    }

    private static Button? FindButton(Node root, string text)
    {
        if (root is Button btn && btn.Visible && btn.Text == text)
        {
            return btn;
        }
        foreach (var child in root.GetChildren())
        {
            var found = FindButton(child, text);
            if (found is not null)
            {
                return found;
            }
        }
        return null;
    }

    private static void PressButton(Node root, string text)
    {
        var btn = FindButton(root, text);
        btn?.EmitSignal(BaseButton.SignalName.Pressed);
    }
}
