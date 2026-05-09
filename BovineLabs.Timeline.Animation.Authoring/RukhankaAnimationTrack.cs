using System;
using System.ComponentModel;
using System.Linq;
using BovineLabs.Timeline.Authoring;
using Rukhanka;
using Rukhanka.Hybrid;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Hash128 = Unity.Entities.Hash128;

namespace BovineLabs.Timeline.Animation.Authoring
{
    [Serializable]
    [TrackClipType(typeof(RukhankaAnimationClip))]
    [TrackBindingType(typeof(Animator))]
    [DisplayName("BovineLabs/Animation/Rukhanka Clip")]
    public class RukhankaAnimationTrack : DOTSTrack
    {
        [Tooltip("Layer index for multi-track blending. 0 = base layer, 1+ = additive/override layers.")]
        public int LayerIndex;

        [Header("Track Offsets")]
        [Tooltip(
            "How track offsets are applied. In DOTS, ApplyTransformOffsets is the standard deterministic approach.")]
        public TrackOffset trackOffset = TrackOffset.ApplyTransformOffsets;

        public Vector3 positionOffset = Vector3.zero;
        public Vector3 eulerAnglesOffset = Vector3.zero;

        [Header("Avatar Mask")] public AvatarMask avatarMask;
        public bool applyAvatarMask = true;

#if UNITY_EDITOR
        /// <summary>
        /// In edit mode, create a native AnimationMixerPlayable so Unity's Timeline
        /// editor can scrub animations via the PlayableGraph.
        /// Track offsets are not applied in preview (AnimationOffsetPlayable is internal).
        /// </summary>
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            if (!Application.isPlaying)
            {
                return AnimationMixerPlayable.Create(graph, inputCount);
            }

            return base.CreateTrackMixer(graph, go, inputCount);
        }
#endif

        protected override void Bake(BakingContext context)
        {
            var rigDef = context.Director.ResolveRigDefinition(this);
            if (rigDef == null)
            {
                Debug.LogWarning(
                    $"[RukhankaAnimationTrack] '{name}' has no RigDefinitionAuthoring binding — animation data will not be baked.");
                base.Bake(context);
                return;
            }

            var baker = context.Baker;
            var trackEntity = context.TrackEntity;

            // Handle Avatar Mask Baking
            Hash128 avatarMaskHash = default;
            if (applyAvatarMask && avatarMask != null)
            {
                var maskBaker = new AvatarMaskBaker();
                var maskBlob = maskBaker.CreateAvatarMaskBlob(baker, avatarMask, rigDef);
                avatarMaskHash = maskBlob.Value.hash;

                var maskData = new AvatarMaskBakingData
                {
                    rigEntity = trackEntity,
                    dataBlob = maskBlob
                };
                baker.AddBuffer<AvatarMaskBakingData>(trackEntity).Add(maskData);
            }

            // Bake Track Data
            baker.AddComponent(trackEntity, new RukhankaSingleTrackData
            {
                LayerIndex = LayerIndex,
                TrackPositionOffset = trackOffset == TrackOffset.ApplyTransformOffsets ? positionOffset : Vector3.zero,
                TrackRotationOffset = trackOffset == TrackOffset.ApplyTransformOffsets
                    ? Quaternion.Euler(eulerAnglesOffset)
                    : Quaternion.identity,
                ApplyAvatarMask = applyAvatarMask,
                AvatarMaskHash = avatarMaskHash
            });

            // Bake clips
            var clipsToBake = GetClips()
                .Select(c => c.asset as RukhankaAnimationClip)
                .Where(h => h?.animationClipHolder != null)
                .Select(h => h.animationClipHolder)
                .ToHashSet();

            if (clipsToBake.Count > 0)
            {
                var bakedAnimations = new AnimationClipBaker().BakeAnimations(
                    baker, clipsToBake.ToArray(), rigDef.GetAvatar(), rigDef.gameObject);

                var e = baker.CreateAdditionalEntity(TransformUsageFlags.None, false, name + "_AnimationAssets");
                var buffer = baker.AddBuffer<NewBlobAssetDatabaseRecord<AnimationClipBlob>>(e);
                buffer.AddValidAnimations(bakedAnimations);

                if (bakedAnimations.IsCreated) bakedAnimations.Dispose();
            }

            base.Bake(context);
        }
    }
}
