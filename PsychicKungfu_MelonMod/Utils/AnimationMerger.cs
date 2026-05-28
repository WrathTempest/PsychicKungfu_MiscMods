using DBLoad;
using HarmonyLib;
using MelonLoader;
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
        // ── Data structures ───────────────────────────────────────────────────

        private struct PoolEntry
        {
            public Spine.Animation Animation;
            public int SourceBones;
            public int SourceSlots;
            public int SourceRes;
        }

        // name → first skeleton in scan that had this animation
        private static readonly Dictionary<string, PoolEntry> _pool =
            new Dictionary<string, PoolEntry>(StringComparer.Ordinal);

        // Per-skeleton: names successfully injected (compatible case, no swap needed)
        private static readonly Dictionary<SkeletonData, HashSet<string>> _injected =
            new Dictionary<SkeletonData, HashSet<string>>();

        // res → persistent invisible ghost instance.
        // Renderer is OFF by default; Prefix enables it for the duration of the skill.
        private static readonly Dictionary<int, SkeletonAnimation> _ghosts =
            new Dictionary<int, SkeletonAnimation>();

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

        // ── Initialization ────────────────────────────────────────────────────

        public static void Initialize()
        {
            if (_animationsField == null)
            {
                MelonLogger.Error(
                    "[AnimMerger] SkeletonData.animations field not found — " +
                    "Spine version mismatch or stripping. Aborting.");
                return;
            }

            if (Character.Dic == null || Character.Dic.Count == 0)
            {
                MelonLogger.Error(
                    "[AnimMerger] Character.Dic is null/empty — called too early? Aborting.");
                return;
            }

            var seen = new HashSet<int>();
            int total = 0;
            foreach (var kv in Character.Dic)
                if (kv.Value != null && seen.Add(kv.Value.m_res)) total++;

            MelonLogger.Msg(
                $"[AnimMerger] Starting prefab scan — " +
                $"{Character.Dic.Count} entries, {total} unique res IDs.");

            seen.Clear();
            int scanned = 0, skipped = 0, failed = 0;

            foreach (var kv in Character.Dic)
            {
                try
                {
                    var cd = kv.Value;
                    if (cd == null) { skipped++; continue; }
                    if (!seen.Add(cd.m_res)) { skipped++; continue; }

                    string path = $"Prefabs/Roles/{cd.m_res}/{cd.m_res}";
                    var obj = Singleton<ResManager>.Instance.Load<UnityEngine.Object>(path);
                    var prefab = obj as GameObject;
                    if (prefab == null)
                    {
                        MelonLogger.Warning($"[AnimMerger] Not a GameObject: '{path}'");
                        skipped++;
                        continue;
                    }

                    var sa = prefab.GetComponentInChildren<SkeletonAnimation>(true);
                    if (sa?.skeletonDataAsset == null) { skipped++; continue; }

                    SkeletonData data = sa.skeletonDataAsset.GetSkeletonData(true);
                    if (data == null) { skipped++; continue; }

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

                    MelonLogger.Msg(
                        $"[AnimMerger] res={cd.m_res} bones={srcBones} slots={srcSlots} " +
                        $"+{added} new (enum_path={enumPath}) pool_total={_pool.Count}");
                    scanned++;
                }
                catch (Exception ex)
                {
                    failed++;
                    MelonLogger.Error($"[AnimMerger] Scan exception: {ex}");
                }
            }

            MelonLogger.Msg(
                $"[AnimMerger] Scan complete — " +
                $"scanned={scanned} skipped={skipped} failed={failed} pool={_pool.Count}");
        }

        // ── Ghost skeleton management ──────────────────────────────────────────
        //
        // Ghosts are persistent invisible GameObjects, one per unique res value.
        // All Renderers are disabled at creation time. The Prefix enables the
        // ghost's renderer and disables the player's renderer for the skill duration,
        // then the End callback reverses this.
        //
        // SkeletonAnimation stays enabled at all times so its AnimationState is
        // always ready to play — no initialization delay when a skill fires.
        //
        // All other MonoBehaviours (AI, movement, audio, etc.) are disabled so
        // the ghost has zero gameplay presence between uses.

        private static SkeletonAnimation GetOrCreateGhost(int res)
        {
            if (_ghosts.TryGetValue(res, out var existing) && existing != null)
                return existing;

            string path = $"Prefabs/Roles/{res}/{res}";
            var obj = Singleton<ResManager>.Instance.Load<UnityEngine.Object>(path);
            if (!(obj is GameObject prefab))
            {
                MelonLogger.Warning($"[AnimMerger] Ghost: prefab not found for res={res}");
                return null;
            }

            GameObject ghost = UnityEngine.Object.Instantiate(prefab);
            ghost.name = $"[GhostSkeleton_{res}]";
            UnityEngine.Object.DontDestroyOnLoad(ghost);

            // Renderers OFF — Prefix will enable the one we need during the skill
            foreach (var r in ghost.GetComponentsInChildren<Renderer>(true))
                r.enabled = false;

            // Remove physics presence
            foreach (var c in ghost.GetComponentsInChildren<Collider2D>(true))
                c.enabled = false;
            foreach (var rb in ghost.GetComponentsInChildren<Rigidbody2D>(true))
                rb.isKinematic = true;

            // Disable all game-logic MonoBehaviours; keep ONLY SkeletonAnimation.
            // This neutralises AI, movement controllers, audio, etc.
            foreach (var mb in ghost.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(mb is SkeletonAnimation))
                    mb.enabled = false;

            var sa = ghost.GetComponentInChildren<SkeletonAnimation>(true);
            if (sa == null)
            {
                MelonLogger.Warning(
                    $"[AnimMerger] Ghost res={res}: no SkeletonAnimation found — destroying");
                UnityEngine.Object.Destroy(ghost);
                return null;
            }

            // Keep SkeletonAnimation and its GameObject active so AnimationState is ready
            if (res < 7)
            {
                sa.skeleton.SetSkin(MonoSingleton<SaveManager>.Instance.SaveData.Skin);
            }
            
            sa.enabled = true;
            sa.gameObject.SetActive(true);
            ghost.SetActive(true);

            _ghosts[res] = sa;
            MelonLogger.Msg(
                $"[AnimMerger] Ghost created: res={res} " +
                $"bones={sa.Skeleton?.Bones?.Count} slots={sa.Skeleton?.Slots?.Count}");
            return sa;
        }

        // ── Direct injection (compatible skeletons only) ───────────────────────
        //
        // Used as a fast path when source and target bone/slot counts match exactly.
        // Permanently adds the animation to the player's SkeletonData — no swap needed.

        public static bool EnsureAnimation(SkeletonData target, string animName)
        {
            try
            {
                if (target == null || string.IsNullOrEmpty(animName)) return false;
                if (target.FindAnimation(animName) != null) return true;

                if (!_pool.TryGetValue(animName, out var entry))
                    return false;

                // Spine timelines address bones/slots by index — mismatched counts
                // corrupt the skeleton permanently. Only inject on exact match.
                int tBones = target.Bones.Count;
                int tSlots = target.Slots.Count;
                if (entry.SourceBones != tBones || entry.SourceSlots != tSlots)
                    return false; // caller will fall through to model-swap path

                // Dedup: don't re-inject if already attempted for this skeleton
                if (!_injected.TryGetValue(target, out var injSet))
                    _injected[target] = injSet = new HashSet<string>();

                if (!injSet.Add(animName))
                    return target.FindAnimation(animName) != null;

                // Inject
                MelonLogger.Msg(
                    $"[AnimMerger] Injecting '{animName}' | " +
                    $"source_res={entry.SourceRes} " +
                    $"duration={entry.Animation.Duration:F3}s " +
                    $"timelines={entry.Animation.Timelines.Count} | " +
                    $"target bones={tBones} slots={tSlots}");

                object listObj = _animationsField.GetValue(target);
                if (listObj == null)
                {
                    MelonLogger.Error("[AnimMerger] animations list is null on target skeleton.");
                    return false;
                }

                var addMethod = listObj.GetType()
                    .GetMethod("Add", new[] { typeof(Spine.Animation) });
                if (addMethod == null)
                {
                    MelonLogger.Error(
                        $"[AnimMerger] Add(Animation) not found on {listObj.GetType().FullName}.");
                    return false;
                }

                addMethod.Invoke(listObj, new object[] { entry.Animation });

                bool success = target.FindAnimation(animName) != null;
                MelonLogger.Msg(success
                    ? $"[AnimMerger] ✓ Injected '{animName}'"
                    : $"[AnimMerger] ✗ Add() called but FindAnimation still null for '{animName}'");
                return success;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[AnimMerger] EnsureAnimation('{animName}') threw:\n{ex}");
                return false;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static IEnumerable<Spine.Animation> EnumerateAnimations(
            SkeletonData data, out int enumPath)
        {
            enumPath = 0;
            if (data == null || _animationsField == null)
                return System.Linq.Enumerable.Empty<Spine.Animation>();

            object listObj = _animationsField.GetValue(data);
            if (listObj == null)
            {
                MelonLogger.Warning("[AnimMerger] animations field is null on skeleton.");
                return System.Linq.Enumerable.Empty<Spine.Animation>();
            }

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

            MelonLogger.Error(
                $"[AnimMerger] Cannot enumerate animations. " +
                $"Type={listType.FullName} IEnumerable=false Items={items != null} Count={countF != null}");
            return System.Linq.Enumerable.Empty<Spine.Animation>();
        }

        private static IEnumerable<Spine.Animation> EnumerateViaInterface(
            System.Collections.IEnumerable source)
        {
            foreach (object obj in source)
                if (obj is Spine.Animation a) yield return a;
        }

        private static IEnumerable<Spine.Animation> EnumerateViaFields(
            object listObj, FieldInfo itemsField, FieldInfo countField)
        {
            var arr = itemsField.GetValue(listObj) as System.Collections.IList;
            int count = (int)countField.GetValue(listObj);
            if (arr == null) yield break;
            for (int i = 0; i < count; i++)
                if (arr[i] is Spine.Animation a) yield return a;
        }

        // ── Harmony patch ─────────────────────────────────────────────────────
        //
        // When Play(name, loop, needDebug, isSkill=true) is called for a skill
        // animation that is missing from the player's skeleton, we intercept in
        // two stages:
        //
        // PRIORITY ORDER
        //   1. Animation already in player skeleton
        //      → Prefix does nothing, original Play() runs normally.
        //
        //   2. Skeleton-compatible source exists
        //      → EnsureAnimation() injects it permanently, original Play() finds it.
        //
        //   3. Incompatible source (different bone/slot count — the common case)
        //      → MODEL SWAP:
        //        Prefix  — position ghost at player location, match facing scale,
        //                   enable ghost renderer, disable player renderer,
        //                   replace RoleController.m_animation with ghost SA.
        //                   Return true → original Play() runs on ghost SA.
        //                   The game registers all its event listeners (particles,
        //                   hit callbacks) on the ghost's AnimationState, which is
        //                   at the player's world position → effects appear correctly.
        //        Postfix  — hook TrackEntry.End (fires on both normal finish AND
        //                   interruption) to restore m_animation and renderers.
        //                   Guard against double-restore by checking that m_animation
        //                   is still the ghost when End fires.
        //
        // WHY End AND NOT Complete?
        //   Complete fires only at the end of a full animation loop/play-through.
        //   End fires whenever the track is cleared, including skill interruption
        //   (taking a hit, death, dodge). Using End ensures the player model is
        //   always restored regardless of how the skill terminates.

        private static readonly Dictionary<RoleController, SwapContext> _activeSwaps =
                new Dictionary<RoleController, SwapContext>();

        private class SwapContext
        {
            public SkeletonAnimation OriginalSA;
            public SkeletonAnimation GhostSA;
            public Renderer PlayerRenderer;
            public Renderer GhostRenderer;

            // Stored BEFORE parenting — after SetParent the ghost's .root changes,
            // but the Transform reference itself remains valid for the restore call.
            public Transform GhostRoot;

            // Player's real m_roleId so AudioManager looks up the right audio bank.
            public int OriginalRoleId;
        }

        // ── Harmony patch ─────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(RoleController), "Play")]
        public static class RoleControllerPlayPatch
        {
            // ── Prefix ────────────────────────────────────────────────────────────
            // No __state parameter — state is kept in _activeSwaps instead.

            static void Prefix(
                RoleController __instance,
                string name,
                bool loop,
                bool isSkill)
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

                    if (EnsureAnimation(data, name))
                    {
                        MelonLogger.Msg($"[AnimMerger] '{name}' injected — no swap needed");
                        return;
                    }

                    var ghostSA = GetOrCreateGhost(entry.SourceRes);
                    if (ghostSA == null)
                    {
                        MelonLogger.Warning(
                            $"[AnimMerger] Swap aborted: ghost unavailable for res={entry.SourceRes}");
                        return;
                    }

                    // ── Sound fix: set m_roleId to source character's res ─────────────────
                    // RoleController.Play registers an event handler that calls:
                    //   AudioManager.PlayRoleEffect(e.Data.AudioPath, this.m_roleId)
                    // The roleId is used to look up the character's audio bank. Playing the
                    // source character's animation with the player's roleId finds no audio.
                    // We temporarily swap roleId to the source res and restore it after.
                    int originalRoleId = (int)(_roleIdField?.GetValue(__instance) ?? 0);
                    _roleIdField?.SetValue(__instance, entry.SourceRes);
                    MelonLogger.Msg(
                        $"[AnimMerger] Swap: m_roleId {originalRoleId} → {entry.SourceRes} (audio bank fix)");

                    // ── Movement fix: parent ghost to player root ─────────────────────────
                    // The player's position is driven by m_transform (the RoleController's
                    // own GameObject transform). DOTween dash/translate moves this transform,
                    // and all children move with it. Parenting the ghost here makes it follow
                    // every frame automatically — no per-frame update needed.
                    // Store the root reference NOW, before SetParent changes .root to the
                    // player's root.
                    Transform ghostRoot = ghostSA.transform.root;
                    ghostRoot.SetParent(__instance.transform);
                    ghostRoot.localPosition = Vector3.zero;
                    ghostRoot.localRotation = Quaternion.identity;
                    ghostRoot.localScale = Vector3.one;

                    // Mirror the player SA's facing rotation (set by RoleController.Rotate
                    // via DOLocalRotateQuaternion on the SA's own transform).
                    ghostSA.transform.localRotation = originalSA.transform.localRotation;

                    MelonLogger.Msg(
                        $"[AnimMerger] Swap: '{name}' | " +
                        $"player bones={data.Bones.Count} slots={data.Slots.Count} → " +
                        $"ghost res={entry.SourceRes} bones={entry.SourceBones} slots={entry.SourceSlots} | " +
                        $"worldPos={originalSA.transform.position} facing={ghostSA.transform.localRotation.eulerAngles}");

                    var playerRenderer = originalSA.GetComponent<Renderer>();
                    var ghostRenderer = ghostSA.GetComponent<Renderer>();

                    if (playerRenderer != null) playerRenderer.enabled = false;
                    else MelonLogger.Warning("[AnimMerger] Swap: player Renderer not found on SA GameObject");

                    if (ghostRenderer != null) ghostRenderer.enabled = true;
                    else MelonLogger.Warning("[AnimMerger] Swap: ghost Renderer not found on SA GameObject");

                    MelonLogger.Msg(
                        $"[AnimMerger] Swap: player renderer hidden={playerRenderer != null}, " +
                        $"ghost renderer shown={ghostRenderer != null}");

                    _animField.SetValue(__instance, ghostSA);
                    MelonLogger.Msg("[AnimMerger] Swap: m_animation replaced with ghost SA");

                    _activeSwaps[__instance] = new SwapContext
                    {
                        OriginalSA = originalSA,
                        GhostSA = ghostSA,
                        PlayerRenderer = playerRenderer,
                        GhostRenderer = ghostRenderer,
                        GhostRoot = ghostRoot,      // reference captured before re-parent
                        OriginalRoleId = originalRoleId
                    };
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[AnimMerger] Swap Prefix threw:\n{ex}");
                }
            }

            // ── Postfix ───────────────────────────────────────────────────────────

            static void Postfix(
                RoleController __instance,
                string name,
                bool isSkill,
                TrackEntry __result)
            {
                // Always log so we can confirm Postfix is actually being called
                MelonLogger.Msg(
                    $"[AnimMerger] Postfix: name='{name}' isSkill={isSkill} " +
                    $"result={(__result != null ? $"OK duration={__result.Animation?.Duration:F3}s" : "NULL")} " +
                    $"hasSwap={_activeSwaps.ContainsKey(__instance)}");

                if (!_activeSwaps.TryGetValue(__instance, out var ctx)) return;
                _activeSwaps.Remove(__instance);

                if (__result == null)
                {
                    // Play() returned null even on the ghost — animation was not found on
                    // its live SkeletonData. Restore immediately so player isn't stuck invisible.
                    MelonLogger.Warning(
                        $"[AnimMerger] Swap: Play() returned null for '{name}' on ghost — " +
                        $"ghost SkeletonData may differ from prefab scan. Restoring immediately.");
                    RestoreSwap(__instance, name, ctx);
                    return;
                }

                MelonLogger.Msg(
                    $"[AnimMerger] Swap: registering End callback for '{name}' " +
                    $"duration={__result.Animation?.Duration:F3}s");

                var rc = __instance;
                __result.End += (entry) =>
                {
                    try
                    {
                        // Guard: only restore if m_animation is still the ghost.
                        // Prevents double-restore if End fires more than once.
                        var current = _animField?.GetValue(rc) as SkeletonAnimation;
                        if (current != ctx.GhostSA)
                        {
                            MelonLogger.Msg(
                                $"[AnimMerger] End callback for '{name}': " +
                                $"m_animation already changed — skipping restore");
                            return;
                        }

                        RestoreSwap(rc, name, ctx);
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"[AnimMerger] End callback for '{name}' threw:\n{ex}");
                    }
                };
            }

            // ── Shared restore ────────────────────────────────────────────────────

            // ── Replace RestoreSwap entirely ──────────────────────────────────────────

            private static void RestoreSwap(RoleController rc, string animName, SwapContext ctx)
            {
                // Restore m_animation to the player's real skeleton
                _animField.SetValue(rc, ctx.OriginalSA);
                MelonLogger.Msg(
                    $"[AnimMerger] Swap restored: '{animName}' ended — " +
                    $"m_animation back to player skeleton");

                // Restore m_roleId so the player's own audio bank is used again
                _roleIdField?.SetValue(rc, ctx.OriginalRoleId);
                MelonLogger.Msg($"[AnimMerger] Restore: m_roleId → {ctx.OriginalRoleId}");

                // Detach ghost from the player's transform hierarchy.
                // SetParent(null) makes it a scene root again; we must re-apply
                // DontDestroyOnLoad because parenting to a scene object removed it.
                if (ctx.GhostRoot != null)
                {
                    ctx.GhostRoot.SetParent(null);
                    UnityEngine.Object.DontDestroyOnLoad(ctx.GhostRoot.gameObject);
                    MelonLogger.Msg("[AnimMerger] Restore: ghost unparented, DontDestroyOnLoad re-applied");
                }

                // Hide ghost, show player
                if (ctx.GhostRenderer != null) ctx.GhostRenderer.enabled = false;
                if (ctx.PlayerRenderer != null) ctx.PlayerRenderer.enabled = true;

                MelonLogger.Msg(
                    $"[AnimMerger] Restore: ghost hidden={ctx.GhostRenderer != null}, " +
                    $"player shown={ctx.PlayerRenderer != null}");
            }
        }
    }
}