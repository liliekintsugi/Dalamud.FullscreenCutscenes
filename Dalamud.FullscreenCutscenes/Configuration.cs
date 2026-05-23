using Dalamud.Configuration;
using System;

namespace Dalamud.FullscreenCutscenes;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public bool IsEnabled { get; set; } = true;
}
