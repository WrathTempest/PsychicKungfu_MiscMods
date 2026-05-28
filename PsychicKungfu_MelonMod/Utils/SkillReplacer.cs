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
        // CONFIG
        // ─────────────────────────────────────────────────────────────

        public static ConfigEntry<string> ReplaceSkillsConfig;

        // Example:
        // 500180,500181,500182

        // ─────────────────────────────────────────────────────────────
        // STATE
        // ─────────────────────────────────────────────────────────────

        public static bool _enabled;

        // role instance id -> original active skills
        public static readonly Dictionary<int, List<ActiveSkill>> _originalSkills =
            new Dictionary<int, List<ActiveSkill>>();

        // ─────────────────────────────────────────────────────────────
        // INIT
        // ─────────────────────────────────────────────────────────────

        public static void Initialize(ConfigFile config)
        {
            ReplaceSkillsConfig = config.Bind(
                "Skill Replacer",
                "Replacement Skills",
                "500180",
                "Comma separated replacement skill IDs.\nExample: 500180,500181,500182");

            Debug.Log("[SkillReplacer] Initialized");
        }

        // ─────────────────────────────────────────────────────────────
        // UPDATE
        // ─────────────────────────────────────────────────────────────

        public static void Update()
        {
            if (Keyboard.current != null && Keyboard.current.leftCtrlKey.wasPressedThisFrame)
            {
                _enabled = !_enabled;

                Main.Log.LogInfo(
                    $"[SkillReplacer] " +
                    $"{(_enabled ? "ENABLED" : "DISABLED")}");
            }
        }

        // ─────────────────────────────────────────────────────────────
        // PARSE CONFIG
        // ─────────────────────────────────────────────────────────────

        public static List<int> GetConfiguredSkillIds()
        {
            List<int> ids = new List<int>();

            try
            {
                string raw = ReplaceSkillsConfig.Value;

                if (string.IsNullOrWhiteSpace(raw))
                    return ids;

                string[] split =
                    raw.Split(
                        new[] { ',' },
                        StringSplitOptions.RemoveEmptyEntries);

                foreach (string s in split)
                {
                    if (int.TryParse(s.Trim(), out int id))
                    {
                        ids.Add(id);
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[SkillReplacer] Invalid skill id: {s}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }

            return ids;
        }

        // ─────────────────────────────────────────────────────────────
        // APPLY
        // ─────────────────────────────────────────────────────────────

        public static void ApplyReplacement(Role role)
        {
            if (role == null)
                return;

            if (role.m_actives == null)
                return;

            int key = role.GetHashCode();

            // save original once
            if (!_originalSkills.ContainsKey(key))
            {
                _originalSkills[key] =
                    role.m_actives.ToList();

                Debug.Log(
                    "[SkillReplacer] Saved original skills");
            }

            List<int> ids = GetConfiguredSkillIds();

            if (ids.Count == 0)
            {
                Debug.LogWarning(
                    "[SkillReplacer] No replacement skills configured");
                return;
            }

            int max =
                Mathf.Min(
                    6,
                    Mathf.Min(
                        role.m_actives.Count,
                        ids.Count));

            for (int i = 0; i < max; i++)
            {
                try
                {
                    int skillId = ids[i];
                    if (Skill.Get(skillId) != null)
                    {
                        role.m_actives[i] = new ActiveSkill(skillId, role);
                        Debug.Log($"[SkillReplacer] Slot {i} -> {skillId}");
                    }            
                    
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // RESTORE
        // ─────────────────────────────────────────────────────────────

        public static void RestoreOriginal(Role role)
        {
            if (role == null)
                return;

            int key = role.GetHashCode();

            if (!_originalSkills.TryGetValue(key, out var original))
                return;

            int max =
                Mathf.Min(
                    role.m_actives.Count,
                    original.Count);

            for (int i = 0; i < max; i++)
            {
                role.m_actives[i] = original[i];
            }

            Debug.Log(
                "[SkillReplacer] Restored original skills");
        }
        
    }
}