using DBLoad;
using HarmonyLib;
using Spine;
using Spine.Unity;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PsychicKungfu_MelonMod.Utils
{
    public static class AnimationMerger
    {
        // ── 1. Standard Mode: Full-String Exact Matches (Case-Insensitive) ───
        // If a slot name matches any of these strings EXACTLY, it will bypass the shadow overlay.
        public static readonly HashSet<string> VfxExactNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
        };

        // ── 2. Standard Mode: Partial Substring Matches (Case-Insensitive) ───
        // If a slot name CONTAINS any of these keywords, it will bypass the shadow overlay.
        public static readonly List<string> VfxKeywords = new List<string>
        {
            "guang", "star", "atk", "eff", "vfx", "qi", "dun", "bo", "lun", "dao", "jian"
        };

        // ── 3. Opposite Mode: Full-String Exact Matches (Case-Insensitive) ───
        // TRIGGER: If a skeleton contains ANY slot matching this collection, the script flips
        // to Opposite Mode. ONLY the elements matching these rules are blacked out; all else stays clear.
        public static readonly HashSet<string> ExclusiveBlackoutNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {

        };

        // ── 4. Opposite Mode: Partial Substring Matches (Case-Insensitive) ───
        // TRIGGER: If a skeleton contains ANY slot name containing these keywords, it also triggers
        // Opposite Mode. Only slots matching these keywords or the exact list above will be blacked out.
        // NOTE: This list will be dynamically appended with slots that have a global occurrence count of 1.
        public static readonly List<string> ExclusiveBlackoutKeywords = new List<string>
        {
             "yueque",
        };

        // ── 5. Independent Blackout Mode: Full-String Exact Matches Only (Case-Insensitive) ───
        // Slots matching these strings exactly will be blacked out across modes, but will NOT 
        // flip a skeleton into full Opposite Mode or auto-whitelist remaining slots into VfxExactNames.
        public static readonly HashSet<string> IndependentBlackoutNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "t", "yan", "st"
        };

        // ── Data structures ───────────────────────────────────────────────────

        private static readonly Dictionary<string, PoolEntry> _pool =
            new Dictionary<string, PoolEntry>(StringComparer.Ordinal);

        private static readonly Dictionary<SkeletonData, HashSet<string>> _injected =
            new Dictionary<SkeletonData, HashSet<string>>();

        private static readonly Dictionary<int, SkeletonAnimation> _ghosts =
            new Dictionary<int, SkeletonAnimation>();

        // Central collection to store and count unique slot frequencies across all scanned resources
        private static readonly Dictionary<string, int> _globalSlotCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private struct PoolEntry
        {
            public Spine.Animation Animation;
            public int SourceBones;
            public int SourceSlots;
            public int SourceRes;
        }

        // ── Cached reflection ─────────────────────────────────────────────────

        private static readonly FieldInfo _animationsField =
            typeof(SkeletonData).GetField(
                "animations",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo _animField =
            typeof(RoleController).GetField(
                "m_animation",
                BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo _roleIdField =
            typeof(RoleController).GetField(
                "m_roleId",
                BindingFlags.NonPublic | BindingFlags.Instance);

        // ── OrientationSyncer ─────────────────────────────────────────────────

        private class OrientationSyncer : MonoBehaviour
        {
            public SkeletonAnimation Source;
            public SkeletonAnimation Target;

            public void Sync()
            {
                if (Source == null || Target == null) return;

                // Copy both spatial orientation options to catch models using transform-based flipping
                Target.transform.localRotation = Source.transform.localRotation;
                Target.transform.localScale = Source.transform.localScale;

                if (Target.Skeleton != null && Source.Skeleton != null)
                {
                    Target.Skeleton.ScaleX = Source.Skeleton.ScaleX;
                    Target.Skeleton.ScaleY = Source.Skeleton.ScaleY;

                    // Force internal matrix recalculation so orientation changes instantly
                    Target.Skeleton.UpdateWorldTransform();
                }
            }

            // Sync on LateUpdate to ensure we run AFTER the base controller calculates its positioning rules
            private void LateUpdate() => Sync();
        }

        // ── Initialization & Slot Collection ──────────────────────────────────

        public static void EnhanceQiAura(SkeletonAnimation mainAnim, float targetR, float targetG, float targetB, float boneScale = 1.4f)
        {
            if (mainAnim == null || mainAnim.Skeleton == null) return;

            // 1. Find the active skin
            var currentSkin = mainAnim.Skeleton.Skin ?? mainAnim.Skeleton.Data.DefaultSkin;
            if (currentSkin == null) return;

            // 2. Find all slot indices that contain an attachment named "qi"
            System.Collections.Generic.HashSet<int> qiSlotIndices = new System.Collections.Generic.HashSet<int>();
            foreach (var entry in currentSkin.Attachments)
            {
                if (entry.Name == "qi")
                {
                    qiSlotIndices.Add(entry.SlotIndex);
                }
            }

            // Safety check: if no "qi" attachment is active in this skin, exit early
            if (qiSlotIndices.Count == 0) return;

            // 3. Subscribe to the callback using the resolved slot indices
            mainAnim.UpdateLocal += (anim) =>
            {
                foreach (int index in qiSlotIndices)
                {
                    // Direct array lookup using the index we verified from the skin
                    var slot = anim.Skeleton.Slots.Items[index];
                    if (slot == null) continue;

                    // Calculate the current animation's fading intensity dynamically
                    float intensity = UnityEngine.Mathf.Max(slot.R, slot.G, slot.B) * slot.A;

                    // Apply your custom color, scaled by the animation's natural curve
                    slot.R = targetR * intensity;
                    slot.G = targetG * intensity;
                    slot.B = targetB * intensity;

                    // Scale the slot's bone dynamically
                    if (slot.Bone != null)
                    {
                        slot.Bone.ScaleX = boneScale;
                        slot.Bone.ScaleY = boneScale;
                    }
                }
            };
        }
        public static void Initialize()
        {
            for (int i = 0; i < 30; i++)
            {
                IndependentBlackoutNames.Add(i.ToString());
                IndependentBlackoutNames.Add("q" + i.ToString());
                IndependentBlackoutNames.Add("l" + i.ToString());
                IndependentBlackoutNames.Add("r" + i.ToString());
                IndependentBlackoutNames.Add("s" + i.ToString());
                IndependentBlackoutNames.Add("g" + i.ToString());
                IndependentBlackoutNames.Add("f" + i.ToString());
                IndependentBlackoutNames.Add("y" + i.ToString());
                IndependentBlackoutNames.Add("d" + i.ToString());
            }
            if (_animationsField == null)
            {
                Main.Log.LogError(
                    "[AnimMerger] SkeletonData.animations field not found. Aborting.");
                return;
            }

            if (Character.Dic == null || Character.Dic.Count == 0)
            {
                Main.Log.LogError(
                    "[AnimMerger] Character.Dic is null/empty — called too early? Aborting.");
                return;
            }

            var seen = new HashSet<int>();
            int total = 0;
            foreach (var kv in Character.Dic)
                if (kv.Value != null && seen.Add(kv.Value.m_res)) total++;

            Main.Log.LogInfo(
                $"[AnimMerger] Starting prefab scan — " +
                $"{Character.Dic.Count} entries, {total} unique res IDs.");

            seen.Clear();
            int scanned = 0, skipped = 0, failed = 0;

            // Temporary map tracking which unique slots belong to each unique resource ID (m_res)
            var resToSlots = new Dictionary<int, List<string>>();

            foreach (var kv in Character.Dic)
            {
                try
                {
                    var cd = kv.Value;
                    if (cd == null) { skipped++; continue; }
                    if (!seen.Add(cd.m_res)) { skipped++; continue; }

                    string path = $"Prefabs/Roles/{cd.m_res}/{cd.m_res}";
                    var obj = Singleton<ResManager>.Instance.Load<UnityEngine.Object>(path);
                    if (!(obj is GameObject prefab))
                    {
                        skipped++;
                        continue;
                    }

                    var sa = prefab.GetComponentInChildren<SkeletonAnimation>(true);
                    if (sa?.skeletonDataAsset == null) { skipped++; continue; }

                    SkeletonData data = sa.skeletonDataAsset.GetSkeletonData(true);
                    if (data == null) { skipped++; continue; }

                    // Collect, map, and count all slot occurrences globally
                    var currentResSlots = new List<string>();
                    for (int i = 0; i < data.Slots.Count; i++)
                    {
                        var slotData = data.Slots.Items[i];
                        if (slotData != null && !string.IsNullOrEmpty(slotData.Name))
                        {
                            _globalSlotCounts.TryGetValue(slotData.Name, out int count);
                            _globalSlotCounts[slotData.Name] = count + 1;
                            currentResSlots.Add(slotData.Name);
                        }
                    }
                    resToSlots[cd.m_res] = currentResSlots;

                    int srcBones = data.Bones.Count;
                    int srcSlots = data.Slots.Count;
                    int added = 0;
                    int enumPath = 0;

                    foreach (var anim in EnumerateAnimations(data, out enumPath))
                    {
                        if (anim == null || string.IsNullOrEmpty(anim.Name)) continue;
                        if (!_pool.ContainsKey(anim.Name))
                        {
                            _pool[anim.Name] = new PoolEntry
                            {
                                Animation = anim,
                                SourceBones = srcBones,
                                SourceSlots = srcSlots,
                                SourceRes = cd.m_res
                            };
                            added++;
                        }
                    }

                    scanned++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Main.Log.LogError($"[AnimMerger] Scan exception: {ex}");
                }
            }

            Main.Log.LogInfo(
                $"[AnimMerger] Scan complete — scanned={scanned} skipped={skipped} failed={failed} pool={_pool.Count}");

            // 1. Log the full collection overview 
            //LogGlobalSlotInventory();

            // 2. Automatically feed strings with count == 1 into ExclusiveBlackoutKeywords
            //AutoPopulateExclusiveKeywords();

            // 3. Process relations between blackout slots and their source skeletons to auto-whitelist remainder slots
            ProcessBlackoutResForVfxWhitelisting(resToSlots);
        }

        // Automatically filters unique slot names into the keywords list
        private static void AutoPopulateExclusiveKeywords()
        {
            try
            {
                int addedCount = 0;
                foreach (var kvp in _globalSlotCounts)
                {
                    if (kvp.Value == 1)
                    {
                        if (!ExclusiveBlackoutKeywords.Contains(kvp.Key))
                        {
                            ExclusiveBlackoutKeywords.Add(kvp.Key);
                            addedCount++;
                        }
                    }
                }
                Main.Log.LogInfo($"[AnimMerger] Automatically processed and assigned {addedCount} unique slot names (global count = 1) into ExclusiveBlackoutKeywords.");
            }
            catch (Exception ex)
            {
                Main.Log.LogWarning($"[AnimMerger] AutoPopulateExclusiveKeywords failed: {ex.Message}");
            }
        }

        // Checks each scanned res; if it contains any blackout slots, adds all its other non-blacked-out slots to VfxExactNames
        private static void ProcessBlackoutResForVfxWhitelisting(Dictionary<int, List<string>> resToSlots)
        {
            try
            {
                int totalAdded = 0;

                foreach (var kvp in resToSlots)
                {
                    int resId = kvp.Key;
                    List<string> slots = kvp.Value;
                    bool resHasBlackoutTrigger = false;

                    // Pass 1: Determine if this resource contains any blacked-out slots (IndependentBlackoutNames IS EXCLUDED HERE)
                    for (int i = 0; i < slots.Count; i++)
                    {
                        string slotName = slots[i];

                        // Check exact names map
                        if (ExclusiveBlackoutNames.Contains(slotName))
                        {
                            resHasBlackoutTrigger = true;
                            break;
                        }

                        // Check keyword partial substrings
                        for (int j = 0; j < ExclusiveBlackoutKeywords.Count; j++)
                        {
                            if (slotName.IndexOf(ExclusiveBlackoutKeywords[j], StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                resHasBlackoutTrigger = true;
                                break;
                            }
                        }

                        if (resHasBlackoutTrigger) break;
                    }

                    // Pass 2: If triggered, whitelist every remaining slot that isn't itself part of the blackout rule
                    if (resHasBlackoutTrigger)
                    {
                        for (int i = 0; i < slots.Count; i++)
                        {
                            string slotName = slots[i];
                            bool isThisSlotBlackedOut = ExclusiveBlackoutNames.Contains(slotName);

                            if (!isThisSlotBlackedOut)
                            {
                                for (int j = 0; j < ExclusiveBlackoutKeywords.Count; j++)
                                {
                                    if (slotName.IndexOf(ExclusiveBlackoutKeywords[j], StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        isThisSlotBlackedOut = true;
                                        break;
                                    }
                                }
                            }

                            // If it's a normal skeleton part on a blackout skeleton, add it to VfxExactNames to keep it clear
                            if (!isThisSlotBlackedOut)
                            {
                                if (VfxExactNames.Add(slotName))
                                {
                                    totalAdded++;
                                }
                            }
                        }
                    }
                }

                if (totalAdded > 0)
                {
                    Main.Log.LogInfo($"[AnimMerger] Cross-reference check complete. Dynamically appended {totalAdded} non-blackout slots into VfxExactNames from skeletons operating on Opposite Mode.");
                }
            }
            catch (Exception ex)
            {
                Main.Log.LogWarning($"[AnimMerger] ProcessBlackoutResForVfxWhitelisting failed: {ex.Message}");
            }
        }

        // Aggregated Slot Log Generator
        private static void LogGlobalSlotInventory()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine();
                sb.AppendLine($"========================================================================");
                sb.AppendLine($"[AnimMerger] GLOBAL SLOT NAME INVENTORY ({_globalSlotCounts.Count} Unique Slots Across All Prefabs):");
                sb.AppendLine($"========================================================================");

                var sortedSlots = new List<string>(_globalSlotCounts.Keys);
                sortedSlots.Sort();

                foreach (var slotName in sortedSlots)
                {
                    int count = _globalSlotCounts[slotName];
                    sb.AppendLine($"  • Slot: '{slotName}'  [Occurrences/Duplicates: {count}]");
                }
                sb.AppendLine($"========================================================================");

                Main.Log.LogInfo(sb.ToString());
            }
            catch (Exception ex)
            {
                Main.Log.LogWarning($"[AnimMerger] LogGlobalSlotInventory failed to build report: {ex.Message}");
            }
        }

        // ── Ghost skeleton management ─────────────────────────────────────────

        private static SkeletonAnimation GetOrCreateGhost(int res)
        {
            if (_ghosts.TryGetValue(res, out var existing) && existing != null)
                return existing;

            string path = $"Prefabs/Roles/{res}/{res}";
            var obj = Singleton<ResManager>.Instance.Load<UnityEngine.Object>(path);
            if (!(obj is GameObject prefab))
            {
                Main.Log.LogWarning($"[AnimMerger] Ghost: prefab not found for res={res}");
                return null;
            }

            GameObject ghost = UnityEngine.Object.Instantiate(prefab);
            ghost.name = $"[GhostSkeleton_{res}]";
            UnityEngine.Object.DontDestroyOnLoad(ghost);

            foreach (var r in ghost.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (var c in ghost.GetComponentsInChildren<Collider2D>(true)) c.enabled = false;
            foreach (var rb in ghost.GetComponentsInChildren<Rigidbody2D>(true)) rb.isKinematic = true;

            foreach (var mb in ghost.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(mb is SkeletonAnimation)) mb.enabled = false;

            var sa = ghost.GetComponentInChildren<SkeletonAnimation>(true);
            if (sa == null)
            {
                UnityEngine.Object.Destroy(ghost);
                return null;
            }

            if (res < 7)
            {
                sa.skeleton.SetSkin(MonoSingleton<SaveManager>.Instance.SaveData.Skin);
                EnhanceQiAura(sa, 0f, 0f, 0f, 1.2f);
            }
                

            // This now includes global counter contexts per slot inline
            //LogSlotInventory(sa, res);

            // ── Dynamic Dual-Dictionary Shadow Filter (NPC/monster ghosts only) ──
            if (res >= 7)
            {
                sa.UpdateLocal += (animated) =>
                {
                    if (animated.Skeleton == null) return;

                    var slotsList = animated.Skeleton.Slots;
                    bool useExclusiveMode = false;

                    // Pass 1: Pre-scan slots to check if standard mode or opposite mode applies (Garbage-Free)
                    for (int i = 0; i < slotsList.Count; i++)
                    {
                        var slot = slotsList.Items[i];
                        if (slot?.Data == null || string.IsNullOrEmpty(slot.Data.Name)) continue;

                        string sName = slot.Data.Name;

                        // Check 1A: Exact Name Match
                        if (ExclusiveBlackoutNames.Contains(sName) || IndependentBlackoutNames.Contains(sName))
                        {
                            useExclusiveMode = true;
                            break;
                        }

                        // Check 1B: Keyword Substring Match
                        for (int j = 0; j < ExclusiveBlackoutKeywords.Count; j++)
                        {
                            if (sName.IndexOf(ExclusiveBlackoutKeywords[j], StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                useExclusiveMode = true;
                                break;
                            }
                        }

                        if (useExclusiveMode) break;
                    }

                    // Pass 2: Evaluate and apply blackouts based on active mode context
                    for (int i = 0; i < slotsList.Count; i++)
                    {
                        var slot = slotsList.Items[i];
                        if (slot?.Data == null || string.IsNullOrEmpty(slot.Data.Name)) continue;

                        string slotName = slot.Data.Name;

                        // Force blackout if found in IndependentBlackoutNames (Works in BOTH standard and opposite modes)
                        if (IndependentBlackoutNames.Contains(slotName))
                        {
                            slot.R = 0f; slot.G = 0f; slot.B = 0f;
                            if (slot.A > 0.75f) slot.A = 0.75f;
                            continue;
                        }

                        if (useExclusiveMode)
                        {
                            // ── OPPOSITE MODE ACTIVE ──────────────────────────
                            bool shouldBlackout = ExclusiveBlackoutNames.Contains(slotName);

                            if (!shouldBlackout)
                            {
                                for (int j = 0; j < ExclusiveBlackoutKeywords.Count; j++)
                                {
                                    if (slotName.IndexOf(ExclusiveBlackoutKeywords[j], StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        shouldBlackout = true;
                                        break;
                                    }
                                }
                            }

                            if (shouldBlackout)
                            {
                                slot.R = 0f; slot.G = 0f; slot.B = 0f;
                                if (slot.A > 0.75f) slot.A = 0.75f;
                            }
                        }
                        else
                        {
                            // ── STANDARD FILTERING ACTIVE ─────────────────────
                            if (slot.Data.BlendMode == BlendMode.Additive) continue;
                            if (VfxExactNames.Contains(slotName)) continue;

                            bool shouldSkip = false;
                            for (int j = 0; j < VfxKeywords.Count; j++)
                            {
                                if (slotName.IndexOf(VfxKeywords[j], StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    shouldSkip = true;
                                    break;
                                }
                            }

                            if (shouldSkip) continue;

                            slot.R = 0f; slot.G = 0f; slot.B = 0f;
                            if (slot.A > 0.75f) slot.A = 0.75f;
                        }
                    }
                };
            }

            sa.enabled = true;
            sa.gameObject.SetActive(true);
            ghost.SetActive(true);

            _ghosts[res] = sa;
            return sa;
        }

        // Updated function to query current global registry occurrence stats per slot string
        public static void LogSlotInventory(SkeletonAnimation sa, int res)
        {
            try
            {
                var slots = sa.Skeleton.Data.Slots;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"[AnimMerger] Slot inventory res={res} ({slots.Count} slots):");

                for (int i = 0; i < slots.Count; i++)
                {
                    var sd = slots.Items[i];
                    if (sd == null) continue;

                    string boneName = sd.BoneData?.Name ?? "None";
                    string slotName = sd.Name ?? "Unknown";

                    // Lookup current occurrence counts safely inside our central dictionary
                    int globalCount = 0;
                    if (!string.IsNullOrEmpty(slotName))
                    {
                        _globalSlotCounts.TryGetValue(slotName, out globalCount);
                    }

                    sb.AppendLine($"  [{i:000}] '{slotName}' [Bone: '{boneName}'] blend={sd.BlendMode} (Global Occurrences: {globalCount})");
                }

                Main.Log.LogInfo(sb.ToString());
            }
            catch (Exception ex)
            {
                Main.Log.LogWarning($"[AnimMerger] LogSlotInventory(res={res}) failed: {ex.Message}");
            }
        }

        // ── Direct injection (compatible skeletons only) ───────────────────────

        public static bool EnsureAnimation(SkeletonData target, string animName)
        {
            try
            {
                if (target == null || string.IsNullOrEmpty(animName)) return false;
                if (target.FindAnimation(animName) != null) return true;

                if (!_pool.TryGetValue(animName, out var entry)) return false;

                int tBones = target.Bones.Count;
                int tSlots = target.Slots.Count;
                if (entry.SourceBones != tBones || entry.SourceSlots != tSlots) return false;

                if (!_injected.TryGetValue(target, out var injSet))
                    _injected[target] = injSet = new HashSet<string>();

                if (!injSet.Add(animName))
                    return target.FindAnimation(animName) != null;

                object listObj = _animationsField.GetValue(target);
                if (listObj == null) return false;

                var addMethod = listObj.GetType().GetMethod("Add", new[] { typeof(Spine.Animation) });
                if (addMethod == null) return false;

                addMethod.Invoke(listObj, new object[] { entry.Animation });
                return target.FindAnimation(animName) != null;
            }
            catch (Exception ex)
            {
                Main.Log.LogError($"[AnimMerger] EnsureAnimation('{animName}') threw:\n{ex}");
                return false;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static IEnumerable<Spine.Animation> EnumerateAnimations(SkeletonData data, out int enumPath)
        {
            enumPath = 0;
            if (data == null || _animationsField == null) return System.Linq.Enumerable.Empty<Spine.Animation>();

            object listObj = _animationsField.GetValue(data);
            if (listObj == null) return System.Linq.Enumerable.Empty<Spine.Animation>();

            if (listObj is System.Collections.IEnumerable enumerable)
            {
                enumPath = 1;
                return EnumerateViaInterface(enumerable);
            }

            Type listType = listObj.GetType();
            FieldInfo items = listType.GetField("Items", BindingFlags.Public | BindingFlags.Instance);
            FieldInfo countF = listType.GetField("Count", BindingFlags.Public | BindingFlags.Instance);

            if (items != null && countF != null)
            {
                enumPath = 2;
                return EnumerateViaFields(listObj, items, countF);
            }

            return System.Linq.Enumerable.Empty<Spine.Animation>();
        }

        private static IEnumerable<Spine.Animation> EnumerateViaInterface(System.Collections.IEnumerable source)
        {
            foreach (object obj in source)
                if (obj is Spine.Animation a) yield return a;
        }

        private static IEnumerable<Spine.Animation> EnumerateViaFields(object listObj, FieldInfo itemsField, FieldInfo countField)
        {
            var arr = itemsField.GetValue(listObj) as System.Collections.IList;
            int count = (int)countField.GetValue(listObj);
            if (arr == null) yield break;
            for (int i = 0; i < count; i++)
                if (arr[i] is Spine.Animation a) yield return a;
        }

        // ── Harmony patch ─────────────────────────────────────────────────────

        private static readonly Dictionary<RoleController, SwapContext> _activeSwaps =
            new Dictionary<RoleController, SwapContext>();

        private class SwapContext
        {
            public SkeletonAnimation OriginalSA;
            public SkeletonAnimation GhostSA;
            public Renderer PlayerRenderer;
            public Renderer GhostRenderer;
            public Transform GhostRoot;
            public int OriginalRoleId;
        }

        [HarmonyPatch(typeof(RoleController), "Play")]
        public static class RoleControllerPlayPatch
        {
            static void Prefix(RoleController __instance, string name, bool loop, bool isSkill)
            {
                try
                {
                    if (!isSkill || string.IsNullOrEmpty(name)) return;
                    if (!int.TryParse(name, out _)) return;

                    var originalSA = _animField?.GetValue(__instance) as SkeletonAnimation;
                    var data = originalSA?.AnimationState?.Data?.SkeletonData;
                    if (data == null) return;

                    if (data.FindAnimation(name) != null) return;
                    if (!_pool.TryGetValue(name, out var entry)) return;
                    if (EnsureAnimation(data, name)) return;

                    var ghostSA = GetOrCreateGhost(entry.SourceRes);
                    if (ghostSA == null) return;

                    int originalRoleId = (int)(_roleIdField?.GetValue(__instance) ?? 0);
                    _roleIdField?.SetValue(__instance, entry.SourceRes);

                    Transform ghostRoot = ghostSA.transform.root;
                    ghostRoot.SetParent(__instance.transform);
                    ghostRoot.localPosition = Vector3.zero;
                    ghostRoot.localRotation = Quaternion.identity;
                    ghostRoot.localScale = Vector3.one;

                    var syncer = ghostSA.GetComponent<OrientationSyncer>()
                               ?? ghostSA.gameObject.AddComponent<OrientationSyncer>();
                    syncer.Source = originalSA;
                    syncer.Target = ghostSA;
                    syncer.enabled = true;

                    // Force a sync run immediately right here during frame creation 
                    // so the ghost registers the correct flipped state before render pass
                    syncer.Sync();

                    var playerRenderer = originalSA.GetComponent<Renderer>();
                    var ghostRenderer = ghostSA.GetComponent<Renderer>();
                    if (playerRenderer != null) playerRenderer.enabled = false;
                    if (ghostRenderer != null) ghostRenderer.enabled = true;

                    _animField.SetValue(__instance, ghostSA);

                    _activeSwaps[__instance] = new SwapContext
                    {
                        OriginalSA = originalSA,
                        GhostSA = ghostSA,
                        PlayerRenderer = playerRenderer,
                        GhostRenderer = ghostRenderer,
                        GhostRoot = ghostRoot,
                        OriginalRoleId = originalRoleId
                    };
                }
                catch (Exception ex)
                {
                    Main.Log.LogError($"[AnimMerger] Swap Prefix threw:\n{ex}");
                }
            }

            static void Postfix(RoleController __instance, string name, bool isSkill, TrackEntry __result)
            {
                if (!_activeSwaps.TryGetValue(__instance, out var ctx)) return;
                _activeSwaps.Remove(__instance);

                if (__result == null)
                {
                    RestoreSwap(__instance, name, ctx);
                    return;
                }

                ctx.GhostSA.GetComponent<OrientationSyncer>()?.Sync();

                var rc = __instance;
                __result.End += (entry) =>
                {
                    try
                    {
                        var current = _animField?.GetValue(rc) as SkeletonAnimation;
                        if (current != ctx.GhostSA) return;
                        RestoreSwap(rc, name, ctx);
                    }
                    catch (Exception ex)
                    {
                        Main.Log.LogError($"[AnimMerger] End callback for '{name}' threw:\n{ex}");
                    }
                };
            }

            private static void RestoreSwap(RoleController rc, string animName, SwapContext ctx)
            {
                var syncer = ctx.GhostSA.GetComponent<OrientationSyncer>();
                if (syncer != null) syncer.enabled = false;

                _animField.SetValue(rc, ctx.OriginalSA);
                _roleIdField?.SetValue(rc, ctx.OriginalRoleId);

                if (ctx.GhostRoot != null)
                {
                    ctx.GhostRoot.SetParent(null);
                    UnityEngine.Object.DontDestroyOnLoad(ctx.GhostRoot.gameObject);
                }

                if (ctx.GhostRenderer != null) ctx.GhostRenderer.enabled = false;
                if (ctx.PlayerRenderer != null) ctx.PlayerRenderer.enabled = true;
            }
        }
    }


}