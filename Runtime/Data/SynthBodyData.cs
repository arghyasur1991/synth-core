using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Genesis.Sentience.Synth
{
    [Serializable]
    public class BoneMesh : PerBoneData
    {
        public bool locked = false;
        public bool showChildren = false;
        public bool isLeftEye;
        public bool isRightEye;
        public float parentMassPercent;
        public float selfMassPercent;
        public Vector3[] meshVertices;
        public List<int> vertexIndices;
        public List<int> triangles;
    }

    [CreateAssetMenu()]
    public class SynthBodyData : SynthBonesData<BoneMesh>
    {
        public float mass = 1;

        [Header("Eye Bones")]
        [Tooltip("Name of the left eye bone in the skeleton. Set by wizard or manually.")]
        public string leftEyeBone;
        [Tooltip("Name of the right eye bone in the skeleton. Set by wizard or manually.")]
        public string rightEyeBone;
        protected override void OnValidate()
        {
            base.OnValidate();
            if (mass < 1)
            {
                mass = 1;
            }
        }

        protected override BoneMesh CreatePerBoneData(List<string> parents, string bone, int boneIndex)
        {
            return new BoneMesh {
                bone = bone, parents = parents, boneIndex = boneIndex,
                vertexIndices = new(), triangles = new()
            };
        }

        public override void BindHumanModel()
        {
            boneData = new();
            if (humanModel.skinnedMeshRenderers == null ||
                humanModel.skinnedMeshRenderers.Count == 0 ||
                humanModel.bodyMeshIndex < 0 ||
                humanModel.bodyMeshIndex >= humanModel.skinnedMeshRenderers.Count)
            {
                Debug.LogWarning("SynthBodyData: SynthModel has no skinned mesh renderers yet — skipping bind.");
                return;
            }
            var skinnedMesh = humanModel.skinnedMeshRenderers[humanModel.bodyMeshIndex].skinnedMeshRenderer;
            var bones = skinnedMesh.bones;

            Mesh mesh = humanModel.skinnedMeshRenderers[humanModel.bodyMeshIndex].skinnedMeshRenderer.sharedMesh;
            int boneIndex = -1;
            foreach (var bone in bones)
            {
                boneIndex++;
                var parent = bone.parent;
                List<string> parents = new();
                while (parent != null)
                {
                    var parentName = parent.name;
                    parents.Insert(0, parentName);
                    parent = parent.parent;
                }

                var boneName = bone.name;

                var boneData = CreatePerBoneData(parents, boneName, boneIndex);
                this.boneData.Add(boneData);
            }
            var oriMesh = new Mesh();
            skinnedMesh.BakeMesh(oriMesh);
            var vertices = oriMesh.vertices;
            UpdateBoneMeshData(mesh, vertices);
            boneData.Sort();
        }

        public bool ShowOnEditor(string bone)
        {
            var boneMesh = GetBoneData(bone);
            for (int i = 0; i < boneMesh.parents.Count; i++)
            {
                var parent = GetBoneData(boneMesh.parents[i]);
                if (parent != null && !parent.showChildren)
                {
                    return false;
                }
            }
            return true;
        }

        public bool IsLocked(string bone)
        {
            var boneMesh = GetBoneData(bone);
            var siblings = GetSiblings(boneMesh);
            bool allSiblingsLocked = true;
            foreach(var sibling in siblings)
            {
                allSiblingsLocked &= sibling.locked;
            }
            return allSiblingsLocked || boneMesh.locked;
        }

        public bool IsSelfMassLocked(string bone)
        {
            var boneMesh = GetBoneData(bone);
            return GetChildCount(boneMesh) == 0;
        }

        public void AdjustSiblingMasses(string bone, float newMassPercent)
        {
            var boneMesh = GetBoneData(bone);
            float currentParentMassPercent = boneMesh.parentMassPercent;
            float newParentMassPercent = GetParentMassPercent(boneMesh, newMassPercent);
            float delta = newParentMassPercent - currentParentMassPercent;
            var siblings = GetSiblings(boneMesh);
            const float minParentMassPercent = 0.01f;
            if (siblings.Count == 0 || newParentMassPercent >= 1 || newParentMassPercent < minParentMassPercent)
            {
                return;
            }
            var unlockedSiblings = siblings.FindAll(s => !s.locked);
            float deltaDist = delta / unlockedSiblings.Count;
            foreach (var sibling in unlockedSiblings)
            {
                if ((sibling.parentMassPercent - deltaDist) < minParentMassPercent)
                {
                    deltaDist = Mathf.Min(deltaDist, sibling.parentMassPercent - minParentMassPercent);
                }
            }
            foreach (var sibling in unlockedSiblings)
            {
                sibling.parentMassPercent -= deltaDist;
            }
            boneMesh.parentMassPercent = currentParentMassPercent + deltaDist * unlockedSiblings.Count;
        }

        public float GetMass(string bone)
        {
            BoneMesh boneMesh = GetBoneData(bone);
            float mass = this.mass;
            for (int i = 0; i < boneMesh.parents.Count; i++)
            {
                var parent = GetBoneData(boneMesh.parents[i]);
                if (parent != null)
                {
                    mass *= parent.parentMassPercent;
                    mass -= mass * parent.selfMassPercent;
                }
            }
            return mass * boneMesh.parentMassPercent;
        }

        public float GetMassPercent(string bone)
        {
            return GetMass(bone) / mass * 100;
        }

        public float GetSelfMass(string bone)
        {
            BoneMesh boneMesh = GetBoneData(bone);
            float mass = GetMass(bone);
            return mass * boneMesh.selfMassPercent;
        }

        public float GetSelfMassPercent(string bone)
        {
            return GetSelfMass(bone) / mass * 100; //GetBoneData(bone).selfMassPercent * 100;
        }

        public void SetSelfMassPercent(string bone, float newValue)
        {
            float subTreeMassPercent = GetMass(bone) / mass;
            newValue = (newValue / 100) / subTreeMassPercent;
            newValue *= 100;
            BoneMesh boneMesh = GetBoneData(bone);
            if (newValue < 1)
            {
                return;
            }
            if (newValue > 99 && GetChildCount(boneMesh) > 0)
            {
                return;
            }
            if (newValue > 100)
            {
                return;
            }

            boneMesh.selfMassPercent = newValue / 100;
        }

        private float GetParentMassPercent(BoneMesh boneMesh, float massPercent)
        {
            var parent = GetParent(boneMesh);
            if (parent != null)
            {
                return massPercent / GetMassPercent(parent.bone);
            }
            return massPercent;
        }

        private int GetChildCount(BoneMesh boneMesh)
        {
            return boneData.FindAll(b => b.parents.Last() == boneMesh.bone).Count;
        }

        private void UpdateBoneMeshData(Mesh oriMesh, Vector3[] oriMeshVertices)
        {
            var triangles = oriMesh.triangles;
            BoneWeight[] boneWeights = oriMesh.boneWeights;

            for (int i = 0; i < boneWeights.Length; i++)
            {
                var boneWeight = boneWeights[i];
                var boneIndex = boneWeight.boneIndex0;
                var weight = boneWeight.weight0;

                if (weight > 0.3)
                {
                    boneData[boneIndex].vertexIndices.Add(i);
                }
            }

            for (int i = 0; i < triangles.Length / 3; i++)
            {
                int vert1 = triangles[3 * i];
                int vert2 = triangles[3 * i + 1];
                int vert3 = triangles[3 * i + 2];

                int bone1 = boneWeights[vert1].boneIndex0;
                int bone2 = boneWeights[vert2].boneIndex0;
                int bone3 = boneWeights[vert3].boneIndex0;

                if (bone1 != bone2 || bone1 != bone3)
                {
                    continue;
                }

                BoneMesh boneMesh = boneData[bone1];
                int vert1Index = boneMesh.vertexIndices.IndexOf(vert1);
                int vert2Index = boneMesh.vertexIndices.IndexOf(vert2);
                int vert3Index = boneMesh.vertexIndices.IndexOf(vert3);

                if (vert1Index >= 0 && vert2Index >= 0 && vert3Index >= 0)
                {
                    boneMesh.triangles.Add(vert1Index);
                    boneMesh.triangles.Add(vert2Index);
                    boneMesh.triangles.Add(vert3Index);
                }
            }

            for (int i = 0; i < boneData.Count; i++)
            {
                int newVerticesCount = boneData[i].vertexIndices.Count;
                if (newVerticesCount > 10)
                {
                    var newMeshVertices = new Vector3[newVerticesCount];
                    for (int j = 0; j < newVerticesCount; j++)
                    {
                        newMeshVertices[j] = oriMeshVertices[boneData[i].vertexIndices[j]];
                    }
                    boneData[i].meshVertices = newMeshVertices;
                    boneData[i].selfMassPercent = GetChildCount(boneData[i]) > 0? 0.1f: 1;
                }
                boneData[i].parentMassPercent = 1.0f / (1 + GetSiblings(boneData[i]).Count);
            }
        }

        /// <summary>
        /// Name of the left eye bone. Prefers the explicit field; falls back to
        /// the legacy isLeftEye flag on BoneMesh for backward compatibility.
        /// </summary>
        public string LeftEyeBoneName =>
            !string.IsNullOrEmpty(leftEyeBone) ? leftEyeBone
            : boneData?.Find(b => b.isLeftEye)?.bone;

        /// <summary>
        /// Name of the right eye bone. Prefers the explicit field; falls back to
        /// the legacy isRightEye flag on BoneMesh for backward compatibility.
        /// </summary>
        public string RightEyeBoneName =>
            !string.IsNullOrEmpty(rightEyeBone) ? rightEyeBone
            : boneData?.Find(b => b.isRightEye)?.bone;

        public override void NotifyOnUpdate()
        {
            base.NotifyOnUpdate();
        }
    }
}
