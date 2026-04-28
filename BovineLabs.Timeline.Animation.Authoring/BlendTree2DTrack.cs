using System;
using System.Collections.Generic;
using System.ComponentModel;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Core.PropertyDrawers;
using Rukhanka;
using Rukhanka.Hybrid;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Animation.Authoring
{
    [Serializable]
    [TrackClipType(typeof(BlendTree2DClip))]
    [TrackColor(0.20f, 0.70f, 0.85f)]
    [TrackBindingType(typeof(RigDefinitionAuthoring))]
    [DisplayName("BovineLabs/Animation/Blend Tree 2D")]
    public class BlendTree2DTrack : DOTSTrack
    {
        [Tooltip(
            "Blend tree algorithm: SimpleDirectional for 1D-like with a center, FreeformCartesian for 2D positions, FreeformDirectional for 2D with polar handling.")]
        public MotionBlob.Type BlendTreeType = MotionBlob.Type.BlendTree2DSimpleDirectional;

        [Tooltip(
            "Layer index for multi-track blending. 0 = base layer, 1+ = additive/override layers. Multiple tracks on the same rig can have different layer indices.")]
        public int LayerIndex;

        [Header("Exit / Fallback Override (Optional)")]
        [Tooltip(
            "Animation clip to play as fallback when no timeline clips are active on this track's target. Overrides the default fallback set on TimelineAnimationStateAuthoring. Highest layer index wins when multiple tracks specify overrides.")]
        public AnimationClip ExitIdleClip;

        [Tooltip("Time in seconds to blend into this fallback clip.")] [Min(0.001f)]
        public float BlendInDuration = 0.25f;

        [Tooltip("Time in seconds to blend out of this fallback clip.")] [Min(0.001f)]
        public float BlendOutDuration = 0.25f;

        [Tooltip(
            "How the fallback animation wraps. Loop = restart from beginning, Clamp = freeze at last frame, Hold = always show last frame.")]
        public FallbackPlaybackMode FallbackPlaybackMode = FallbackPlaybackMode.Loop;

        [Tooltip(
            "Motion entries that define the blend tree. Each entry maps an animation clip to a 2D direction/position.")]
        public List<BlendTree2DMotionEntry> Motions = new();

        private void OnValidate()
        {
            var changed = false;
            foreach (var motion in Motions)
            {
                var prev = motion.directionCalc;
                motion.CalcDirection();
                if (prev != motion.directionCalc) changed = true;
            }
#if UNITY_EDITOR
            if (changed) UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        protected override void Bake(BakingContext context)
        {
            var director = context.Director;
            var binding = director.GetGenericBinding(this);
            var rigDef = binding as RigDefinitionAuthoring ??
                         (binding as GameObject)?.GetComponent<RigDefinitionAuthoring>();

            if (rigDef == null)
            {
                Debug.LogWarning($"[BlendTree2DTrack] '{name}' has no RigDefinitionAuthoring binding — animation data will not be baked.");
                base.Bake(context);
                return;
            }

            var baker = context.Baker;
            var trackEntity = context.TrackEntity;
            var avatar = rigDef.GetAvatar();

            baker.AddComponent(trackEntity,
                new BlendAnimationTree2DTrackData { BlendTreeType = BlendTreeType, LayerIndex = LayerIndex });

            var motionBuffer = baker.AddBuffer<BlendTree2DMotionData>(trackEntity);
            var clipsToBake = new List<AnimationClip>();
            var index = 0;

            foreach (var motion in Motions)
            {
                if (motion.clip == null) continue;
                motion.CalcDirection();
                motionBuffer.Add(new BlendTree2DMotionData
                {
                    AnimationHash = BakingUtils.ComputeAnimationHash(motion.clip, avatar),
                    BlendTree2DMotionElement = new ScriptedAnimator.BlendTree2DMotionElement
                        { pos = motion.directionCalc, motionIndex = index++ }
                });
                clipsToBake.Add(motion.clip);
            }

            if (ExitIdleClip != null)
            {
                baker.AddComponent(trackEntity, new TrackFallbackOverride
                {
                    FallbackClipHash = BakingUtils.ComputeAnimationHash(ExitIdleClip, avatar),
                    BlendInSpeed = 1f / Mathf.Max(0.001f, BlendInDuration),
                    BlendOutSpeed = 1f / Mathf.Max(0.001f, BlendOutDuration),
                    PlaybackMode = FallbackPlaybackMode,
                    LayerIndex = LayerIndex,
                    BlendMode = AnimationBlendingMode.Override,
                    AvatarMaskHash = default
                });
                clipsToBake.Add(ExitIdleClip);
            }

            if (clipsToBake.Count > 0)
            {
                var bakedAnimations =
                    new AnimationClipBaker().BakeAnimations(baker, clipsToBake.ToArray(), avatar, rigDef.gameObject);
                var e = baker.CreateAdditionalEntity(TransformUsageFlags.None, false, name + "_BlendTreeAssets");
                var dbBuffer = baker.AddBuffer<NewBlobAssetDatabaseRecord<AnimationClipBlob>>(e);
                dbBuffer.AddValidAnimations(bakedAnimations);

                if (bakedAnimations.IsCreated) bakedAnimations.Dispose();
            }

            base.Bake(context);
        }

        [Serializable]
        public class BlendTree2DMotionEntry
        {
            [Tooltip("Animation clip for this motion entry.")]
            public AnimationClip clip;

            [Tooltip("Direction angle in degrees. 0 = forward, 90 = right, -90 = left, 180 = backward.")]
            [Range(-180, 180)]
            public float degreeCalc;

            [Tooltip("Distance from origin in the blend space. Controls how far this motion extends.")]
            public float rangeCalc = 1;

            [Tooltip("Computed direction vector (auto-calculated from degree and range).")]
            [InspectorReadOnly]
            public Vector2 directionCalc;

            internal Vector2 CalcDirection()
            {
                var radians = degreeCalc * Mathf.Deg2Rad;
                directionCalc = new Vector2(Mathf.Sin(radians) * rangeCalc, Mathf.Cos(radians) * rangeCalc);
                return directionCalc;
            }
        }
    }
}