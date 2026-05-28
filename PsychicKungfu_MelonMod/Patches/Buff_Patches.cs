using DBLoad;
using HarmonyLib;
using MelonLoader;
using PsychicKungfu_MelonMod.Utils;
using UnityEngine;

namespace PsychicKungfu_MelonMod.Patches
{
    [HarmonyPatch]
    internal class Buff_Patches
    {
        [HarmonyPatch(typeof(Role), "ImmuneBuff")]
        [HarmonyPrefix]
        public static bool DebuffImmune(Role __instance, int buffId, ref bool __result)
        {
            BuffData buffData = Buff.Get(buffId);
            if (buffData.m_de != 1) return true;
            if (!Helpers.HasPassive(__instance, 69420)) return true;
            if (!Helpers.HasBuff(__instance, 6942)) return true; //only in second phase
            //MelonLogger.Msg($"Negated Debuff: {buffId}");
            //buffInfo.EffectShow();
            __result = true;
            return false;
        }
    }
}