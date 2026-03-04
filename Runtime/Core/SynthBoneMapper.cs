using System;
using System.Collections.Generic;
using UnityEngine;

namespace Genesis.Sentience.Synth
{
    /// <summary>
    /// Discovers humanoid bones on a model using Unity's Avatar API first,
    /// then falls back to name-pattern matching for extended bones.
    /// Provides canonical naming, side detection, and mirror lookups.
    /// </summary>
    public class SynthBoneMapper
    {
        private readonly Dictionary<SynthBone, Transform> _boneToTransform = new();
        private readonly Dictionary<Transform, SynthBone> _transformToBone = new();
        private readonly HashSet<Transform> _allDiscovered = new();

        /// <summary>
        /// The skeleton adapter detected (or overridden) for this model.
        /// Centralizes all skeleton-convention-specific behavior.
        /// </summary>
        public HumanoidAdapter Adapter { get; private set; }

        /// <summary>All bones discovered on this model (SynthBone → Transform).</summary>
        public IReadOnlyDictionary<SynthBone, Transform> BoneMap => _boneToTransform;

        /// <summary>Number of bones discovered.</summary>
        public int DiscoveredCount => _boneToTransform.Count;

        /// <summary>
        /// Create a mapper from an Animator with a Humanoid Avatar.
        /// Uses Avatar.GetBoneTransform for all bones with a Unity mapping,
        /// then scans the hierarchy for extended bones via name patterns.
        /// Returns null if the Animator has no valid Humanoid Avatar.
        /// </summary>
        public static SynthBoneMapper Create(Animator animator)
        {
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                Debug.LogWarning("SynthBoneMapper: Animator has no valid Humanoid Avatar, " +
                                 "falling back to name-pattern discovery only.");
                return CreateFromNamePatterns(animator != null ? animator.transform : null);
            }

            var mapper = new SynthBoneMapper();

            // Phase 1: Avatar-based discovery for standard bones
            foreach (var kvp in SynthBoneCatalog.All)
            {
                var synthBone = kvp.Key;
                var info = kvp.Value;

                if (info.UnityMapping.HasValue)
                {
                    var t = animator.GetBoneTransform(info.UnityMapping.Value);
                    if (t != null)
                    {
                        mapper.Register(synthBone, t);
                    }
                }
            }

            // Phase 2: Name-pattern fallback for extended bones (not in Avatar)
            var root = animator.transform;
            var allTransforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var kvp in SynthBoneCatalog.All)
            {
                var synthBone = kvp.Key;
                var info = kvp.Value;

                if (mapper._boneToTransform.ContainsKey(synthBone))
                    continue; // Already found via Avatar

                if (info.FallbackNamePatterns == null)
                    continue;

                foreach (var t in allTransforms)
                {
                    if (mapper._transformToBone.ContainsKey(t))
                        continue; // Already mapped to something else

                    foreach (var pattern in info.FallbackNamePatterns)
                    {
                        if (t.name.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
                            t.name.EndsWith(":" + pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            mapper.Register(synthBone, t);
                            goto nextBone;
                        }
                    }
                }
                nextBone:;
            }

            mapper.Adapter = HumanoidAdapter.Create(SkeletonType.Auto, animator.transform);
            Debug.Log($"SynthBoneMapper: Discovered {mapper.DiscoveredCount} bones " +
                      $"(Avatar: {CountAvatarMapped(mapper)}, pattern: {mapper.DiscoveredCount - CountAvatarMapped(mapper)}, " +
                      $"skeleton: {mapper.Adapter.Type})");

            return mapper;
        }

        /// <summary>Fallback: discover all bones via name patterns only (no Avatar).</summary>
        private static SynthBoneMapper CreateFromNamePatterns(Transform root)
        {
            var mapper = new SynthBoneMapper();
            if (root == null) return mapper;

            var allTransforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var kvp in SynthBoneCatalog.All)
            {
                var synthBone = kvp.Key;
                var info = kvp.Value;
                if (info.FallbackNamePatterns == null) continue;

                foreach (var t in allTransforms)
                {
                    if (mapper._transformToBone.ContainsKey(t)) continue;
                    foreach (var pattern in info.FallbackNamePatterns)
                    {
                        if (t.name.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
                            t.name.EndsWith(":" + pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            mapper.Register(synthBone, t);
                            goto nextBone;
                        }
                    }
                }
                nextBone:;
            }
            mapper.Adapter = HumanoidAdapter.Create(SkeletonType.Auto, root);
            return mapper;
        }

        private static int CountAvatarMapped(SynthBoneMapper mapper)
        {
            int count = 0;
            foreach (var kvp in mapper._boneToTransform)
            {
                var info = SynthBoneCatalog.Get(kvp.Key);
                if (info.UnityMapping.HasValue) count++;
            }
            return count;
        }

        /// <summary>
        /// Override the auto-detected skeleton type. Used by the wizard when
        /// the user manually selects a skeleton type from the dropdown.
        /// </summary>
        public void SetAdapter(SkeletonType type)
        {
            Adapter = HumanoidAdapter.Create(type);
        }

        private void Register(SynthBone bone, Transform t)
        {
            _boneToTransform[bone] = t;
            _transformToBone[t] = bone;
            _allDiscovered.Add(t);
        }

        // --- Query methods ---

        /// <summary>Get the Transform for a SynthBone, or null if not found.</summary>
        public Transform GetTransform(SynthBone bone)
        {
            return _boneToTransform.TryGetValue(bone, out var t) ? t : null;
        }

        /// <summary>Get the SynthBone for a Transform, or SynthBone.Unknown.</summary>
        public SynthBone GetSynthBone(Transform t)
        {
            return t != null && _transformToBone.TryGetValue(t, out var bone) ? bone : SynthBone.Unknown;
        }

        /// <summary>True if this Transform is a recognized SynthBone.</summary>
        public bool IsRecognized(Transform t)
        {
            return t != null && _transformToBone.ContainsKey(t);
        }

        /// <summary>True if this Transform is the root/hips bone.</summary>
        public bool IsRoot(Transform t)
        {
            return GetSynthBone(t) == SynthBone.Hips;
        }

        /// <summary>
        /// True if this Transform is a chain-stop bone (Head, Hand, Foot).
        /// Children of chain-stop bones (fingers, face detail) are excluded from physics.
        /// </summary>
        public bool IsChainStop(Transform t)
        {
            var bone = GetSynthBone(t);
            if (bone == SynthBone.Unknown) return false;
            return SynthBoneCatalog.Get(bone).IsChainStop;
        }

        /// <summary>True if this bone is on the right side (for deduplication and mirror logic).</summary>
        public bool IsRightSide(Transform t)
        {
            var bone = GetSynthBone(t);
            if (bone != SynthBone.Unknown)
                return SynthBoneCatalog.Get(bone).Side == BoneSide.Right;

            // Unrecognized bone: determine side from nearest recognized ancestor
            return GetInheritedSide(t) == BoneSide.Right;
        }

        /// <summary>True if this bone is on the left side.</summary>
        public bool IsLeftSide(Transform t)
        {
            var bone = GetSynthBone(t);
            if (bone != SynthBone.Unknown)
                return SynthBoneCatalog.Get(bone).Side == BoneSide.Left;

            return GetInheritedSide(t) == BoneSide.Left;
        }

        /// <summary>
        /// Get the side-neutral canonical name for a bone.
        /// For recognized SynthBones: uses catalog (e.g., LeftUpperArm → "UpperArm").
        /// For unrecognized bones: strips common left/right prefixes/suffixes.
        /// </summary>
        public string GetCanonicalName(Transform t)
        {
            if (t == null) return null;

            var bone = GetSynthBone(t);
            if (bone != SynthBone.Unknown)
                return SynthBoneCatalog.GetCanonicalName(bone);

            // Unrecognized: strip side prefix from raw name
            return StripSidePrefix(t.name);
        }

        /// <summary>Get the mirror bone Transform (left↔right), or null.</summary>
        public Transform GetMirror(Transform t)
        {
            var bone = GetSynthBone(t);
            if (bone == SynthBone.Unknown) return null;
            var info = SynthBoneCatalog.Get(bone);
            if (!info.MirrorBone.HasValue) return null;
            return GetTransform(info.MirrorBone.Value);
        }

        /// <summary>
        /// Returns true if this bone's joint range should be flipped for the right side.
        /// Right-side bones with asymmetric joint ranges need negation+swap to mirror left-side data.
        /// </summary>
        public bool NeedsJointRangeFlip(Transform t)
        {
            return IsRightSide(t);
        }

        /// <summary>
        /// Walk up the hierarchy to find the nearest recognized ancestor's side.
        /// Used for unrecognized bones (twist, auxiliary) to determine their laterality.
        /// </summary>
        private BoneSide GetInheritedSide(Transform t)
        {
            var p = t.parent;
            while (p != null)
            {
                var bone = GetSynthBone(p);
                if (bone != SynthBone.Unknown)
                    return SynthBoneCatalog.Get(bone).Side;
                p = p.parent;
            }
            return BoneSide.Center;
        }

        /// <summary>
        /// Strip common left/right naming patterns to produce a side-neutral name.
        /// Handles Daz (lBone/rBone), Mixamo (mixamorig:LeftBone), Unreal (bone_l),
        /// and generic (Left_bone, Right_bone) conventions.
        /// </summary>
        private static string StripSidePrefix(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            // Strip namespace prefix (e.g., "mixamorig:" or "Armature:")
            int colonIdx = name.LastIndexOf(':');
            string stripped = colonIdx >= 0 ? name.Substring(colonIdx + 1) : name;

            // Daz convention: lBone / rBone (lowercase l/r followed by uppercase)
            if (stripped.Length > 1 &&
                (stripped[0] == 'l' || stripped[0] == 'r') &&
                char.IsUpper(stripped[1]))
            {
                return stripped.Substring(1);
            }

            // "Left"/"Right" prefix (case-insensitive)
            if (stripped.StartsWith("Left", StringComparison.OrdinalIgnoreCase))
                return stripped.Substring(4).TrimStart('_');
            if (stripped.StartsWith("Right", StringComparison.OrdinalIgnoreCase))
                return stripped.Substring(5).TrimStart('_');

            // "_l" / "_r" / "_L" / "_R" suffix
            if (stripped.EndsWith("_l", StringComparison.OrdinalIgnoreCase) ||
                stripped.EndsWith("_r", StringComparison.OrdinalIgnoreCase))
            {
                return stripped.Substring(0, stripped.Length - 2);
            }

            // ".l" / ".r" / ".L" / ".R" suffix (Blender convention)
            if (stripped.EndsWith(".l", StringComparison.OrdinalIgnoreCase) ||
                stripped.EndsWith(".r", StringComparison.OrdinalIgnoreCase))
            {
                return stripped.Substring(0, stripped.Length - 2);
            }

            return stripped;
        }
    }
}
