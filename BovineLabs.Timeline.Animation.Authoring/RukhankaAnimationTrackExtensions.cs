using System.Linq;
using Rukhanka;
using Rukhanka.Hybrid;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Hash128 = Unity.Entities.Hash128;

namespace BovineLabs.Timeline.Animation.Authoring
{
    internal static class RukhankaAnimationTrackExtensions
    {
        /// <summary>
        ///     Resolves a RigDefinitionAuthoring from the track binding.
        ///     Supports binding to either a RigDefinitionAuthoring directly, an Animator,
        ///     or a GameObject that contains either component.
        /// </summary>
        public static RigDefinitionAuthoring ResolveRigDefinition(this PlayableDirector director, TrackAsset track)
        {
            var binding = director.GetGenericBinding(track);

            // Direct binding to RigDefinitionAuthoring
            if (binding is RigDefinitionAuthoring rda)
                return rda;

            // Binding to Animator (TrackBindingType changed from RigDefinitionAuthoring to Animator
            // for editor preview support) — look for RigDefinitionAuthoring on the same GameObject.
            if (binding is Animator animator)
                return animator.GetComponent<RigDefinitionAuthoring>();

            // Fallback: binding could be a GameObject
            if (binding is GameObject go)
                return go.GetComponent<RigDefinitionAuthoring>();

            return null;
        }

        public static bool TryComputeHash(this AnimationClip clip, Avatar avatar, out Hash128 hash)
        {
            if (clip != null)
            {
                hash = BakingUtils.ComputeAnimationHash(clip, avatar);
                return true;
            }

            hash = default;
            return false;
        }

        public static void AddValidAnimations(
            this DynamicBuffer<NewBlobAssetDatabaseRecord<AnimationClipBlob>> buffer,
            NativeArray<BlobAssetReference<AnimationClipBlob>> bakedAnimations)
        {
            foreach (var ba in bakedAnimations.Where(ba => ba != BlobAssetReference<AnimationClipBlob>.Null))
                buffer.Add(new NewBlobAssetDatabaseRecord<AnimationClipBlob>
                {
                    hash = ba.Value.hash,
                    value = ba
                });
        }
    }
}