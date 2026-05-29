using BepInEx.Configuration;
using System;
using UnityEngine;

namespace PsychicKungfu_MelonMod
{
    public static class ModConfig
    {
        // ─── Config Entries ──────────────────────────────────────────────────
        // BepInEx automatically handles typing based on the generic parameter ConfigEntry<T>
        public static ConfigEntry<string> ReplaceSkillsConfig { get; private set; }
        public static ConfigEntry<bool> SpawnCraftingMats { get; private set; }
        public static ConfigEntry<bool> LiftWeaponSkillRestriction { get; private set; }
        public static ConfigEntry<bool> DifficultyPatch { get; private set; }
        public static ConfigEntry<bool> SpawnManuals { get; private set; }
        public static ConfigEntry<bool> MaxSkillExp { get; private set; }
        public static ConfigEntry<int> DumpLanguage { get; private set; }
        public static ConfigEntry<string> CustomName { get; private set; }

        // ─── Hot-Reload Event Dispatcher ─────────────────────────────────────
        // Other classes can subscribe to this event to safely clear their caches 
        // when any setting is modified or hot-swapped in real-time.
        public static event Action OnConfigHotSwapped;

        /// <summary>
        /// Binds all configuration variables and registers the global hot-swap listener.
        /// Call this exactly once during your plugin's Awake/Startup phase.
        /// </summary>
        public static void Initialize(ConfigFile config)
        {
            DumpLanguage = config.Bind(
                "General Settings",
                "DumpLanguage",
                2,
                $"Set the language of the data dump upon game start. 0 = tc, 1 = sc, 2 = en, -1 = disabled" +
                $"This plugin supports overriding data entries by putting the relevant .json files inside DBOverride by default.");

            CustomName = config.Bind(
                "General Settings",
                "CustomName",
                "Hero",
                "Input your custom name here.");

            SpawnCraftingMats = config.Bind(
                "General Settings",
                "SpawnCraftingMats",
                true,
                "When enabled, will spawn all insufficient crafting materials when viewing its recipe in the crafting window (forge and alchemy).");

            DifficultyPatch = config.Bind(
                "General Settings",
                "DifficultyPatch",
                true,
                "Makes difficulty multipliers apply to everyone except the player, and makes the multipliers also apply to ATK and not just Armor Piercing.");

            SpawnManuals = config.Bind(
                "General Settings",
                "SpawnManuals",
                true,
                "Spawns all manuals at game load if you have not yet opened the new game plus achievement package.");

            MaxSkillExp = config.Bind(
                "General Settings",
                "MaxSkillExp",
                false,
                "Maxes out the skill upon first learning it.");

            LiftWeaponSkillRestriction = config.Bind(
                "Skills",
                "LiftWeaponSkillRestriction",
                true,
                "When enabled, will change all weapon categories to ALL (to allow equipment regardless of weapon) and remove most skill learning requirements.");

            ReplaceSkillsConfig = config.Bind(
                "Skills",
                "Replacement Skills",
                "500180",
                "Comma separated replacement skill IDs. IDs are dumped on game start, look for Skill.json.\nExample: 500180,500181,500182");

            // ─── Global Setting Changed Tracker ──────────────────────────────
            // Instead of registering individual events per item, hooking the entire
            // ConfigFile event triggers a cleanup routine whenever ANY option alters.
            config.SettingChanged += GlobalConfigChangedHandler;
            Debug.Log("[ModConfig] Configurations successfully bound and mapped.");
        }

        private static void GlobalConfigChangedHandler(object sender, SettingChangedEventArgs e)
        {
            //Debug.Log($"[ModConfig] Dynamic Hot-Swap Detected! Modified setting: [{e.ChangedSetting.Definition.Section}] -> {e.ChangedSetting.Definition.Key}");

            // Notify all external systems/subscribers to wipe current state structures
            OnConfigHotSwapped?.Invoke();
        }
    }
}