using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
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
using SPTarkov.Server.Core.Models.Utils;

namespace CoolerStims
{
    // ── Config models ──
    public class BuffEntry
    {
        [JsonPropertyName("value")]    public double Value { get; set; }
        [JsonPropertyName("duration")] public int Duration { get; set; }
        [JsonPropertyName("delay")]    public int Delay { get; set; }
    }

    public class StimConfig
    {
        [JsonPropertyName("traderPrice")]        public double TraderPrice { get; set; }
        [JsonPropertyName("traderLoyaltyLevel")] public int TraderLoyaltyLevel { get; set; } = 4;
        [JsonPropertyName("buffs")]              public Dictionary<string, BuffEntry> Buffs { get; set; } = new();
    }

    public class ModConfig
    {
        [JsonPropertyName("apex")]  public StimConfig Apex { get; set; } = new();
        [JsonPropertyName("aegis")] public StimConfig Aegis { get; set; } = new();
        [JsonPropertyName("iron")]  public StimConfig Iron { get; set; } = new();
        [JsonPropertyName("argus")] public StimConfig Argus { get; set; } = new();
        [JsonPropertyName("graft")] public StimConfig Graft { get; set; } = new();
    }

    public record CoolerStimsMetadata : AbstractModMetadata
    {
        public override string ModGuid       { get; init; } = "com.vultify.coolerstims";
        public override string Name          { get; init; } = "CoolerStims";
        public override string Author        { get; init; } = "Vultify";
        public override string License       { get; init; } = "MIT";
        public override string Url           { get; init; } = "";
        public override bool?  IsBundleMod   { get; init; } = false;

        public override SemanticVersioning.Version Version { get; init; }
            = new SemanticVersioning.Version("1.3.0", false);

        public override SemanticVersioning.Range SptVersion { get; init; }
            = new SemanticVersioning.Range("~4.0.13", false);

        public override List<string>                                  Contributors      { get; init; } = new();
        public override List<string>                                  Incompatibilities { get; init; } = new();
        public override Dictionary<string, SemanticVersioning.Range>  ModDependencies   { get; init; } = new();
    }

    [Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 3)]
    public class CoolerStimsMod : IOnLoad
    {
        private static readonly HashSet<string> KnownStimTpls = new()
        {
            "5c0e531d86f7747fa54205c2", "5c0e530286f7747fa1419862",
            "5ed515e03a40a50460332579", "5c0e534186f7747fa1419867",
            "637b612fb7afa97bfc3d7005", "5ed51652f6c34d2cc26336a1",
            "5fca138c2a7b221b2852a5c6", "5c0e533786f7747fa23f4d47",
            "5c0e53c886f7747fa54205c3",
            "5ed515c8d380ab312177c0fa", "5ed5160a87bb8443d10680b5",
        };

        private static readonly HashSet<string> MedContainerIds = new()
        {
            "5909d4c186f7746ad34e805a", "5909d24f86f77466f56e6855",
            "61aa1ead84ea0800645777fd", "5d6fe50986f77449d97f7463",
        };

        private const string PARENT_ID       = "5448f3a64bdc2d60728b456a";
        private const string THERAPIST_ID    = "54cb57776803fa99248b456e";
        private const string ROUBLES_ID      = "5449016a4bdc2d6f028b456f";
        private const string HANDBOOK_PARENT = "5b47574386f77428ca22b33a";

        private const string APEX_STIM_ID    = "5c0a1b2c3d4e5f6789abcde1";
        private const string APEX_BUFF_KEY   = "Stimulator_Buffs_APEX";
        private const string APEX_BASE_ID    = "637b612fb7afa97bfc3d7005";
        private const string APEX_ASSORT_ID  = "5c0a1b2c3d4e5f6789abcde2";

        private const string AEGIS_STIM_ID   = "5c0a1b2c3d4e5f6789abcde3";
        private const string AEGIS_BUFF_KEY  = "Stimulator_Buffs_AEGIS";
        private const string AEGIS_BASE_ID   = "5c0e534186f7747fa1419867";
        private const string AEGIS_ASSORT_ID = "5c0a1b2c3d4e5f6789abcde4";

        private const string IRON_STIM_ID    = "5c0a1b2c3d4e5f6789abcde5";
        private const string IRON_BUFF_KEY   = "Stimulator_Buffs_IRON";
        private const string IRON_BASE_ID    = "5ed51652f6c34d2cc26336a1";

        private const string ARGUS_STIM_ID   = "5c0a1b2c3d4e5f6789abcde6";
        private const string ARGUS_BUFF_KEY  = "Stimulator_Buffs_ARGUS";
        private const string ARGUS_BASE_ID   = "5ed515c8d380ab312177c0fa";
        private const string ARGUS_ASSORT_ID = "5c0a1b2c3d4e5f6789abcde7";

        private const string GRAFT_STIM_ID   = "5c0a1b2c3d4e5f6789abcde8";
        private const string GRAFT_BUFF_KEY  = "Stimulator_Buffs_GRAFT";
        // Cloned from Morphine (Drugs baseclass), not a real Stimulant — confirmed via
        // Salco's Armory's B.O.N.E. stim (same trick) that DrugsItemClass items skip the
        // StimulatorBuffs-forces-Head override that Stimulator-class items hit in
        // GClass3009.method_7, so Drugs-class items can combine StimulatorBuffs with
        // effects_damage body-part healing. Prefab/UsePrefab below override the visuals
        // back to Meldonin's syringe so it still looks/plays like an injector, not morphine.
        private const string GRAFT_BASE_ID   = "544fb3f34bdc2d03748b456a"; // Morphine
        private const string DRUGS_PARENT_ID = "5448f3a14bdc2d27728b4569";
        private const string GRAFT_LOOT_PREFAB = "assets/content/weapons/usable_items/item_syringe/item_stimulator_mildronate_loot.bundle";
        private const string GRAFT_USE_PREFAB  = "assets/content/weapons/usable_items/item_syringe/item_stimulator_mildronate_container.bundle";

        private readonly CustomItemService  _customItemService;
        private readonly DatabaseService    _databaseService;
        private readonly TraderHelper       _traderHelper;
        private readonly ICloner            _cloner;
        private readonly ISptLogger<CoolerStimsMod> _logger;

        private ModConfig _config = new();

        public CoolerStimsMod(
            CustomItemService  customItemService,
            DatabaseService    databaseService,
            TraderHelper       traderHelper,
            ICloner            cloner,
            ISptLogger<CoolerStimsMod> logger)
        {
            _customItemService = customItemService;
            _databaseService   = databaseService;
            _traderHelper      = traderHelper;
            _cloner            = cloner;
            _logger            = logger;
        }

        public Task OnLoad()
        {
            try
            {
                LoadConfig();

                CreateAPEX();
                AddAPEXBuffs();
                AddAPEXToTherapist();

                CreateAEGIS();
                AddAEGISBuffs();
                AddAEGISToTherapist();

                CreateIRON();
                AddIRONBuffs();

                CreateARGUS();
                AddARGUSBuffs();
                AddARGUSToTherapist();

                CreateGRAFT();
                AddGRAFTBuffs();

                AddToLootTables(APEX_STIM_ID,  2000);
                AddToLootTables(AEGIS_STIM_ID, 1500);
                AddToLootTables(IRON_STIM_ID,  1700);
                AddToLootTables(ARGUS_STIM_ID, 1600);
                AddToLootTables(GRAFT_STIM_ID, 900);

                AddToLooseLoot(APEX_STIM_ID,  2);
                AddToLooseLoot(AEGIS_STIM_ID, 1);
                AddToLooseLoot(IRON_STIM_ID,  2);
                AddToLooseLoot(ARGUS_STIM_ID, 2);
                AddToLooseLoot(GRAFT_STIM_ID, 1);

                _logger.Success("[CoolerStims] Loaded — APEX, AEGIS, IRON, ARGUS, and GRAFT stims added.");
            }
            catch (Exception ex)
            {
                _logger.Error($"[CoolerStims] ERROR during load: {ex}");
            }
            return Task.CompletedTask;
        }

        private void LoadConfig()
        {
            var modDir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var configPath = System.IO.Path.Combine(modDir, "config.json");

            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                _config = JsonSerializer.Deserialize<ModConfig>(json) ?? new ModConfig();
                Console.WriteLine("[CoolerStims] Loaded config.json");
            }
            else
            {
                Console.WriteLine("[CoolerStims] No config.json found, using defaults.");
            }
        }

        private BuffEntry GetBuff(StimConfig stim, string key, double defaultValue = 0, int defaultDuration = 60, int defaultDelay = 0)
        {
            if (stim.Buffs.TryGetValue(key, out var entry))
                return entry;
            return new BuffEntry { Value = defaultValue, Duration = defaultDuration, Delay = defaultDelay };
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
                HandbookPriceRoubles = 42000,
                FleaPriceRoubles     = 42000,
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
            var c = _config.Apex;
            var maxStam   = GetBuff(c, "maxStamina",       15,   110, 0);
            var stamRate  = GetBuff(c, "staminaRate",       5,    110, 0);
            var stress    = GetBuff(c, "stressResistance",  25,   110, 0);
            var energy    = GetBuff(c, "energyRate",        1.5,  110, 0);
            var eCrash    = GetBuff(c, "energyCrash",       -3,   60,  95);
            var tunnel    = GetBuff(c, "tunnelVision",      0,    60,  95);
            var health    = GetBuff(c, "healthSkill",       20,   110, 0);

            var buffs = _databaseService.GetGlobals().Configuration.Health.Effects.Stimulator.Buffs;
            buffs[APEX_BUFF_KEY] = new List<Buff>
            {
                new Buff { BuffType = "MaxStamina",        Chance = 1, Delay = maxStam.Delay,  Duration = maxStam.Duration,  Value = maxStam.Value,  AbsoluteValue = true,  SkillName = "" },
                new Buff { BuffType = "StaminaRate",        Chance = 1, Delay = stamRate.Delay, Duration = stamRate.Duration, Value = stamRate.Value, AbsoluteValue = true,  SkillName = "" },
                new Buff { BuffType = "SkillRate",          Chance = 1, Delay = stress.Delay,   Duration = stress.Duration,   Value = stress.Value,   AbsoluteValue = true,  SkillName = "StressResistance" },
                new Buff { BuffType = "EnergyRate",         Chance = 1, Delay = energy.Delay,   Duration = energy.Duration,   Value = energy.Value,   AbsoluteValue = true,  SkillName = "" },
                new Buff { BuffType = "EnergyRate",         Chance = 1, Delay = eCrash.Delay,   Duration = eCrash.Duration,   Value = eCrash.Value,   AbsoluteValue = true,  SkillName = "" },
                new Buff { BuffType = "QuantumTunnelling",  Chance = 1, Delay = tunnel.Delay,   Duration = tunnel.Duration,   Value = 0,              AbsoluteValue = true,  SkillName = "" },
                new Buff { BuffType = "SkillRate",          Chance = 1, Delay = health.Delay,   Duration = health.Duration,   Value = health.Value,   AbsoluteValue = true,  SkillName = "Health" },
            };
        }

        private void AddAPEXToTherapist()
        {
            var price = _config.Apex.TraderPrice > 0 ? _config.Apex.TraderPrice : 84379;
            var ll    = _config.Apex.TraderLoyaltyLevel > 0 ? _config.Apex.TraderLoyaltyLevel : 4;

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
                new List<BarterScheme> { new BarterScheme { Count = price, Template = ROUBLES_ID } }
            };
            assort.LoyalLevelItems[APEX_ASSORT_ID] = ll;
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
                HandbookPriceRoubles = 50000,
                FleaPriceRoubles     = 50000,
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
            var c = _config.Aegis;
            var bleed   = GetBuff(c, "removeBloodLoss", 0,    90,  0);
            var heal    = GetBuff(c, "healthRate",       9.5,  60,  0);
            var hydra   = GetBuff(c, "hydrationCrash",   -2,   60,  70);
            var stam    = GetBuff(c, "staminaCrash",     -2,   60,  70);
            var tunnel  = GetBuff(c, "tunnelVision",     0,    60,  70);

            var buffs = _databaseService.GetGlobals().Configuration.Health.Effects.Stimulator.Buffs;
            buffs[AEGIS_BUFF_KEY] = new List<Buff>
            {
                new Buff { BuffType = "RemoveAllBloodLosses", Chance = 1, Delay = bleed.Delay,  Duration = bleed.Duration,  Value = 0,            AbsoluteValue = true,  SkillName = "" },
                new Buff { BuffType = "HealthRate",           Chance = 1, Delay = heal.Delay,   Duration = heal.Duration,   Value = heal.Value,   AbsoluteValue = true,  SkillName = "" },
                new Buff { BuffType = "HydrationRate",        Chance = 1, Delay = hydra.Delay,  Duration = hydra.Duration,  Value = hydra.Value,  AbsoluteValue = true,  SkillName = "" },
                new Buff { BuffType = "StaminaRate",          Chance = 1, Delay = stam.Delay,   Duration = stam.Duration,   Value = stam.Value,   AbsoluteValue = true,  SkillName = "" },
                new Buff { BuffType = "QuantumTunnelling",    Chance = 1, Delay = tunnel.Delay, Duration = tunnel.Duration, Value = 0,            AbsoluteValue = true,  SkillName = "" },
            };
        }

        private void AddAEGISToTherapist()
        {
            var price = _config.Aegis.TraderPrice > 0 ? _config.Aegis.TraderPrice : 50000;
            var ll    = _config.Aegis.TraderLoyaltyLevel > 0 ? _config.Aegis.TraderLoyaltyLevel : 4;

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
                new List<BarterScheme> { new BarterScheme { Count = price, Template = ROUBLES_ID } }
            };
            assort.LoyalLevelItems[AEGIS_ASSORT_ID] = ll;
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
                HandbookPriceRoubles = 55000,
                FleaPriceRoubles     = 179975,
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
            var c = _config.Iron;
            var weight  = GetBuff(c, "weightLimit",  0.8,  120, 0);
            var maxStam = GetBuff(c, "maxStamina",   30,   120, 0);
            var stamR   = GetBuff(c, "staminaRate",  10,   120, 0);
            var hCrash  = GetBuff(c, "healthCrash",  -1,   60,  110);
            var eCrash  = GetBuff(c, "energyCrash",  -1,   60,  110);

            var buffs = _databaseService.GetGlobals().Configuration.Health.Effects.Stimulator.Buffs;
            buffs[IRON_BUFF_KEY] = new List<Buff>
            {
                new Buff { BuffType = "WeightLimit",  Chance = 1, Delay = weight.Delay,  Duration = weight.Duration,  Value = weight.Value,  AbsoluteValue = false, SkillName = "" },
                new Buff { BuffType = "MaxStamina",   Chance = 1, Delay = maxStam.Delay, Duration = maxStam.Duration, Value = maxStam.Value, AbsoluteValue = true,  SkillName = "" },
                new Buff { BuffType = "StaminaRate",  Chance = 1, Delay = stamR.Delay,   Duration = stamR.Duration,   Value = stamR.Value,   AbsoluteValue = true,  SkillName = "" },
                new Buff { BuffType = "HealthRate",   Chance = 1, Delay = hCrash.Delay,  Duration = hCrash.Duration,  Value = hCrash.Value,  AbsoluteValue = true,  SkillName = "" },
                new Buff { BuffType = "EnergyRate",   Chance = 1, Delay = eCrash.Delay,  Duration = eCrash.Duration,  Value = eCrash.Value,  AbsoluteValue = true,  SkillName = "" },
            };
        }

        // ══════════════════════════════════════════════════════════════════════
        // ARGUS Perception Stim
        // ══════════════════════════════════════════════════════════════════════

        private void CreateARGUS()
        {
            _customItemService.CreateItemFromClone(new NewItemFromCloneDetails
            {
                ItemTplToClone       = ARGUS_BASE_ID,
                NewId                = ARGUS_STIM_ID,
                ParentId             = PARENT_ID,
                HandbookParentId     = HANDBOOK_PARENT,
                HandbookPriceRoubles = 42000,
                FleaPriceRoubles     = 42000,
                Locales = new Dictionary<string, LocaleDetails>
                {
                    ["en"] = new LocaleDetails
                    {
                        Name      = "ARGUS Perception Stim",
                        ShortName = "A.R.G.U.S.",
                        Description =
                            "Acute Response Gain Uptake Serum. A field-issue cognitive stimulant engineered to " +
                            "sharpen attention and sensory acuity during high-tempo salvage operations. ARGUS " +
                            "floods the system with a synthetic alertness compound, dramatically increasing " +
                            "focus, energy, and the speed at which the operator can examine, sort, and identify " +
                            "recovered materiel.\n\n" +
                            "Active period: approximately 210 seconds.\n" +
                            "Side effects: mild visual distortion and accelerated fluid loss as the compound " +
                            "wears off.\n\n" +
                            "Classified Schedule III. Issued to reconnaissance and salvage units only."
                    }
                },
                OverrideProperties = new TemplateItemProperties
                {
                    BackgroundColor = "yellow",
                    StimulatorBuffs = ARGUS_BUFF_KEY,
                    MedUseTime      = 3,
                    MaxHpResource   = 0,
                }
            });
        }

        private void AddARGUSBuffs()
        {
            var c = _config.Argus;
            var attention  = GetBuff(c, "attention",      20,   210, 0);
            var perception = GetBuff(c, "perception",     20,   210, 0);
            var search     = GetBuff(c, "search",         20,   210, 0);
            var energy     = GetBuff(c, "energyRate",     1.5,  210, 0);
            var tunnel     = GetBuff(c, "tunnelVision",   0,    60,  210);
            var hydra      = GetBuff(c, "hydrationCrash", -1,   60,  210);

            var buffs = _databaseService.GetGlobals().Configuration.Health.Effects.Stimulator.Buffs;
            buffs[ARGUS_BUFF_KEY] = new List<Buff>
            {
                new Buff { BuffType = "SkillRate",         Chance = 1, Delay = attention.Delay,  Duration = attention.Duration,  Value = attention.Value,  AbsoluteValue = true, SkillName = "Attention" },
                new Buff { BuffType = "SkillRate",         Chance = 1, Delay = perception.Delay, Duration = perception.Duration, Value = perception.Value, AbsoluteValue = true, SkillName = "Perception" },
                new Buff { BuffType = "SkillRate",         Chance = 1, Delay = search.Delay,     Duration = search.Duration,     Value = search.Value,     AbsoluteValue = true, SkillName = "Search" },
                new Buff { BuffType = "EnergyRate",        Chance = 1, Delay = energy.Delay,     Duration = energy.Duration,     Value = energy.Value,     AbsoluteValue = true, SkillName = "" },
                new Buff { BuffType = "QuantumTunnelling", Chance = 1, Delay = tunnel.Delay,     Duration = tunnel.Duration,     Value = 0,                AbsoluteValue = true, SkillName = "" },
                new Buff { BuffType = "HydrationRate",     Chance = 1, Delay = hydra.Delay,      Duration = hydra.Duration,      Value = hydra.Value,      AbsoluteValue = true, SkillName = "" },
            };
        }

        private void AddARGUSToTherapist()
        {
            var price = _config.Argus.TraderPrice > 0 ? _config.Argus.TraderPrice : 84379;
            var ll    = _config.Argus.TraderLoyaltyLevel > 0 ? _config.Argus.TraderLoyaltyLevel : 4;

            var assort = _traderHelper.GetTraderAssortsByTraderId(THERAPIST_ID);
            assort.Items.Add(new Item
            {
                Id       = ARGUS_ASSORT_ID,
                Template = ARGUS_STIM_ID,
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
            assort.BarterScheme[ARGUS_ASSORT_ID] = new List<List<BarterScheme>>
            {
                new List<BarterScheme> { new BarterScheme { Count = price, Template = ROUBLES_ID } }
            };
            assort.LoyalLevelItems[ARGUS_ASSORT_ID] = ll;
        }

        // ══════════════════════════════════════════════════════════════════════
        // GRAFT Trauma Injector
        // ══════════════════════════════════════════════════════════════════════

        private void CreateGRAFT()
        {
            _customItemService.CreateItemFromClone(new NewItemFromCloneDetails
            {
                ItemTplToClone       = GRAFT_BASE_ID,
                NewId                = GRAFT_STIM_ID,
                ParentId             = DRUGS_PARENT_ID,
                HandbookParentId     = HANDBOOK_PARENT,
                HandbookPriceRoubles = 22000,
                FleaPriceRoubles     = 68000,
                Locales = new Dictionary<string, LocaleDetails>
                {
                    ["en"] = new LocaleDetails
                    {
                        Name      = "GRAFT Trauma Injector",
                        ShortName = "G.R.A.F.T.",
                        Description =
                            "Generative Rapid Augmentation Field Treatment. An experimental bio-nanite " +
                            "injector that grows synthetic scaffolding around damaged bone and tissue, " +
                            "letting a shattered limb bear weight again within seconds instead of hours. GRAFT " +
                            "sets fractures on contact and kick-starts localized regeneration in a destroyed " +
                            "limb.\n\n" +
                            "Multi-dose auto-injector — treats one destroyed limb per charge.\n" +
                            "Active period: approximately 30 seconds.\n" +
                            "Side effects: residual hand tremors and a delayed energy crash as the nanite " +
                            "payload burns through the body's reserves.\n\n" +
                            "Unregulated biotech — never cleared for trader distribution. Flea market " +
                            "circulation only."
                    }
                },
                OverrideProperties = new TemplateItemProperties
                {
                    BackgroundColor  = "violet",
                    // MedUseTime must match the actual baked length of the use animation in
                    // whatever bundle Prefab/UsePrefab point to (Meldonin's syringe, both 2s
                    // vanilla) — not an arbitrary flavor number. Mismatching this (previously 4)
                    // against the real 2s clip is the likely cause of the weapon not returning
                    // to hands after use.
                    MedUseTime       = 2,
                    MaxHpResource    = 5,
                    StimulatorBuffs  = GRAFT_BUFF_KEY,
                    // Deliberately NOT overriding BodyPartPriority — left empty like Morphine's
                    // own default (Salco's BONE stim does the same). A non-empty custom priority
                    // list here caused the engine's auto-continue-on-double-click behavior (meant
                    // for cycling through multiple destroyed limbs) to keep reapplying to the same
                    // already-healed limb every second until charges ran out, since this item is
                    // Drugs-class and can't signal "nothing left to treat" the way Medkit-class
                    // items do. Leaving it empty (confirmed via BONE, which doesn't drain like
                    // this and returns hands immediately after the animation) avoids that loop.
                    EffectsDamage    = new Dictionary<DamageEffectType, EffectsDamageProperties>
                    {
                        [DamageEffectType.Fracture]      = new EffectsDamageProperties { Delay = 0, Duration = 0, FadeOut = 0 },
                        [DamageEffectType.DestroyedPart] = new EffectsDamageProperties { Delay = 0, Duration = 0, FadeOut = 0, HealthPenaltyMin = 25, HealthPenaltyMax = 45 },
                    },
                    // Visuals only — keeps GRAFT looking/playing like the injector it's
                    // supposed to be instead of Morphine's syringe, even though it's
                    // structurally cloned from Morphine for the Drugs-class mechanics above.
                    Prefab    = new Prefab { Path = GRAFT_LOOT_PREFAB, Rcid = "" },
                    UsePrefab = new Prefab { Path = GRAFT_USE_PREFAB,  Rcid = "" },
                }
            });
        }

        private void AddGRAFTBuffs()
        {
            var c = _config.Graft;
            var heal   = GetBuff(c, "healthRate",  1,    30, 0);
            var energy = GetBuff(c, "energyCrash", -0.5, 60, 30);
            var tremor = GetBuff(c, "handsTremor",  2,    45, 30);

            var buffs = _databaseService.GetGlobals().Configuration.Health.Effects.Stimulator.Buffs;
            buffs[GRAFT_BUFF_KEY] = new List<Buff>
            {
                new Buff { BuffType = "HealthRate",  Chance = 1, Delay = heal.Delay,   Duration = heal.Duration,   Value = heal.Value,   AbsoluteValue = true, SkillName = "" },
                new Buff { BuffType = "EnergyRate",  Chance = 1, Delay = energy.Delay, Duration = energy.Duration, Value = energy.Value, AbsoluteValue = true, SkillName = "" },
                new Buff { BuffType = "HandsTremor", Chance = 1, Delay = tremor.Delay, Duration = tremor.Duration, Value = tremor.Value, AbsoluteValue = true, SkillName = "" },
            };
        }

        // ══════════════════════════════════════════════════════════════════════
        // Loot tables
        // ══════════════════════════════════════════════════════════════════════

        private void AddToLootTables(string stimId, int relativeProbability)
        {
            var locations = _databaseService.GetTables().Locations.GetDictionary();

            foreach ((string locationId, Location location) in locations)
            {
                if (location.StaticLoot == null) continue;

                location.StaticLoot.AddTransformer(staticLoot =>
                {
                    if (staticLoot == null) return staticLoot;

                    foreach (var containerId in MedContainerIds)
                    {
                        MongoId mongoId = containerId;
                        if (staticLoot.TryGetValue(mongoId, out var container))
                        {
                            var containerItemDistribution = container.ItemDistribution?.ToList() ?? new List<ItemDistribution>();
                            containerItemDistribution.Add(new ItemDistribution
                            {
                                Tpl                 = stimId,
                                RelativeProbability = relativeProbability
                            });
                            staticLoot[mongoId] = container with { ItemDistribution = containerItemDistribution };
                        }
                    }

                    return staticLoot;
                });
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Loose loot
        // ══════════════════════════════════════════════════════════════════════

        private void AddToLooseLoot(string stimId, int relativeProbability)
        {
            var locations = _databaseService.GetTables().Locations.GetDictionary();
            var composedKey = stimId.GetHashCode().ToString();

            foreach ((string locationId, Location location) in locations)
            {
                if (location.LooseLoot == null) continue;

                location.LooseLoot.AddTransformer(looseLoot =>
                {
                    if (looseLoot?.Spawnpoints == null) return looseLoot;

                    var spawnpoints = looseLoot.Spawnpoints.ToList();

                    foreach (var sp in spawnpoints)
                    {
                        var items = sp.Template?.Items;
                        if (items == null || !items.Any()) continue;

                        bool hasStim = items.Any(i => KnownStimTpls.Contains(i.Template.ToString()));
                        if (!hasStim) continue;

                        var itemsList = items.ToList();
                        itemsList.Add(new SptLootItem
                        {
                            ComposedKey = composedKey,
                            Id = new MongoId(),
                            Template = new MongoId(stimId),
                            Upd = new Upd { StackObjectsCount = 1 }
                        });
                        sp.Template.Items = itemsList;

                        var distList = sp.ItemDistribution?.ToList() ?? new List<LooseLootItemDistribution>();
                        distList.Add(new LooseLootItemDistribution
                        {
                            ComposedKey = new ComposedKey { Key = composedKey },
                            RelativeProbability = relativeProbability
                        });
                        sp.ItemDistribution = distList;
                    }

                    return looseLoot with { Spawnpoints = spawnpoints };
                });
            }
        }
    }
}
