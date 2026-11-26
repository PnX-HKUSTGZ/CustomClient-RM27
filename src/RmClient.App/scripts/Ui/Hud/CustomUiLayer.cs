using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RmClient.App.Ui.Hud;

/// <summary>
/// Optional host layer for local extension widget scenes.
/// Distinct from the official RM2027 0x0310/0x0311 drawing protocol renderer.
/// </summary>
public partial class CustomUiLayer : Control
{
    private readonly Dictionary<string, Control> _widgets = new(StringComparer.Ordinal);

    public Control? RegisterWidget(string id, PackedScene? scene)
    {
        if (string.IsNullOrEmpty(id) || _widgets.ContainsKey(id) || scene is null)
        {
            return null;
        }
        var instance = scene.Instantiate();
        if (instance is not Control widget)
        {
            instance.Free();
            return null;
        }
        AddChild(widget);
        _widgets[id] = widget;
        return widget;
    }

    public void RemoveWidget(string id)
    {
        if (_widgets.Remove(id, out var widget) && IsInstanceValid(widget))
        {
            widget.QueueFree();
        }
    }

    public void ClearWidgets()
    {
        foreach (var id in _widgets.Keys.ToList())
        {
            RemoveWidget(id);
        }
    }

    public int WidgetCount => _widgets.Count;
}
