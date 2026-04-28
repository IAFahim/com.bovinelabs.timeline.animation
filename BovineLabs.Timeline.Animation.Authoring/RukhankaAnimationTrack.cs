using System;
using System.ComponentModel;
using System.Linq;
using BovineLabs.Timeline.Authoring;
using Rukhanka;
using Rukhanka.Hybrid;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Animation.Authoring
{
    [Serializable]
    [TrackClipType(typeof(RukhankaAnimationClip))]
    [TrackColor(0.16f, 0.54f, 0.88f)]
    [TrackBindingType(typeof(RigDefinitionAuthoring))]
    [DisplayName("BovineLabs/Animation/Rukhanka Clip")]
    public class RukhankaAnimationTrack : DOTSTrack
    {
        [Tooltip(
            "Layer index for multi-track blending. 0 = base layer, 1+ = additive/override layers. Multiple tracks on the same rig can have different layer indices.")]
        public int LayerIndex;

        protected override void Bake(BakingContext context)
        {
            var rigDef = context.Director.ResolveRigDefinition(this);
            if (rigDef == null)
            {
                Debug.LogWarning($"[RukhankaAnimationTrack] '{name}' has no RigDefinitionAuthoring binding — animation data will not be baked.");
                base.Bake(context);
                return;
            }

            context.Baker.AddComponent(context.TrackEntity, new RukhankaSingleTrackData
            {
                LayerIndex = LayerIndex
            });

            var clipsToBake = GetClips()
                .Select(c => c.asset as RukhankaAnimationClip)
                .Where(h => h?.animationClipHolder != null)
                .Select(h => h.animationClipHolder)
                .ToHashSet();

            if (clipsToBake.Count > 0)
            {
                var bakedAnimations = new AnimationClipBaker().BakeAnimations(
                    context.Baker, clipsToBake.ToArray(), rigDef.GetAvatar(), rigDef.gameObject);

                var e = context.Baker.CreateAdditionalEntity(TransformUsageFlags.None, false,
                    name + "_AnimationAssets");
                var buffer = context.Baker.AddBuffer<NewBlobAssetDatabaseRecord<AnimationClipBlob>>(e);
                buffer.AddValidAnimations(bakedAnimations);

                if (bakedAnimations.IsCreated) bakedAnimations.Dispose();
            }

            base.Bake(context);
        }
    }
}