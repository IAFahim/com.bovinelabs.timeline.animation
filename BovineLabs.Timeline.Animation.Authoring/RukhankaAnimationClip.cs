using BovineLabs.Timeline.Authoring;
using Rukhanka;
using Rukhanka.Hybrid;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Animation.Authoring
{
    public class RukhankaAnimationClip : DOTSClip, ITimelineClipAsset
    {
        [Tooltip("The animation clip to play when this timeline clip is active.")]
        public AnimationClip animationClipHolder;

        [Header("Clip Transform Offsets")]
        public Vector3 positionOffset = Vector3.zero;
        public Vector3 eulerAnglesOffset = Vector3.zero;
        
        [Space][Tooltip("Removes the starting offset of the animation so it begins exactly at the track's offset.")]
        public bool removeStartOffset = true;
        public bool applyFootIK = true;

        public override double duration => animationClipHolder != null ? animationClipHolder.length : base.duration;
        public ClipCaps clipCaps => ClipCaps.All;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            if (animationClipHolder != null)
            {
                Avatar avatar = null;
                var rigDef = context.Director.ResolveRigDefinition(context.Track);
                if (rigDef != null) avatar = rigDef.GetAvatar();

                context.Baker.AddComponent(clipEntity, new RukhankaSingleClipData
                {
                    ClipHash = BakingUtils.ComputeAnimationHash(animationClipHolder, avatar),
                    ClipIn = (float)context.Clip.clipIn,
                    TimeScale = (float)context.Clip.timeScale,
                    PreExtrapolation = context.Clip.preExtrapolationMode,
                    PostExtrapolation = context.Clip.postExtrapolationMode,
                    
                    PositionOffset = positionOffset,
                    RotationOffset = Quaternion.Euler(eulerAnglesOffset),
                    RemoveStartOffset = removeStartOffset,
                    ApplyFootIK = applyFootIK
                });
            }

            base.Bake(clipEntity, context);
        }
    }
}