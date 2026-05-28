using HarmonyLib;
using MelonLoader;
using PsychicKungfu_MelonMod.Utils;
using UnityEngine;

namespace PsychicKungfu_MelonMod.Patches
{
    [HarmonyPatch]
    internal class UIUtils_Patches
    {
        [HarmonyPatch(typeof(UIUtlils), nameof(UIUtlils.LoadSkill))]
        [HarmonyPostfix]
        public static void LoadSkillSprite(ref Sprite __result, int id)
        {
            if (__result != null) return;
            id = 50769;
            __result = Singleton<ResManager>.Instance.LoadSprite("Atlas/Skill", $"Skill_" + id);
        }
    }
}