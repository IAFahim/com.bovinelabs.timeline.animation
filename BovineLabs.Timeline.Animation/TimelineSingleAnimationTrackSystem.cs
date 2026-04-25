using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.Jobs;
using BovineLabs.Timeline.Data;
using Rukhanka;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Hash128 = Unity.Entities.Hash128;

namespace BovineLabs.Timeline.Animation
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateBefore(typeof(TimelineAnimationUnificationSystem))]
    public partial struct TimelineSingleAnimationTrackSystem : ISystem
    {
        private NativeParallelMultiHashMap<Entity, BlendGroupEntry> activeAnimationsMap;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            activeAnimationsMap = new NativeParallelMultiHashMap<Entity, BlendGroupEntry>(64, Allocator.Persistent);
            state.RequireForUpdate<BlobDatabaseSingleton>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (activeAnimationsMap.IsCreated)
                activeAnimationsMap.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            activeAnimationsMap.Clear();

            var blobDB = SystemAPI.GetSingleton<BlobDatabaseSingleton>();

            var gatherJob = new GatherActiveClipsJob
            {
                AnimDB = blobDB.animations,
                ClipWeights = state.GetUnsafeComponentLookup<ClipWeight>(true),
                TrackDataLookup = state.GetUnsafeComponentLookup<RukhankaSingleTrackData>(true),
                ActiveAnimations = activeAnimationsMap.AsParallelWriter()
            };

            state.Dependency = gatherJob.ScheduleParallel(state.Dependency);

            var applyJob = new ApplyAnimationsJob
            {
                ActiveAnimations = activeAnimationsMap,
                AnimationBuffers = state.GetUnsafeBufferLookup<BlendGroupEntry>()
            };

            state.Dependency = applyJob.ScheduleParallel(activeAnimationsMap, 64, state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive), typeof(TimelineActive))]
        public partial struct GatherActiveClipsJob : IJobEntity
        {
            [ReadOnly] public NativeHashMap<Hash128, BlobAssetReference<AnimationClipBlob>> AnimDB;
            [ReadOnly] public UnsafeComponentLookup<ClipWeight> ClipWeights;
            [ReadOnly] public UnsafeComponentLookup<RukhankaSingleTrackData> TrackDataLookup;

            public NativeParallelMultiHashMap<Entity, BlendGroupEntry>.ParallelWriter ActiveAnimations;

            private void Execute(Entity clipEntity, in RukhankaSingleClipData clipData, in TrackBinding binding, in Clip clip, in LocalTime localTime)
            {
                if (!TrackDataLookup.TryGetComponent(clip.Track, out var trackData)) return;

                var weight = 1f;
                if (ClipWeights.TryGetComponent(clipEntity, out var clipWeight))
                    weight = clipWeight.Value;

                if (weight <= 0f) return;
                if (!AnimDB.TryGetValue(clipData.ClipHash, out var clipBlob) || !clipBlob.IsCreated) return;

                var timeInSeconds = (float)(double)localTime.Value;
                var duration = math.max(0.001f, clipBlob.Value.length);
                var normalizedTime = clipBlob.Value.looped
                    ? math.frac(timeInSeconds / duration)
                    : math.saturate(timeInSeconds / duration);

                ActiveAnimations.Add(binding.Value, new BlendGroupEntry
                {
                    LayerIndex = trackData.LayerIndex,
                    ClipHash = clipData.ClipHash,
                    NormalizedTime = normalizedTime,
                    Weight = weight,
                    AvatarMaskHash = default,
                    BlendMode = AnimationBlendingMode.Override,
                    MotionId = ComputeMotionId(clip.Track, trackData.LayerIndex, clipData.ClipHash)
                });
            }

            private uint ComputeMotionId(Entity track, int layerIndex, Hash128 clipHash)
            {
                var hash = (uint)track.Index;
                hash = hash * 31 ^ (uint)track.Version;
                hash = hash * 31 ^ (uint)layerIndex;
                hash = hash * 31 ^ (uint)clipHash.GetHashCode();
                return hash;
            }
        }

        [BurstCompile]
        public struct ApplyAnimationsJob : IJobParallelHashMapDefer
        {
            [ReadOnly] public NativeParallelMultiHashMap<Entity, BlendGroupEntry> ActiveAnimations;
            [NativeDisableParallelForRestriction] public UnsafeBufferLookup<BlendGroupEntry> AnimationBuffers;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(ActiveAnimations, entryIndex, out var entity, out var entry);

                if (!AnimationBuffers.TryGetBuffer(entity, out var buffer)) return;

                buffer.Add(entry);
            }
        }
    }
}