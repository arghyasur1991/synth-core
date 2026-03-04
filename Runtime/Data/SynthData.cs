using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Genesis.Sentience.Synth
{
    [CreateAssetMenu()]
    public class SynthData: ScriptableObject
    {
        [SerializeField] private SynthModel humanModel;
        [SerializeField] private BoneJointsData boneJointsData;
        [SerializeField] private SynthBodyData synthBodyData;

        public string ModelName
        {
            get
            {
                return humanModel.humanModel.name;
            }
        }

        public string BodyMeshName
        {
            get
            {
                return humanModel.skinnedMeshRenderers[humanModel.bodyMeshIndex].skinnedMeshRenderer.sharedMesh.name;
            }
        }

        public string SkeletonRootName
        {
            get
            {
                var root = humanModel.humanModel.transform;
                var anim = humanModel.humanModel.GetComponent<Animator>();

                var adapter = HumanoidAdapter.Create(SkeletonType.Auto, root);
                return adapter.GetSkeletonRootName(root, anim);
            }
        }

        public SkinnedMeshRenderer[] MeshRenderers
        {
            get
            {
                var meshRenderers = humanModel.skinnedMeshRenderers;
                var meshRenderersForLoad = meshRenderers.FindAll(r => r.load);
                return meshRenderersForLoad.Select(m => m.skinnedMeshRenderer).ToArray();
            }
        }

        public List<SynthSkinData> LoadableSkins
        {
            get
            {
                return humanModel.skinnedMeshRenderers.FindAll(r => r.load);
            }
        }

        public List<BoneJoint> BoneJoints
        {
            get
            {
                return boneJointsData.boneData;
            }
        }

        public List<BoneMesh> BoneMeshes
        {
            get
            {
                return synthBodyData.boneData;
            }
        }

        public float GetMass(string bone)
        {
            return synthBodyData.GetSelfMass(bone);
        }

        /// <summary>Name of the bone flagged as left eye in SynthBodyData, or null.</summary>
        public string LeftEyeBoneName => synthBodyData != null ? synthBodyData.LeftEyeBoneName : null;

        /// <summary>Name of the bone flagged as right eye in SynthBodyData, or null.</summary>
        public string RightEyeBoneName => synthBodyData != null ? synthBodyData.RightEyeBoneName : null;

        private void OnValidate()
        {
            SyncModelReferences();
        }

        /// <summary>
        /// Propagates the humanModel reference to sub-assets without triggering bind.
        /// Binding is done explicitly via inspector buttons or the wizard.
        /// </summary>
        private void SyncModelReferences()
        {
            if (humanModel == null) return;
            if (boneJointsData != null && boneJointsData.humanModel != humanModel)
                boneJointsData.humanModel = humanModel;
            if (synthBodyData != null && synthBodyData.humanModel != humanModel)
                synthBodyData.humanModel = humanModel;
        }
    }
}
