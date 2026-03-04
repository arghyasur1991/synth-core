using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Genesis.Sentience.Synth
{
    [Serializable]
    public class PerBoneData : IComparable<PerBoneData>
    {
        public List<string> parents;
        public string bone;
        public int boneIndex;

        public int CompareTo(PerBoneData other)
        {
            int diffParentIndex = FirstDifferentParentIndex(other);
            var nextLevelBoneSelf = (diffParentIndex == parents.Count) ? bone : parents[diffParentIndex];
            var nextLevelBoneOther = (diffParentIndex == other.parents.Count) ? other.bone : other.parents[diffParentIndex];

            if (nextLevelBoneSelf == nextLevelBoneOther)
            {
                return parents.Count.CompareTo(other.parents.Count);
            }
            return nextLevelBoneSelf.CompareTo(nextLevelBoneOther);
        }

        private int FirstDifferentParentIndex(PerBoneData other)
        {
            if (parents.Count <= other.parents.Count)
            {
                for (int i = 0; i < parents.Count; i++)
                {
                    if (parents[i] != other.parents[i])
                    {
                        return i;
                    }
                }
                return parents.Count;
            }
            for (int i = 0; i < other.parents.Count; i++)
            {
                if (parents[i] != other.parents[i])
                {
                    return i;
                }
            }
            return other.parents.Count;
        }
    }

    public abstract class SynthBonesData<T>: UpdatableData where T: PerBoneData
    {
        public SynthModel humanModel;
        [BoneJoint] public List<T> boneData;

        protected override void OnValidate()
        {
            if (humanModel == null)
            {
                UnbindModel();
            }
            base.OnValidate();
        }

        public List<T> GetSiblings(T item)
        {
            return boneData.FindAll(m => m.parents.Last() == item.parents.Last() && m != item);
        }

        public T GetParent(T item)
        {
            return boneData.Find(m => m.bone == item.parents.Last());
        }

        public T GetBoneData(string bone)
        {
            return boneData.Find(m => m.bone == bone);
        }

        public virtual void BindHumanModel()
        {
            boneData = new();
            if (humanModel.skinnedMeshRenderers == null ||
                humanModel.skinnedMeshRenderers.Count == 0 ||
                humanModel.bodyMeshIndex < 0 ||
                humanModel.bodyMeshIndex >= humanModel.skinnedMeshRenderers.Count)
            {
                Debug.LogWarning("SynthBonesData: SynthModel has no skinned mesh renderers yet — skipping bind.");
                return;
            }
            var bones = humanModel.skinnedMeshRenderers[humanModel.bodyMeshIndex].skinnedMeshRenderer.bones;

            // Build a mapper from the model's Animator for generic bone discovery
            SynthBoneMapper mapper = null;
            var animator = humanModel.humanModel != null
                ? humanModel.humanModel.GetComponent<Animator>()
                : null;
            if (animator != null)
                mapper = SynthBoneMapper.Create(animator);

            int boneIndex = -1;
            foreach (var bone in bones)
            {
                boneIndex++;

                // Skip root bone (Hips/pelvis)
                if (mapper != null)
                {
                    if (mapper.IsRoot(bone)) continue;
                }
                else if (bone.name == "hip") continue;

                // Walk parent chain; skip bones whose parents are chain-stop (Head, Hand, Foot)
                var parent = bone.parent;
                bool underChainStop = false;
                List<string> parents = new();
                while (parent != null)
                {
                    string parentName = mapper != null
                        ? mapper.GetCanonicalName(parent) ?? parent.name
                        : StripSidePrefix(parent.name);
                    parents.Insert(0, parentName);

                    bool isChainStop = mapper != null
                        ? mapper.IsChainStop(parent)
                        : (parent.name == "head" || parent.name.Contains("Hand") || parent.name.Contains("Foot"));
                    if (isChainStop)
                    {
                        underChainStop = true;
                        break;
                    }
                    parent = parent.parent;
                }
                if (underChainStop) continue;

                // Skip right-side bones (data is stored once, applied symmetrically)
                if (mapper != null)
                {
                    if (mapper.IsRightSide(bone)) continue;
                }
                else if (bone.name.Length > 1 && bone.name.StartsWith("r") && char.IsUpper(bone.name[1]))
                    continue;

                // Canonical (side-neutral) name
                string boneName = mapper != null
                    ? mapper.GetCanonicalName(bone) ?? bone.name
                    : StripSidePrefix(bone.name);

                var data = CreatePerBoneData(parents, boneName, boneIndex);
                this.boneData.Add(data);
            }
            boneData.Sort();
        }

        /// <summary>
        /// Legacy side-prefix stripping for when no SynthBoneMapper is available.
        /// Handles Daz naming (lBone → Bone).
        /// </summary>
        private static string StripSidePrefix(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 2) return name;
            if ((name[0] == 'l' || name[0] == 'r') && char.IsUpper(name[1]))
                return name.Substring(1);
            return name;
        }

        private void UnbindModel()
        {
            boneData = null;
        }

        protected abstract T CreatePerBoneData(List<string> parents, string bone, int boneIndex);
    }
}
