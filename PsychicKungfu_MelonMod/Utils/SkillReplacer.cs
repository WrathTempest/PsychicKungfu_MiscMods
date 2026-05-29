using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using PsychicKungfu_MelonMod.Utils;
using DBLoad;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PsychicKungfu_MelonMod
{
    public static class SkillReplacer
    {
        // ─────────────────────────────────────────────────────────────
        // STATE
        // ─────────────────────────────────────────────────────────────
        public static int _currentPage = 0;
        public static bool _keyPressed = false;

        public static FightWindow _fightWindowInstance;
        public static Role _cachedRole;

        // Keeps track of the master list (Originals + Appended Config Skills)
        public static readonly Dictionary<int, List<ActiveSkill>> _masterSkills =
            new Dictionary<int, List<ActiveSkill>>();

        // ─────────────────────────────────────────────────────────────
        // INIT
        // ─────────────────────────────────────────────────────────────
        public static void Initialize(ConfigFile config)
        {
            // Subscribe to the centralized global hot-swap listener inside ModConfig
            ModConfig.OnConfigHotSwapped += OnConfigHotReload;

            Debug.Log("[SkillReplacer] Initialized and hooked to centralized ModConfig event tracker.");
        }

        private static void OnConfigHotReload()
        {
            _masterSkills.Clear();
            _currentPage = 0;
            Debug.Log($"[SkillReplacer] Config hot-swapped via ModConfig! Cleared master skill cache.");
        }

        // ─────────────────────────────────────────────────────────────
        // UPDATE (Key Detection & UI Force Refresh via Traverse)
        // ─────────────────────────────────────────────────────────────
        public static void Update()
        {
            if (Keyboard.current != null && Keyboard.current.leftCtrlKey.wasPressedThisFrame)
            {
                _keyPressed = true;

                if (_fightWindowInstance != null && _cachedRole != null)
                {
                    // Use Traverse to invoke the PRIVATE method 'SetSkill'
                    Traverse.Create(_fightWindowInstance).Method("SetSkill", _cachedRole).GetValue();
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // INITIALIZATION: APPEND CONFIG SKILLS
        // ─────────────────────────────────────────────────────────────
        // CALL THIS METHOD INSIDE YOUR PLAYER INITIALIZATION PATCH
        public static void AppendConfigSkills(Role role)
        {
            if (role == null || role.m_actives == null) return;

            // 1. Use .Distinct() to immediately remove duplicates inside the config string itself
            List<int> ids = GetConfiguredSkillIds().Distinct().ToList();
            if (ids.Count == 0) return;

            List<ActiveSkill> workingList = role.m_actives.ToList();

            // 2. Build a list of skill IDs the player already has in their hotbar
            HashSet<int> existingIds = new HashSet<int>(workingList.Select(s => s.m_id));

            foreach (int skillId in ids)
            {
                // 3. Skip if the player already has this skill equipped base-game
                if (existingIds.Contains(skillId))
                {
                    Debug.Log($"[SkillReplacer] Skipped duplicate skill {skillId} (Player already has it).");
                    continue;
                }

                try
                {
                    if (Skill.Get(skillId) != null)
                    {
                        workingList.Add(new ActiveSkill(skillId, role));
                        existingIds.Add(skillId); // Track it so it doesn't get added again
                        Debug.Log($"[SkillReplacer] Appended unique skill {skillId} to player active skills.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }

            // Write back to the role
            role.m_actives = workingList.ToList();

            int key = role.GetHashCode();
            _masterSkills.Remove(key);
        }

        // ─────────────────────────────────────────────────────────────
        // ROTATION LOGIC
        // ─────────────────────────────────────────────────────────────
        public static void ApplyRotation(Role role)
        {
            if (role == null || role.m_actives == null) return;
            int key = role.GetHashCode();

            // 1. Capture the master list right after your initialization patch has run
            if (!_masterSkills.ContainsKey(key))
            {
                _masterSkills[key] = role.m_actives.ToList();
                Debug.Log($"[SkillReplacer] Master list cached with {_masterSkills[key].Count} total skills.");
            }

            List<ActiveSkill> masterList = _masterSkills[key];
            int totalSkills = masterList.Count;

            if (totalSkills <= 6) return;

            int totalPages = Mathf.CeilToInt((float)totalSkills / 6f);

            if (_keyPressed)
            {
                _currentPage = (_currentPage + 1) % totalPages;
                _keyPressed = false;
                Debug.Log($"[SkillReplacer] Switched to Page {_currentPage + 1}/{totalPages}");
            }

            int offset = _currentPage * 6;

            // 2. Map data dynamically back into the active hotbar slots
            // If m_actives is an array, we can use a standard loop to mutate its indices
            for (int i = 0; i < role.m_actives.Count; i++)
            {
                int targetMasterIndex = (i + offset) % totalSkills;
                role.m_actives[i] = masterList[targetMasterIndex];
            }
        }

        public static List<int> GetConfiguredSkillIds()
        {
            List<int> ids = new List<int>();
            try
            {
                // Pull string data dynamically from the centralized ModConfig container instead
                string raw = ModConfig.ReplaceSkillsConfig.Value;
                if (string.IsNullOrWhiteSpace(raw)) return ids;

                string[] split = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string s in split)
                {
                    if (int.TryParse(s.Trim(), out int id)) ids.Add(id);
                }
            }
            catch (Exception ex) { Debug.LogError(ex); }
            return ids;
        }
    }
}