using BovineLabs.Timeline.Animation;
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
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<BlobDatabaseSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var blobDB = SystemAPI.GetSingleton<BlobDatabaseSingleton>();
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            var job = new ProcessSingleClipsJob
            {
                AnimDB = blobDB.animations,
                TrackDataLookup = SystemAPI.GetComponentLookup<RukhankaSingleTrackData>(true),
                ClipWeightLookup = SystemAPI.GetComponentLookup<ClipWeight>(true),
                BlendGroupEntryLookup = SystemAPI.GetBufferLookup<BlendGroupEntry>(true),
                ECB = ecb
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct ProcessSingleClipsJob : IJobEntity
        {
            [ReadOnly] public NativeHashMap<Hash128, BlobAssetReference<AnimationClipBlob>> AnimDB;
            [ReadOnly] public ComponentLookup<RukhankaSingleTrackData> TrackDataLookup;
            [ReadOnly] public ComponentLookup<ClipWeight> ClipWeightLookup;
            [ReadOnly] public BufferLookup<BlendGroupEntry> BlendGroupEntryLookup;

            public EntityCommandBuffer.ParallelWriter ECB;

            public void Execute(Entity clipEntity, [ChunkIndexInQuery] int chunkIndex, in TrackBinding binding,
                in Clip clip, in LocalTime localTime, in RukhankaSingleClipData clipData)
            {
                var target = binding.Value;
                var track = clip.Track;

                if (!BlendGroupEntryLookup.HasBuffer(target)) return;
                
                if (!TrackDataLookup.TryGetComponent(track, out var trackData)) return;

                var timelineWeight =
                    ClipWeightLookup.HasComponent(clipEntity) ? ClipWeightLookup[clipEntity].Value : 1f;
                if (timelineWeight <= 0f) return;

                if (!AnimDB.TryGetValue(clipData.ClipHash, out var clipBlob) || !clipBlob.IsCreated) return;

                var duration = math.max(0.001f, clipBlob.Value.length);
                var absoluteTime = (float)localTime.Value;

                var normalizedTime = clipBlob.Value.looped
                    ? math.frac(absoluteTime / duration)
                    : math.saturate(absoluteTime / duration);

                ECB.AppendToBuffer(chunkIndex, target, new BlendGroupEntry
                {
                    LayerIndex = trackData.LayerIndex,
                    ClipHash = clipData.ClipHash,
                    NormalizedTime = normalizedTime,
                    Weight = timelineWeight,
                    AvatarMaskHash = default,
                    BlendMode = AnimationBlendingMode.Override
                });
            }
        }
    }
}
