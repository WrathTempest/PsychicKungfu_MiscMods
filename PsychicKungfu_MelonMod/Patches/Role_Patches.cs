using DBLoad;
using Fight;
using HarmonyLib;
using PsychicKungfu_MelonMod.Utils;
using Spine;
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
            if (!__instance.m_actives.Any(x => x.m_id == 502790))
            {
                //__instance.m_actives.Add(new ActiveSkill(502790, __instance));
                //__instance.m_actives.Add(new ActiveSkill(500180, __instance));
                //__instance.m_actives.Add(new ActiveSkill(503600, __instance));
                //Great Sun Buddha Palm
                //Vajra Chan
                //Mountain River Zen
                //A supreme technique created by patriarch
                //Taiqing free and easy realm
                //Northern Abyss
                //Dripping Star sword tech
                //Sweep the Eight Wastes
                //Great Freedom Bitter (passive trigger on damage)
            }
            if (!Helpers.PlayerHasGlobalBuff(6942)) return;
            Helpers.SetPrivateField<int>(__instance, "m_move", 8);
            __instance.m_passives.Sort((a, b) =>
            {
                int qualityA = a?.Data.m_quality ?? 0;
                int qualityB = b?.Data.m_quality ?? 0;
                return qualityB.CompareTo(qualityA);
            });
            Helpers.SetPrivateField<string>(__instance.m_data, "m_chengHao", "Realm of the Gods");

        }

        [HarmonyPatch(typeof(FightWindow), "SetSkill")]
        [HarmonyPrefix]
        public static void PassiveSort(FightWindow __instance, Role role)
        {
            if (role == null) return;
            if (!Helpers.IsPlayer(role)) return;
            if (SkillReplacer._enabled)
            {
                SkillReplacer.ApplyReplacement(role);
            }
            else
            {
                SkillReplacer.RestoreOriginal(role);
            }
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
            Helpers.AddAllSkillManuals();
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
            return true;
        }
    }
}