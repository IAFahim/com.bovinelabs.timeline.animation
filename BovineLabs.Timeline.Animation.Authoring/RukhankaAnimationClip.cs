using BovineLabs.Timeline.Authoring;
using Rukhanka.Hybrid;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Animation.Authoring
{
    public class RukhankaAnimationClip : DOTSClip, ITimelineClipAsset
    {
        public AnimationClip animationClipHolder;

        public override double duration => animationClipHolder != null ? animationClipHolder.length : base.duration;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.ClipIn | ClipCaps.SpeedMultiplier | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            if (animationClipHolder != null)
            {
                Avatar avatar = null;
                var rigDef = context.Director.ResolveRigDefinition(context.Track);
                if (rigDef != null) avatar = rigDef.GetAvatar();

                context.Baker.AddComponent(clipEntity, new RukhankaSingleClipData
                {
                    ClipHash = BakingUtils.ComputeAnimationHash(animationClipHolder, avatar)
                });
            }

            base.Bake(clipEntity, context);
        }
    }
}