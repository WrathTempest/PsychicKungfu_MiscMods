using DBLoad;
using HarmonyLib;
using PsychicKungfu_MelonMod.Utils;
using UnityEngine;

namespace PsychicKungfu_MelonMod.Patches
{
    [HarmonyPatch]
    internal class NPCInfo_Patches
    {
        [HarmonyPatch(typeof(NpcInfo), "set_Favo")]
        [HarmonyPrefix]
        public static void MinimumFavor(NpcInfo __instance, ref int value)
        {
            int delta = value - __instance.Favo;
            if (!Helpers.PlayerHasGlobalBuff(6942)) return;
            if (delta < 0) return;
            if (delta < 30)
            {
                delta = 30;
                value = __instance.Favo + delta;
            }
        }

        [HarmonyPatch(typeof(NpcInfo), "Robe")]
        [HarmonyPrefix]
        public static bool PlunderFavor(NpcInfo __instance)
        {
            if (!Helpers.PlayerHasGlobalBuff(6942)) return true;
            __instance.Robed();
            SaveData saveData = MonoSingleton<SaveManager>.Instance.SaveData;
            NpcData npcData = Npc.Get(__instance.m_id);
            saveData.AddEffectValue(EffectId.善恶, (float)npcData.m_evil, true);
            int globalValue = -1;
            UIUtlils.RollUpTips((globalValue > 0) ? 52 : 53, new object[]
            {
            __instance.Name,
            Mathf.Abs(globalValue)
            });
            return false;
        }
    }
}