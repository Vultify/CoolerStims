using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace CoolerStimsClient
{
    [BepInPlugin("com.vultify.coolerstims", "CoolerStims", "1.3.0")]
    public class CoolerStimsClientPlugin : BaseUnityPlugin
    {
        private class Reskin
        {
            public string TextureFile;
            public Texture2D Texture;
        }

        // Custom stim template id -> reskin texture for the in-hands injector.
        // All injector stims share one in-hands rig; the game assigns the syringe material
        // at runtime keyed by template id, and unknown (custom) ids get an arbitrary vanilla
        // stim's material — so we target the syringe renderers by NAME, not by material,
        // and override whatever material the game picked.
        private static readonly Dictionary<string, Reskin> Reskins = new Dictionary<string, Reskin>
        {
            { "5c0a1b2c3d4e5f6789abcde1", new Reskin { TextureFile = "item_stimulator_apex_d.png"  } }, // APEX
            { "5c0a1b2c3d4e5f6789abcde3", new Reskin { TextureFile = "item_stimulator_aegis_d.png" } }, // AEGIS
            { "5c0a1b2c3d4e5f6789abcde5", new Reskin { TextureFile = "item_stimulator_iron_d.png"  } }, // IRON
            { "5c0a1b2c3d4e5f6789abcde6", new Reskin { TextureFile = "item_stimulator_argus_d.png" } }, // ARGUS
            { "5c0a1b2c3d4e5f6789abcde8", new Reskin { TextureFile = "item_stimulator_graft_d.png" } }, // GRAFT
        };

        // The shared injector rig's renderer names (same for every stim's container prefab).
        private static readonly HashSet<string> SyringeRendererNames = new HashSet<string>
        {
            "syringe_LOD0",
            "syringe_needle_LOD0",
            "syringe_cap_LOD0",
        };

        // The rig is pooled and reused across stims; remember each material instance's
        // game-assigned texture so a later vanilla use can be restored if needed.
        private readonly Dictionary<int, Texture> _originalTextures = new Dictionary<int, Texture>();
        private readonly HashSet<Texture> _ownTextures = new HashSet<Texture>();

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<bool> _debug;
        private Player.AbstractHandsController _lastController;
        private bool _active;
        private bool _loggedThisDraw;
        private int _attempts;

        // If the rig's renderers never even appear, stop scanning after ~5 s.
        private const int MaxAttempts = 300;

        private void Awake()
        {
            _enabled = Config.Bind("1. General", "Custom skins in hands", true,
                "Show the CoolerStims custom skins on the injector during the use animation.");
            _debug = Config.Bind("2. Debug", "Debug logging", false,
                "Log hands-controller changes and renderer/material details to the BepInEx console/log.");
            LoadTextures();
        }

        private void DebugLog(string message)
        {
            if (_debug.Value)
            {
                Logger.LogInfo("[CoolerStims] " + message);
            }
        }

        private void LoadTextures()
        {
            string dir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "textures");
            foreach (var reskin in Reskins.Values)
            {
                string path = Path.Combine(dir, reskin.TextureFile);
                if (!File.Exists(path))
                {
                    Logger.LogWarning($"CoolerStims: texture not found, skipping in-hands reskin: {path}");
                    continue;
                }
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (tex.LoadImage(File.ReadAllBytes(path)))
                {
                    tex.Compress(true);
                    reskin.Texture = tex;
                    _ownTextures.Add(tex);
                }
                else
                {
                    Logger.LogWarning($"CoolerStims: failed to decode texture: {path}");
                    Destroy(tex);
                }
            }
        }

        private void Update()
        {
            try
            {
                if (!_enabled.Value)
                {
                    return;
                }

                var player = Singleton<GameWorld>.Instance?.MainPlayer;
                var controller = player?.HandsController;
                if (controller != _lastController)
                {
                    _lastController = controller;
                    _active = controller is Player.MedsController || controller is Player.UsableItemController;
                    _loggedThisDraw = false;
                    _attempts = 0;
                    if (controller != null)
                    {
                        DebugLog($"hands controller changed: {controller.GetType().Name}, tpl={controller.Item?.TemplateId.ToString() ?? "null"}, watching={_active}");
                    }
                }

                // Re-assert every frame while the item is in hands: the game assigns the
                // syringe material itself at some point during the draw, and this way we
                // win no matter when that happens.
                if (_active && controller != null)
                {
                    if (ApplyTick(controller))
                    {
                        if (!_loggedThisDraw)
                        {
                            _loggedThisDraw = true;
                            DebugLog($"syringe renderers found (frame {_attempts}); texture override active");
                        }
                    }
                    else if (++_attempts > MaxAttempts)
                    {
                        DebugLog("no syringe renderers appeared; giving up for this draw");
                        _active = false;
                    }
                }
            }
            catch (System.Exception e)
            {
                if (_debug != null && _debug.Value)
                {
                    Logger.LogError("[CoolerStims] Update error: " + e);
                }
            }
        }

        private bool ApplyTick(Player.AbstractHandsController controller)
        {
            var item = controller.Item;
            if (item == null)
            {
                return false;
            }

            Reskins.TryGetValue(item.TemplateId.ToString(), out var reskin);
            var root = controller.ControllerGameObject;
            if (root == null)
            {
                return false;
            }

            bool found = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!SyringeRendererNames.Contains(renderer.gameObject.name))
                {
                    continue;
                }
                found = true;

                if (reskin != null && reskin.Texture != null)
                {
                    // .materials (never .sharedMaterials): the assigned material asset is
                    // shared with the vanilla stims' world/loot models.
                    foreach (var material in renderer.materials)
                    {
                        if (material == null)
                        {
                            continue;
                        }
                        var current = material.GetTexture("_MainTex");
                        if (current == reskin.Texture)
                        {
                            continue;
                        }
                        int id = material.GetInstanceID();
                        if (!_ownTextures.Contains(current) && !_originalTextures.ContainsKey(id))
                        {
                            _originalTextures[id] = current;
                        }
                        material.SetTexture("_MainTex", reskin.Texture);
                    }
                }
                else if (reskin == null)
                {
                    // Vanilla stim on a pooled rig we may have touched earlier: if one of our
                    // textures is still on it, put the game's own texture back.
                    foreach (var material in renderer.materials)
                    {
                        if (material == null || !_ownTextures.Contains(material.GetTexture("_MainTex")))
                        {
                            continue;
                        }
                        if (_originalTextures.TryGetValue(material.GetInstanceID(), out var original))
                        {
                            material.SetTexture("_MainTex", original);
                        }
                    }
                }
            }
            return found;
        }
    }
}
