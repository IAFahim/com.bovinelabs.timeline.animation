using BovineLabs.Timeline.Data;
using Rukhanka;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
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
                ClipWeights = SystemAPI.GetComponentLookup<ClipWeight>(true),
                TrackDataLookup = SystemAPI.GetComponentLookup<RukhankaSingleTrackData>(true),
                ActiveAnimations = activeAnimationsMap.AsParallelWriter()
            };

            state.Dependency = gatherJob.ScheduleParallel(state.Dependency);

            var applyJob = new ApplyAnimationsJob
            {
                ActiveAnimations = activeAnimationsMap,
                AnimationBuffers = SystemAPI.GetBufferLookup<BlendGroupEntry>()
            };

            state.Dependency = applyJob.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive), typeof(TimelineActive))]
        public partial struct GatherActiveClipsJob : IJobEntity
        {
            [ReadOnly] public NativeHashMap<Hash128, BlobAssetReference<AnimationClipBlob>> AnimDB;
            [ReadOnly] public ComponentLookup<ClipWeight> ClipWeights;
            [ReadOnly] public ComponentLookup<RukhankaSingleTrackData> TrackDataLookup;

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
                    BlendMode = AnimationBlendingMode.Override
                });
            }
        }

        [BurstCompile]
        public struct ApplyAnimationsJob : IJob
        {
            [ReadOnly] public NativeParallelMultiHashMap<Entity, BlendGroupEntry> ActiveAnimations;
            public BufferLookup<BlendGroupEntry> AnimationBuffers;

            public void Execute()
            {
                var (uniqueKeys, uniqueCount) = ActiveAnimations.GetUniqueKeyArray(Allocator.Temp);

                for (var i = 0; i < uniqueCount; i++)
                {
                    var entity = uniqueKeys[i];
                    if (AnimationBuffers.TryGetBuffer(entity, out var buffer))
                    {
                        foreach (var atp in ActiveAnimations.GetValuesForKey(entity)) 
                            buffer.Add(atp);
                    }
                }

                uniqueKeys.Dispose();
            }
        }
    }
}