using System.Runtime.InteropServices;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Dalamud.FullscreenCutscenes;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/pcutscenes";

    private delegate nint UpdateLetterboxingDelegate(nint thisPtr);

    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly ICommandManager _commandManager;
    private readonly IPluginLog _log;
    private readonly ICondition _condition;
    private readonly Configuration _config;
    private readonly Hook<UpdateLetterboxingDelegate>? _updateLetterboxingHook;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        ISigScanner sigScanner,
        IGameInteropProvider gameInteropProvider,
        ICondition condition,
        IPluginLog log)
    {
        _pluginInterface = pluginInterface;
        _commandManager = commandManager;
        _condition = condition;
        _log = log;

        _config = _pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        _commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "/pcutscenes : active/désactive le plugin\n/pcutscenes true|false : force l'état"
        });

        try
        {
            if (sigScanner.TryScanText("E8 ?? ?? ?? ?? 48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 8B ?? ?? ?? ??", out var ptr))
            {
                _updateLetterboxingHook = gameInteropProvider.HookFromAddress<UpdateLetterboxingDelegate>(ptr, UpdateLetterboxingDetour);
                _updateLetterboxingHook.Enable();
            }
            else
            {
                _log.Warning("[UltrawideCutscenes] Signature scan failed — letterbox removal inactive");
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[UltrawideCutscenes] Failed to initialize hook");
        }

        _pluginInterface.UiBuilder.OpenMainUi += ToggleEnabled;
    }

    private unsafe nint UpdateLetterboxingDetour(nint thisPtr)
    {
        if (_updateLetterboxingHook is null)
            return 0;

        if (_config.IsEnabled &&
            (_condition[ConditionFlag.OccupiedInCutSceneEvent] || _condition[ConditionFlag.WatchingCutscene78]))
        {
            ((SomeConfig*)thisPtr)->ShouldLetterBox &= ~(1 << 5);
        }
        return _updateLetterboxingHook.Original(thisPtr);
    }

    private void ToggleEnabled()
    {
        _config.IsEnabled = !_config.IsEnabled;
        SaveConfig();
    }

    private void OnCommand(string command, string args)
    {
        if (!string.IsNullOrWhiteSpace(args) && bool.TryParse(args, out var val))
            _config.IsEnabled = val;
        else
            _config.IsEnabled = !_config.IsEnabled;

        SaveConfig();
    }

    private void SaveConfig()
    {
        try
        {
            _pluginInterface.SavePluginConfig(_config);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[UltrawideCutscenes] Failed to save config");
        }
    }

    public void Dispose()
    {
        _pluginInterface.UiBuilder.OpenMainUi -= ToggleEnabled;
        _updateLetterboxingHook?.Dispose();
        _commandManager.RemoveHandler(CommandName);
    }

    [StructLayout(LayoutKind.Explicit)]
    public partial struct SomeConfig
    {
        [FieldOffset(0x40)] public int ShouldLetterBox;
    }
}
