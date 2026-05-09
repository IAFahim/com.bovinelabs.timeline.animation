using BovineLabs.Timeline.Authoring;
using Rukhanka;
using Rukhanka.Hybrid;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
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

#if UNITY_EDITOR
        /// <summary>
        /// In edit mode, return a native AnimationClipPlayable so Unity's PlayableGraph
        /// can scrub the animation in the Timeline editor window.
        /// </summary>
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            if (!Application.isPlaying && animationClipHolder != null)
            {
                var playable = AnimationClipPlayable.Create(graph, animationClipHolder);
                playable.SetApplyFootIK(applyFootIK);
                // Note: SetRemoveStartOffset is internal — not available in custom assemblies.
                // The clip will play with its baked-in start offset in editor preview.
                return playable;
            }

            return base.CreatePlayable(graph, owner);
        }
#endif

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
