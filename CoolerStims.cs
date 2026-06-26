using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SemanticVersioning;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;

using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;
using System.Linq;
using SPTarkov.Server.Core.Services.Mod;

namespace CoolerStims
{
    // SPT requires every mod DLL to contain a class that extends AbstractModMetadata
    public record CoolerStimsMetadata : AbstractModMetadata
    {
        public override string ModGuid       { get; init; } = "com.vultify.coolerstims";
        public override string Name          { get; init; } = "CoolerStims";
        public override string Author        { get; init; } = "Vultify";
        public override string License       { get; init; } = "MIT";
        public override string Url           { get; init; } = "";
        public override bool?  IsBundleMod   { get; init; } = false;

        public override SemanticVersioning.Version Version { get; init; }
            = new SemanticVersioning.Version("1.0.0", false);

        public override SemanticVersioning.Range SptVersion { get; init; }
            = new SemanticVersioning.Range("~4.0.13", false);

        public override List<string>                                  Contributors      { get; init; } = new();
        public override List<string>                                  Incompatibilities { get; init; } = new();
        public override Dictionary<string, SemanticVersioning.Range>  ModDependencies   { get; init; } = new();
    }

    [Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 3)]
    public class CoolerStimsMod : IOnLoad
    {
        // ── Known stim tpls — used to find loose-loot spawnpoints to clone ───
        private static readonly HashSet<string> KnownStimTpls = new()
        {
            "5c0e531d86f7747fa54205c2", // Propital
            "5c0e530286f7747fa1419862", // Morphine
            "5ed515e03a40a50460332579", // Adrenaline
            "5c0e534186f7747fa1419867", // eTG-change
            "637b612fb7afa97bfc3d7005", // SJ12
            "5ed51652f6c34d2cc26336a1", // MULE
            "5fca138c2a7b221b2852a5c6", // Zagustin
            "5c0e533786f7747fa23f4d47", // SJ6
            "5c0e53c886f7747fa54205c3", // SJ9
        };

        // ── Medical container IDs (contain Propital across all maps) ──────────
        private static readonly HashSet<string> MedContainerIds = new()
        {
            "5909d5ef86f77467974efbd8",
            "5d6fe50986f77449d97f7463",
            "67614e3a6a90e4f10b0b140d",
            "578f87a3245977356274f2cb",
            "5909d24f86f77466f56e6855",
            "5d6d2b5486f774785c2ba8ea",
            "5d6d2bb386f774785b07a77a",
        };

        // ── Shared ─────────────────────────────────────────────────────────────
        private const string PARENT_ID      = "5448f3a64bdc2d60728b456a"; // injector parent
        private const string THERAPIST_ID   = "54cb57776803fa99248b456e";
        private const string ROUBLES_ID     = "5449016a4bdc2d6f028b456f";
        private const string HANDBOOK_PARENT = "5b47574386f77428ca22b33a"; // Stimulants category

        // ── APEX Combat Stim ───────────────────────────────────────────────────
        private const string APEX_STIM_ID      = "5c0a1b2c3d4e5f6789abcde1";
        private const string APEX_BUFF_KEY     = "Stimulator_Buffs_APEX";
        private const string APEX_BASE_ID      = "637b612fb7afa97bfc3d7005"; // SJ12 TGLabs
        private const string APEX_ASSORT_ID    = "5c0a1b2c3d4e5f6789abcde2";
        private const double APEX_PRICE        = 56000;
        private const double APEX_FLEA         = 75000;

        // ── IRON Endurance Stim ────────────────────────────────────────────────
        private const string IRON_STIM_ID      = "5c0a1b2c3d4e5f6789abcde5";
        private const string IRON_BUFF_KEY     = "Stimulator_Buffs_IRON";
        private const string IRON_BASE_ID      = "5ed51652f6c34d2cc26336a1"; // M.U.L.E.
        private const double IRON_FLEA         = 150000;

        // ── AEGIS Medical Stim ─────────────────────────────────────────────────
        private const string AEGIS_STIM_ID      = "5c0a1b2c3d4e5f6789abcde3";
        private const string AEGIS_BUFF_KEY     = "Stimulator_Buffs_AEGIS";
        private const string AEGIS_BASE_ID      = "5c0e534186f7747fa1419867"; // eTG-change
        private const string AEGIS_ASSORT_ID    = "5c0a1b2c3d4e5f6789abcde4";
        private const double AEGIS_PRICE        = 105000;
        private const double AEGIS_FLEA         = 125000;

        // ── Injected services ──────────────────────────────────────────────────
        private readonly CustomItemService      _customItemService;
        private readonly DatabaseService        _databaseService;
        private readonly TraderHelper           _traderHelper;
        private readonly ICloner                _cloner;

        public CoolerStimsMod(
            CustomItemService     customItemService,
            DatabaseService       databaseService,
            TraderHelper          traderHelper,
            ICloner               cloner)
        {
            _customItemService     = customItemService;
            _databaseService       = databaseService;
            _traderHelper          = traderHelper;
            _cloner                = cloner;
        }

        public Task OnLoad()
        {
            try
            {
                CreateAPEX();
                AddAPEXBuffs();
                AddAPEXToTherapist();

                CreateAEGIS();
                AddAEGISBuffs();
                AddAEGISToTherapist();

                CreateIRON();
                AddIRONBuffs();

                AddToLootTables(APEX_STIM_ID,  400);
                AddToLootTables(AEGIS_STIM_ID, 300);
                AddToLootTables(IRON_STIM_ID,  350);

                AddToLooseLoot(APEX_STIM_ID,  0.0018);
                AddToLooseLoot(AEGIS_STIM_ID, 0.0012);
                AddToLooseLoot(IRON_STIM_ID,  0.0015);

                Console.WriteLine("[CoolerStims] Loaded — APEX, AEGIS, and IRON stims added.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CoolerStims] ERROR during load: {ex}");
            }
            return Task.CompletedTask;
        }

        // ══════════════════════════════════════════════════════════════════════
        // APEX Combat Stim
        // ══════════════════════════════════════════════════════════════════════

        private void CreateAPEX()
        {
            _customItemService.CreateItemFromClone(new NewItemFromCloneDetails
            {
                ItemTplToClone       = APEX_BASE_ID,
                NewId                = APEX_STIM_ID,
                ParentId             = PARENT_ID,
                HandbookParentId     = HANDBOOK_PARENT,
                HandbookPriceRoubles = APEX_PRICE,
                FleaPriceRoubles     = APEX_FLEA,
                Locales = new Dictionary<string, LocaleDetails>
                {
                    ["en"] = new LocaleDetails
                    {
                        Name      = "APEX Combat Stim",
                        ShortName = "A.P.E.X.",
                        Description =
                            "Adrenal Performance Enhancement eXtract. A military-grade combat stimulant " +
                            "developed for tier-1 special operations units. APEX boosts neuromuscular " +
                            "response, increases stamina capacity, and reduces stress-induced weapon " +
                            "deviation, improving performance under sustained fire.\n\n" +
                            "Active period: approximately 110 seconds.\n" +
                            "Side effects: moderate energy depletion following the active phase.\n\n" +
                            "Classified Schedule II in most jurisdictions. For authorised use only."
                    }
                },
                OverrideProperties = new TemplateItemProperties
                {
                    BackgroundColor = "red",
                    StimulatorBuffs = APEX_BUFF_KEY,
                    MedUseTime      = 3,
                    MaxHpResource   = 0,
                    EffectsDamage   = new Dictionary<DamageEffectType, EffectsDamageProperties>
                    {
                        [DamageEffectType.Pain] = new EffectsDamageProperties { Delay = 0, Duration = 180, FadeOut = 5 }
                    }
                }
            });
        }

        private void AddAPEXBuffs()
        {
            var buffs = _databaseService.GetGlobals().Configuration.Health.Effects.Stimulator.Buffs;
            buffs[APEX_BUFF_KEY] = new List<Buff>
            {
                new Buff { BuffType = "MaxStamina",  Chance = 1, Delay = 0,  Duration = 110, Value = 15,  AbsoluteValue = true,  SkillName = "" },
                new Buff { BuffType = "StaminaRate", Chance = 1, Delay = 0,  Duration = 110, Value = 5,   AbsoluteValue = true,  SkillName = "" },
                new Buff { BuffType = "SkillRate",   Chance = 1, Delay = 0,  Duration = 110, Value = 25,  AbsoluteValue = true,  SkillName = "StressResistance" },
                new Buff { BuffType = "EnergyRate",  Chance = 1, Delay = 0,  Duration = 110, Value = 1.5, AbsoluteValue = true,  SkillName = "" },
                new Buff { BuffType = "EnergyRate",  Chance = 1, Delay = 95, Duration = 60,  Value = -3,  AbsoluteValue = true,  SkillName = "" },
                new Buff { BuffType = "QuantumTunnelling", Chance = 1, Delay = 95, Duration = 60, Value = 0, AbsoluteValue = true, SkillName = "" },
                new Buff { BuffType = "SkillRate",  Chance = 1, Delay = 0,  Duration = 110, Value = 20, AbsoluteValue = true,  SkillName = "Health" },
            };
        }

        private void AddAPEXToTherapist()
        {
            var assort = _traderHelper.GetTraderAssortsByTraderId(THERAPIST_ID);
            assort.Items.Add(new Item
            {
                Id       = APEX_ASSORT_ID,
                Template = APEX_STIM_ID,
                ParentId = "hideout",
                SlotId   = "hideout",
                Upd      = new Upd
                {
                    UnlimitedCount        = false,
                    StackObjectsCount     = 3,
                    BuyRestrictionMax     = 3,
                    BuyRestrictionCurrent = 0,
                }
            });
            assort.BarterScheme[APEX_ASSORT_ID] = new List<List<BarterScheme>>
            {
                new List<BarterScheme> { new BarterScheme { Count = 56000, Template = ROUBLES_ID } }
            };
            assort.LoyalLevelItems[APEX_ASSORT_ID] = 2;
        }

        // ══════════════════════════════════════════════════════════════════════
        // AEGIS Medical Stim
        // ══════════════════════════════════════════════════════════════════════

        private void CreateAEGIS()
        {
            _customItemService.CreateItemFromClone(new NewItemFromCloneDetails
            {
                ItemTplToClone       = AEGIS_BASE_ID,
                NewId                = AEGIS_STIM_ID,
                ParentId             = PARENT_ID,
                HandbookParentId     = HANDBOOK_PARENT,
                HandbookPriceRoubles = AEGIS_PRICE,
                FleaPriceRoubles     = AEGIS_FLEA,
                Locales = new Dictionary<string, LocaleDetails>
                {
                    ["en"] = new LocaleDetails
                    {
                        Name      = "AEGIS Medical Stim",
                        ShortName = "A.E.G.I.S.",
                        Description =
                            "Acute Emergency Generative Injector Stimulant. A last-resort medical compound " +
                            "engineered for critical battlefield trauma. AEGIS rapidly coagulates active bleeds, " +
                            "accelerates tissue regeneration, and suppresses pain response to keep the operator " +
                            "functional when conventional treatment is impossible.\n\n" +
                            "Active period: approximately 60–90 seconds.\n" +
                            "Side effects: severe tunnel vision, accelerated hydration loss, and reduced stamina " +
                            "recovery following the active phase.\n\n" +
                            "Classified for emergency use only. Do not administer unless injury is life-threatening."
                    }
                },
                OverrideProperties = new TemplateItemProperties
                {
                    BackgroundColor = "green",
                    StimulatorBuffs = AEGIS_BUFF_KEY,
                    MedUseTime      = 3,
                    MaxHpResource   = 0,
                    EffectsDamage   = new Dictionary<DamageEffectType, EffectsDamageProperties>
                    {
                        [DamageEffectType.Pain] = new EffectsDamageProperties { Delay = 0, Duration = 60, FadeOut = 5 }
                    }
                }
            });
        }

        private void AddAEGISBuffs()
        {
            var buffs = _databaseService.GetGlobals().Configuration.Health.Effects.Stimulator.Buffs;
            buffs[AEGIS_BUFF_KEY] = new List<Buff>
            {
                // ── Positive effects (immediate) ───────────────────────────────
                new Buff { BuffType = "RemoveAllBloodLosses", Chance = 1, Delay = 0,  Duration = 90,  Value = 0,   AbsoluteValue = true,  SkillName = "" },
                new Buff { BuffType = "HealthRate",           Chance = 1, Delay = 0,  Duration = 60,  Value = 2.5, AbsoluteValue = true,  SkillName = "" },
                // ── Negative aftereffects (70 s delay) ────────────────────────
                new Buff { BuffType = "HydrationRate",        Chance = 1, Delay = 70, Duration = 60,  Value = -2,  AbsoluteValue = true,  SkillName = "" },
                new Buff { BuffType = "StaminaRate",          Chance = 1, Delay = 70, Duration = 60,  Value = -2,  AbsoluteValue = true,  SkillName = "" },
                new Buff { BuffType = "QuantumTunnelling",    Chance = 1, Delay = 70, Duration = 60,  Value = 0,   AbsoluteValue = true,  SkillName = "" },
            };
        }

        private void AddAEGISToTherapist()
        {
            var assort = _traderHelper.GetTraderAssortsByTraderId(THERAPIST_ID);
            assort.Items.Add(new Item
            {
                Id       = AEGIS_ASSORT_ID,
                Template = AEGIS_STIM_ID,
                ParentId = "hideout",
                SlotId   = "hideout",
                Upd      = new Upd
                {
                    UnlimitedCount        = false,
                    StackObjectsCount     = 3,
                    BuyRestrictionMax     = 3,
                    BuyRestrictionCurrent = 0,
                }
            });
            assort.BarterScheme[AEGIS_ASSORT_ID] = new List<List<BarterScheme>>
            {
                new List<BarterScheme> { new BarterScheme { Count = 105000, Template = ROUBLES_ID } }
            };
            assort.LoyalLevelItems[AEGIS_ASSORT_ID] = 4;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Shared loot table helper — adds a stim to medical containers in all maps
        // ══════════════════════════════════════════════════════════════════════

        private void AddToLootTables(string stimId, int relativeProbability)
        {
            var locs = _databaseService.GetLocations();
            var staticLoots = new[]
            {
                locs.Bigmap?.StaticLoot?.Value,
                locs.Woods?.StaticLoot?.Value,
                locs.Shoreline?.StaticLoot?.Value,
                locs.Interchange?.StaticLoot?.Value,
                locs.Lighthouse?.StaticLoot?.Value,
                locs.TarkovStreets?.StaticLoot?.Value,
                locs.Laboratory?.StaticLoot?.Value,
                locs.Factory4Day?.StaticLoot?.Value,
                locs.Factory4Night?.StaticLoot?.Value,
                locs.Sandbox?.StaticLoot?.Value,
            };

            foreach (var staticLoot in staticLoots)
            {
                if (staticLoot == null) continue;
                foreach (var containerId in MedContainerIds)
                {
                    MongoId mongoId = containerId;
                    if (staticLoot.TryGetValue(mongoId, out var container))
                    {
                        var list = container.ItemDistribution?.ToList() ?? new List<ItemDistribution>();
                        list.Add(new ItemDistribution
                        {
                            Tpl                 = stimId,
                            RelativeProbability = relativeProbability
                        });
                        container.ItemDistribution = list;
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // IRON Endurance Stim
        // ══════════════════════════════════════════════════════════════════════

        private void CreateIRON()
        {
            _customItemService.CreateItemFromClone(new NewItemFromCloneDetails
            {
                ItemTplToClone       = IRON_BASE_ID,
                NewId                = IRON_STIM_ID,
                ParentId             = PARENT_ID,
                HandbookParentId     = HANDBOOK_PARENT,
                HandbookPriceRoubles = IRON_FLEA,
                FleaPriceRoubles     = IRON_FLEA,
                Locales = new Dictionary<string, LocaleDetails>
                {
                    ["en"] = new LocaleDetails
                    {
                        Name      = "IRON Endurance Stim",
                        ShortName = "I.R.O.N.",
                        Description =
                            "Integrated Resistance and Output Nanodrug. A high-endurance performance compound " +
                            "developed for special operations personnel operating under extreme load conditions. " +
                            "IRON temporarily overrides the body's natural weight tolerance thresholds and sustains " +
                            "neuromuscular output, allowing the operator to maintain combat mobility under loads " +
                            "that would otherwise be debilitating.\n\n" +
                            "Active period: approximately 120 seconds.\n" +
                            "Side effects: accelerated health and energy depletion following the active phase. " +
                            "Do not administer during active combat — reduced stamina recovery after expiration " +
                            "may prove fatal.\n\n" +
                            "Restricted to authorized military logistics personnel. Misuse under field conditions " +
                            "is undertaken at operator's own risk."
                    }
                },
                OverrideProperties = new TemplateItemProperties
                {
                    BackgroundColor = "blue",
                    StimulatorBuffs = IRON_BUFF_KEY,
                    MedUseTime      = 3,
                    MaxHpResource   = 0,
                }
            });
        }

        private void AddIRONBuffs()
        {
            var buffs = _databaseService.GetGlobals().Configuration.Health.Effects.Stimulator.Buffs;
            buffs[IRON_BUFF_KEY] = new List<Buff>
            {
                // ── Positive effects (immediate) ───────────────────────────────
                new Buff { BuffType = "WeightLimit",  Chance = 1, Delay = 0,   Duration = 120, Value = 0.8,  AbsoluteValue = false, SkillName = "" },
                new Buff { BuffType = "MaxStamina",   Chance = 1, Delay = 0,   Duration = 120, Value = 30,   AbsoluteValue = true,  SkillName = "" },
                new Buff { BuffType = "StaminaRate",  Chance = 1, Delay = 0,   Duration = 120, Value = 10,   AbsoluteValue = true,  SkillName = "" },
                // ── Negative aftereffects (110 s delay) ───────────────────────
                new Buff { BuffType = "HealthRate",   Chance = 1, Delay = 110, Duration = 60,  Value = -1,   AbsoluteValue = true,  SkillName = "" },
                new Buff { BuffType = "EnergyRate",   Chance = 1, Delay = 110, Duration = 60,  Value = -1,   AbsoluteValue = true,  SkillName = "" },
            };
        }

        // ══════════════════════════════════════════════════════════════════════
        // Loose loot — create a dedicated rare spawnpoint on every map that
        // already has stim spawns, by cloning the smallest existing stim
        // spawnpoint and swapping its item template to ours.
        // ══════════════════════════════════════════════════════════════════════

        private void AddToLooseLoot(string stimId, double spawnProbability)
        {
            var locs = _databaseService.GetLocations();
            var looseLootMaps = new[]
            {
                locs.Bigmap?.LooseLoot?.Value,
                locs.Woods?.LooseLoot?.Value,
                locs.Shoreline?.LooseLoot?.Value,
                locs.Interchange?.LooseLoot?.Value,
                locs.Lighthouse?.LooseLoot?.Value,
                locs.TarkovStreets?.LooseLoot?.Value,
                locs.Laboratory?.LooseLoot?.Value,
                locs.Factory4Day?.LooseLoot?.Value,
                locs.Factory4Night?.LooseLoot?.Value,
                locs.Sandbox?.LooseLoot?.Value,
            };

            foreach (var looseLoot in looseLootMaps)
            {
                if (looseLoot?.Spawnpoints == null) continue;

                // Find smallest stim spawnpoint to use as clone source
                Spawnpoint? src = null;
                int minItems = int.MaxValue;
                foreach (var sp in looseLoot.Spawnpoints)
                {
                    var items = sp.Template?.Items;
                    if (items == null || !items.Any()) continue;
                    bool hasStim = items.Any(i => KnownStimTpls.Contains(i.Template.ToString()));
                    int count = items.Count();
                    if (hasStim && count < minItems)
                    {
                        minItems = count;
                        src = sp;
                        if (minItems == 1) break; // single-item is ideal
                    }
                }
                if (src == null) continue;

                // Deep-clone and strip to root item only
                var clone = _cloner.Clone(src);
                var root = clone.Template?.Items?.FirstOrDefault(i => i.Id == clone.Template.Root)
                           ?? clone.Template?.Items?.FirstOrDefault();
                if (root == null || clone.Template == null) continue;

                // Swap template, keep everything else (composedKey, _id, position)
                root.Template = stimId;
                clone.Template.Items = new List<SptLootItem> { root };

                // Keep only the distribution entry for the root item (first entry)
                var dist = clone.ItemDistribution?.FirstOrDefault();
                clone.ItemDistribution = dist != null
                    ? new List<LooseLootItemDistribution> { dist }
                    : new List<LooseLootItemDistribution>();

                clone.Probability = spawnProbability;

                var list = looseLoot.Spawnpoints.ToList();
                list.Add(clone);
                looseLoot.Spawnpoints = list;
            }
        }

    }
}
