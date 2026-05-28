using MelonLoader;
using PsychicKungfu_MelonMod;
using PsychicKungfu_MelonMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[assembly: MelonInfo(typeof(PsychicKungfu_MelonMod.Main), PsychicKungfu_MelonMod.Main.PluginName, PsychicKungfu_MelonMod.Main.PluginVersion, "izayoi")]
[assembly: MelonGame("金十四工作室", "JinGu")]

namespace PsychicKungfu_MelonMod
{
    public class Main : MelonMod
    {
        public const string PluginName = "Personal Mod";
        public const string PluginVersion = "1.0.0";

        //internal static MelonLogger.Instance Log = null;

        public override void OnInitializeMelon()
        {
            DBLoadManager.DumpAll(DumpLanguage.English);
            DBLoadManager.LoadOverrides();
            MelonLogger.Msg($"Trying to patch...");
            HarmonyInstance.PatchAll();
            AnimationMerger.Initialize();
            MelonLogger.Msg($"Succesfully applied patches!");

            
        }


    }
}
