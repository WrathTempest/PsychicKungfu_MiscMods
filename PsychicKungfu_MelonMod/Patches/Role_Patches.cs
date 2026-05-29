using DBLoad;
using Fight;
using HarmonyLib;
using PsychicKungfu_MelonMod.Utils;
using Spine;
using Spine.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PsychicKungfu_MelonMod.Patches
{
    [HarmonyPatch]
    internal class Role_Patches
    {
        [HarmonyPatch(typeof(LeaderRole), MethodType.Constructor, new System.Type[]
    {
        typeof(int),
        typeof(CampType),
        typeof(GameObject),
        typeof(Cell),
        typeof(Orientation),
        typeof(bool)
    })]
        [HarmonyPostfix]
        public static void PlayerInit(LeaderRole __instance)
        {
            if (!Helpers.PlayerHasGlobalBuff(6942)) return;
            Helpers.SetPrivateField<int>(__instance, "m_move", __instance.MaxMove + 4);
            __instance.m_passives.Sort((a, b) =>
            {
                int qualityA = a?.Data.m_quality ?? 0;
                int qualityB = b?.Data.m_quality ?? 0;
                return qualityB.CompareTo(qualityA);
            });
            Helpers.SetPrivateField<string>(__instance.m_data, "m_chengHao", "Realm of the Gods");
            Helpers.SetPrivateField<int[]>(__instance.m_data, "m_passives", new int[] {10300});
            SkeletonAnimation animation = Helpers.GetPrivateField<SkeletonAnimation>(__instance.m_controller, "m_animation");
            AnimationMerger.LogSkinAttachments(animation);
            AnimationMerger.EnhanceQiAura(animation, 0, 0, 0, 1.2f);
            SkillReplacer.AppendConfigSkills(__instance);

        }

        

        [HarmonyPatch(typeof(Role), MethodType.Constructor, new Type[] {typeof(int), typeof(CampType), typeof(GameObject), typeof(Cell), typeof(Orientation), typeof(bool) })]
        [HarmonyPostfix]
        public static void DifficultyMultiplier(Role __instance, bool ai)
        {
            if (!ModConfig.DifficultyPatch.Value) return;
            if (Helpers.IsPlayer(__instance)) return;
            SaveData saveData = MonoSingleton<SaveManager>.Instance.SaveData;
            
            DifficultData difficultData = Difficult.Get((int)saveData.m_diffucultEnum);
            int damagemult = ((difficultData.m_atk - 100) / 1) + 100;
            if (ai)
            {
                Helpers.SetPrivateField<int>(__instance, "m_damage", Mathf.RoundToInt((float)__instance.m_data.m_damage *(float)(damagemult) / 100f));
            }
            else
            {            
                int maxHp = Mathf.RoundToInt((float)__instance.m_data.m_hp * (float)difficultData.m_hp / 100f);
                int maxMp = Mathf.RoundToInt((float)__instance.m_data.m_mp * (float)difficultData.m_hp / 100f);
                Helpers.SetPrivateField<int>(__instance, "m_maxHp", maxHp);
                Helpers.SetPrivateField<int>(__instance, "m_maxMp", maxMp);
                Helpers.SetPrivateField<int>(__instance, "m_atk", Mathf.RoundToInt((float)__instance.m_data.m_atk * (float)difficultData.m_atk / 100f));
                Helpers.SetPrivateField<int>(__instance, "m_damage", Mathf.RoundToInt((float)__instance.m_data.m_damage * (float)damagemult / 100f));
                Helpers.SetPrivateField<int>(__instance, "m_def", Mathf.RoundToInt((float)__instance.m_data.m_def * (float)difficultData.m_def / 100f));
                Helpers.SetPrivateField<int>(__instance, "m_speed", Mathf.RoundToInt((float)__instance.m_data.m_speed * (float)difficultData.m_speed / 100f));
                Helpers.SetPrivateField<int>(__instance, "m_curHp", maxHp);
                Helpers.SetPrivateField<int>(__instance, "m_curMp", maxMp);

            }
            
        }

        [HarmonyPatch(typeof(FightWindow), "SetSkill")]
        [HarmonyPrefix]
        public static void PassiveSort(FightWindow __instance, Role role)
        {
            if (role == null || !Helpers.IsPlayer(role)) return;

            // Cache UI window and role references so Update() can trigger redrawing
            SkillReplacer._fightWindowInstance = __instance;
            SkillReplacer._cachedRole = role;

            // Rearrange the data entries right before the UI reads it
            SkillReplacer.ApplyRotation(role);
        }

        [HarmonyPatch(typeof(SaveData), "GetWholePassives")]
        [HarmonyPostfix]
        public static void PassiveSort(SaveData __instance, ref List<int> list)
        {
            list.Sort((a, b) =>
            {
                PassiveData passiveA = Passive.Get(a);
                PassiveData passiveB = Passive.Get(b);

                int qualityA = passiveA?.m_quality ?? 0;
                int qualityB = passiveB?.m_quality ?? 0;

                // Higher quality first
                return qualityB.CompareTo(qualityA);
            });
            foreach (int id in list)
            {
                //MelonLogger.Msg($"Passive ID: {id}");
            }
            //list.Remove(10310);
        }

        [HarmonyPatch(typeof(Role), "SkillAni")]
        [HarmonyPrefix]
        public static bool ApplyAnim(Role __instance, BaseSkill skill, ref TrackEntry __result, bool needDebug)
        {
            if (!Helpers.IsPlayer(__instance)) return true;
            if (skill.m_id == 69420 || skill.m_id == 10300)
            {
                int id = 6942;
                //MelonLogger.Msg($"Changed Track of Passive: 69420");
                __result = __instance.m_controller.Play(string.Format("{0}", id), false, needDebug, true);
                return false;
            }
            else if (skill.m_id == 69421)
            {
                int id = 50015;
                __result = __instance.m_controller.Play(string.Format("{0}", id), false, needDebug, true);
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(SaveManager), "Load", new Type[] {typeof(SaveData)})]
        [HarmonyPostfix]
        public static void AddItems(SaveData saveData)
        {
            if (!ModConfig.SpawnManuals.Value) return;
            if (!saveData.GotItem(50022))
            {
                Helpers.AddAllSkillManuals();
            }
        }
    }
}