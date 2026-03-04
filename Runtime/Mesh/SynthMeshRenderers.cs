using System.Collections.Generic;
using UnityEngine;

namespace Genesis.Sentience.Synth
{
    public class SynthMeshRenderers
    {
        private readonly Transform skeletonRoot;
        private readonly Transform modelRoot;
        private readonly SynthData synthData;
        private readonly List<SynthSkinData> skins;
        public SynthMeshRenderers(Transform synthTransform, Transform skeletonRoot, SynthData synthData)
        {
            this.skeletonRoot = skeletonRoot;
            modelRoot = synthTransform.FindInChildren("modelRoot");
            this.synthData = synthData;
            skins = synthData.LoadableSkins;
        }

        public void Bind()
        {
            if (modelRoot == null)
            {
                Debug.LogWarning("SynthMeshRenderers: 'modelRoot' child not found. " +
                    "Create an empty child named 'modelRoot' under the Synth root.");
                return;
            }
            for (int i = 0; i < skins.Count; i++)
            {
                var srcRenderer = skins[i].skinnedMeshRenderer;
                var mesh = new GameObject(srcRenderer.name);
                mesh.hideFlags = HideFlags.DontSave;
                var meshRenderer = mesh.AddComponent<SkinnedMeshRenderer>();
                var clonedMesh = Object.Instantiate(srcRenderer.sharedMesh);
                clonedMesh.hideFlags = HideFlags.DontSave;
                meshRenderer.sharedMesh = clonedMesh;
                Transform[] bones = srcRenderer.bones;
                var boneNames = new string[bones.Length];

                for (int j = 0; j < bones.Length; j++)
                {
                    boneNames[j] = bones[j].name;
                }

                var synthBones = new Transform[boneNames.Length];
                for (int j = 0; j < boneNames.Length; j++)
                {
                    synthBones[j] = skeletonRoot.FindInChildren(boneNames[j]);
                }
                meshRenderer.bones = synthBones;
                meshRenderer.rootBone = skeletonRoot.GetChild(0);
                meshRenderer.sharedMaterials = srcRenderer.sharedMaterials;
                mesh.transform.SetParent(modelRoot, false);
                if (!skins[i].activateOnBind)
                {
                    mesh.SetActive(false);
                }
            }
        }

        public void Unbind()
        {
            if (modelRoot == null) return;
            var meshes = modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            int meshesCount = meshes.Length;
            var isPlaying = Application.isPlaying;
            for (int i = 0; i < meshesCount; i++)
            {
                if (isPlaying)
                {
                    Object.Destroy(meshes[i].gameObject);
                }
                else
                {
                    Object.DestroyImmediate(meshes[i].gameObject, false);
                }
            }
        }
    }

}
