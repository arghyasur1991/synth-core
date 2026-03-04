using System.Collections.Generic;
using UnityEngine;

namespace Genesis.Sentience.Synth
{
    public class SynthSkinAttribute : PropertyAttribute
    {

    }

    [System.Serializable]
    public struct SynthSkinData
    {
        public SkinnedMeshRenderer skinnedMeshRenderer;
        public bool load;
        public bool activateOnBind;

        public SynthSkinData(SkinnedMeshRenderer renderer, bool l, bool a)
        {
            skinnedMeshRenderer = renderer;
            load = l;
            activateOnBind = a;
        }
    }

    [CreateAssetMenu()]
    public class SynthModel : UpdatableData
    {
        public GameObject humanModel;

        [SynthSkin] public List<SynthSkinData> skinnedMeshRenderers;
        public int bodyMeshIndex;


        protected override void OnValidate()
        {
            if (humanModel == null)
            {
                UnbindModel();
                bodyMeshIndex = 0;
            }
            base.OnValidate();
        }

        public void BindHumanModel()
        {
            var renderers = humanModel.GetComponentsInChildren<SkinnedMeshRenderer>();

            if (skinnedMeshRenderers == null || skinnedMeshRenderers.Count == 0)
            {
                skinnedMeshRenderers = new List<SynthSkinData>();
                foreach (var renderer in renderers)
                {
                    skinnedMeshRenderers.Add(new SynthSkinData(renderer, true, true));
                }
            }
        }

        private void UnbindModel()
        {
            skinnedMeshRenderers = null;
        }
    }
}