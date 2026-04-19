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
        public static RigDefinitionAuthoring ResolveRigDefinition(this PlayableDirector director, TrackAsset track)
        {
            var binding = director.GetGenericBinding(track);
            return binding as RigDefinitionAuthoring
                   ?? (binding as GameObject)?.GetComponent<RigDefinitionAuthoring>();
        }

        public static Hash128 ComputeHashOrDefault(this AnimationClip clip, Avatar avatar)
        {
            return clip != null ? BakingUtils.ComputeAnimationHash(clip, avatar) : default;
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