using DBLoad;
using HarmonyLib;
using PsychicKungfu_MelonMod.Utils;

namespace PsychicKungfu_MelonMod.Patches
{
    [HarmonyPatch]
    internal class SaveData_Patches
    {
        [HarmonyPatch(typeof(SaveData), "get_FullName")]
        [HarmonyPostfix]
        public static void ChangePlayerName(SaveData __instance, ref string __result)
        {
            //MelonLogger.Msg($"Successfully changed player name!");
            string name = ModConfig.CustomName.Value;
            __instance.m_leaderFamily = name;
            __instance.m_leaderName = "";
            __result = name;
        }

        [HarmonyPatch(typeof(SaveData), "AddEffectValue")]
        [HarmonyPrefix]
        public static void ValueBoost(ref float add, EffectId id)
        {
            if (!Helpers.PlayerHasGlobalBuff(6942)) return;
            int effect = (int)id;
            if (effect < 0x12D || effect > 0x134) return;
            add *= 2;
        }

        [HarmonyPatch(typeof(SaveData), "ReadWuXue")]
        [HarmonyPostfix]
        public static void MaxWuxueOnRead(SaveData __instance, bool __result, int id)
        {
            if (!ModConfig.MaxSkillExp.Value) return;
            if (!__result) return;
            WuXueData data = WuXue.Get(id);
            if (data == null) return;
            __instance.AddWuXueExp(id, data.m_exp * 20, true);
        }
    }
}