using System.Collections.Generic;
using RmClient.Core.Events;
using RmClient.Core.Events.Runtime;
using RmClient.Core.Settings;
using Xunit;

namespace RmClient.Core.Tests;

public sealed class UserSettingsTests
{
    [Fact]
    public void ApplyRawValuesClampsAndSanitizesInvalidEntries()
    {
        var data = new UserSettingsData();
        data.ApplyRawValues(new Dictionary<string, object?>
        {
            ["show_crosshair"] = false,
            ["sensitivity"] = 999f,
            ["volume"] = float.NaN,
            ["fps_limit"] = 5000,
            ["video_port"] = 70000,
            ["video_bind_address"] = "::1",
            ["video_source_address"] = "192.168.12.1",
            ["video_decoder"] = "software",
            ["video_transfer"] = "gpu", // Invalid with software decoder -> falls back to auto.
            ["control_source"] = "   ", // Empty -> keep previous.
        });

        Assert.False(data.ShowCrosshair);
        Assert.Equal(100f, data.Sensitivity);
        Assert.Equal(50f, data.Volume);
        Assert.Equal(1000, data.FpsLimit);
        Assert.Equal(65535, data.VideoPort);
        Assert.Equal("0.0.0.0", data.VideoBindAddress);
        Assert.Equal("192.168.12.1", data.VideoSourceAddress);
        Assert.Equal("software", data.VideoDecoder);
        Assert.Equal("auto", data.VideoTransfer);
        Assert.Equal("官方客户端", data.ControlSource);
    }

    [Fact]
    public void SettingsServicePersistsAndPublishesEvents()
    {
        var bus = new EventBus();
        var repo = new InMemorySettingsRepository();
        var service = new UserSettingsService(bus, repo);
        var events = 0;
        using var sub = bus.Subscribe<SettingsChangedEvent>(_ => events++);

        // Missing file returns error without changing values.
        Assert.NotEqual(0, service.Load());
        Assert.Equal(0, events);

        service.Current.FpsLimit = 144;
        service.Current.VideoDecoder = "cuda";
        service.Current.VideoTransfer = "gpu";
        Assert.Equal(0, service.Save());

        var reloaded = new UserSettingsService(bus, repo);
        Assert.Equal(0, reloaded.Load());
        Assert.Equal(144, reloaded.Current.FpsLimit);
        Assert.Equal("cuda", reloaded.Current.VideoDecoder);
        Assert.Equal("gpu", reloaded.Current.VideoTransfer);
        Assert.Equal(1, events);
    }
}
