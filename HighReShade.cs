using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Game;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Memory;
@@ -29,12 +25,6 @@ public class Configs : TweakConfig {
        public bool HideDalamudUi;
        public bool HideGameUi;
        public bool RemoveCopyright;
        public bool UseReShade = false;

        public VirtualKey ReShadeMainKey = VirtualKey.SNAPSHOT;
        public bool ReShadeCtrl = false;
        public bool ReShadeShift = false;
        public bool ReShadeAlt = false;
    }

    public Configs Config { get; private set; }
@@ -93,44 +83,6 @@ public class Configs : TweakConfig {
        } else {
            hasChanged |= ImGui.Checkbox("Remove copyright text", ref Config.RemoveCopyright);
        }
        if (Config.UseReShade || PluginConfig.ShowExperimentalTweaks) {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
            if (ImGui.Checkbox("[Experimental]##Use ReShade to take screenshot", ref Config.UseReShade)) {
                hasChanged = true;
                DisableReShade();
                if (Config.UseReShade) TryEnableReShade();
            }
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.Text($"Use ReShade to take screenshot");
        }
        if (Config.UseReShade) {
            ImGui.Indent();
            ImGui.Indent();
            if (reShadeKeyTestHook == null) {
                ImGui.TextColored(ImGuiColors.DalamudRed, "Failed to hook ReShade.");
                ImGui.TextDisabled("\tThere is no way to fix this. If it doesn't work it doesn't work.");
            } else {
                ImGui.TextWrapped("Take a screenshot using your FFXIV screenshot keybind.\nReShade will be used to take the screenshot instead.");
                ImGui.Spacing();
                var keybindText = new List<string>();
                if (Config.ReShadeCtrl) keybindText.Add("CTRL");
                if (Config.ReShadeAlt) keybindText.Add("ALT");
                if (Config.ReShadeShift) keybindText.Add("SHIFT");
                keybindText.Add($"{Config.ReShadeMainKey.GetFancyName()}");
                
                ImGui.Text($"Current Keybind: {string.Join(" + ", keybindText)}");
                if (updatingReShadeKeybind) {
                    ImGui.TextColored(ImGuiColors.DalamudOrange, "Take a screenshot with ReShade to update the keybind.");
                } else if (ImGui.Button("Update Keybind")) {
                    updatingReShadeKeybind = true;
                }
            }
            ImGui.Unindent();
            ImGui.Unindent();
        }
    };

    public override void Setup() {
@@ -154,70 +106,8 @@ public class Configs : TweakConfig {
            Common.Hook<IsInputIDClickedDelegate>("E9 ?? ?? ?? ?? 83 7F 44 02", IsInputIDClickedDetour);
        isInputIDClickedHook?.Enable();


        if (Config.UseReShade) TryEnableReShade();

        base.Enable();
    }
    public void TryEnableReShade() {
        if (reShadeKeyTestHook == null) {
            foreach (var m in Process.GetCurrentProcess().Modules) {
                if (m is not ProcessModule pm) return;
                if (pm.FileVersionInfo?.FileDescription?.Contains("ReShade") ?? false) {
                    var scanner = new SigScanner(pm);
                    try {
                        var a = scanner.ScanText("E8 ?? ?? ?? ?? 84 C0 74 10 40 38 BE");
                        reShadeKeyTestHook = Common.Hook<ReShadeKeyTest>((nuint)a, ReShadeKeyTestDetour);
                    } catch { }
                }
            }
        }
        reShadeKeyTestHook?.Enable();
    }

    public void DisableReShade() {
        reShadeKeyTestHook?.Disable();
    }

    private byte ReShadeKeyTestDetour(byte* a1, uint mainKey, byte ctrl, byte shift, byte alt, byte a6) {
        var originalReturn = reShadeKeyTestHook.Original(a1, mainKey, ctrl, shift, alt, a6);

        if (updatingReShadeKeybind && originalReturn == 1) {
            Config.ReShadeMainKey = (VirtualKey)mainKey;
            Config.ReShadeCtrl = ctrl > 0;
            Config.ReShadeAlt = alt > 0;
            Config.ReShadeShift = shift > 0;
            updatingReShadeKeybind = false;
        }

        if (shouldPress && mainKey == (uint) Config.ReShadeMainKey && ctrl > 0 == Config.ReShadeCtrl && alt > 0 == Config.ReShadeAlt && shift > 0 == Config.ReShadeShift) {
            shouldPress = false;
            // Reset the res back to normal after the screenshot is taken
            Service.Framework.RunOnTick(() => {
                UIDebug.FreeExclusiveDraw();
                if (Config.HideGameUi) {
                    var raptureAtkModule = Framework.Instance()->GetUiModule()->GetRaptureAtkModule();
                    if (originalUiVisibility && raptureAtkModule->RaptureAtkUnitManager.Flags.HasFlag(RaptureAtkModuleFlags.UiHidden)) {
                        raptureAtkModule->SetUiVisibility(true);
                    }
                }
                var device = Device.Instance();
                if (device->Width != oldWidth || device->Height != oldHeight) {
                    device->NewWidth = oldWidth;
                    device->NewHeight = oldHeight;
                    device->RequestResolutionChange = 1;
                }
                isRunning = false;
            }, delayTicks: 60);
            return 1;
        }



        return originalReturn;
    }

    private bool shouldPress;
    private uint oldWidth;
@@ -260,7 +150,7 @@ public class Configs : TweakConfig {
            return 0;
        }

        if (a2 == ScreenshotButton && shouldPress && (Config.UseReShade == false || reShadeKeyTestHook == null)) {
            shouldPress = false;

            if (Config.RemoveCopyright && copyrightShaderAddress != 0 && originalCopyrightBytes == null) {
@@ -314,7 +204,6 @@ private static byte[] ReplaceRaw(nint address, byte[] data)
        UIDebug.FreeExclusiveDraw();
        SaveConfig(Config);
        isInputIDClickedHook?.Disable();
        DisableReShade();
        base.Disable();
    }
}