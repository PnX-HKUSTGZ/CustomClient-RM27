using System;
using Godot;
using RmClient.Transport.Abstractions;

namespace RmClient.App.Bootstrap;

/// <summary>
/// Marshals callbacks onto Godot's main scene-tree thread via CallDeferred.
/// </summary>
public sealed class GodotMainThreadDispatcher : IMainThreadDispatcher
{
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Callable.From(action).CallDeferred();
    }
}
