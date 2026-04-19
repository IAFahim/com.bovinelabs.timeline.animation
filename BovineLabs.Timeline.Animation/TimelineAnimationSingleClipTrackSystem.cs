using BovineLabs.Timeline.Data;
using Rukhanka;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace BovineLabs.Timeline.Animation
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateBefore(typeof(AnimationProcessSystem))]
    public partial struct TimelineAnimationSingleClipTrackSystem : ISystem
    {
        private NativeParallelMultiHashMap<Entity, AnimationToProcessComponent> activeAnimationsMap;
        private NativeHashSet<Entity> drivenEntitiesLastFrame;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            activeAnimationsMap =
                new NativeParallelMultiHashMap<Entity, AnimationToProcessComponent>(64, Allocator.Persistent);
            drivenEntitiesLastFrame = new NativeHashSet<Entity>(64, Allocator.Persistent);

            state.RequireForUpdate<BlobDatabaseSingleton>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (activeAnimationsMap.IsCreated)
                activeAnimationsMap.Dispose();

            if (drivenEntitiesLastFrame.IsCreated)
                drivenEntitiesLastFrame.Dispose();
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
                ActiveAnimations = activeAnimationsMap.AsParallelWriter()
            };

            state.Dependency = gatherJob.ScheduleParallel(state.Dependency);

            var applyJob = new ApplyAnimationsJob
            {
                ActiveAnimations = activeAnimationsMap,
                DrivenEntitiesLastFrame = drivenEntitiesLastFrame,
                AnimationBuffers = SystemAPI.GetBufferLookup<AnimationToProcessComponent>()
            };

            state.Dependency = applyJob.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive), typeof(TimelineActive))]
        public partial struct GatherActiveClipsJob : IJobEntity
        {
            [ReadOnly] public NativeHashMap<Hash128, BlobAssetReference<AnimationClipBlob>> AnimDB;
            [ReadOnly] public ComponentLookup<ClipWeight> ClipWeights;

            public NativeParallelMultiHashMap<Entity, AnimationToProcessComponent>.ParallelWriter ActiveAnimations;

            private void Execute(Entity clipEntity, in RukhankaAnimationClipAnimated clipData, in TrackBinding binding,
                in LocalTime localTime)
            {
                if (!AnimDB.TryGetValue(clipData.AnimationHash, out var clipBlob))
                    return;

                var weight = 1f;
                if (ClipWeights.TryGetComponent(clipEntity, out var clipWeight))
                    weight = clipWeight.Value;

                if (weight <= 0f)
                    return;

                var timeInSeconds = (float)(double)localTime.Value;
                var normalizedTime = clipBlob.Value.length > 0f ? timeInSeconds / clipBlob.Value.length : 0f;

                var atp = new AnimationToProcessComponent
                {
                    animation = clipBlob,
                    time = normalizedTime,
                    weight = weight,
                    avatarMask = default,
                    blendMode = AnimationBlendingMode.Override,
                    layerIndex = 0,
                    layerWeight = 1f,
                    motionId = (uint)clipEntity
                        .Index
                };

                ActiveAnimations.Add(binding.Value, atp);
            }
        }

        [BurstCompile]
        public struct ApplyAnimationsJob : IJob
        {
            [ReadOnly] public NativeParallelMultiHashMap<Entity, AnimationToProcessComponent> ActiveAnimations;
            public NativeHashSet<Entity> DrivenEntitiesLastFrame;
            public BufferLookup<AnimationToProcessComponent> AnimationBuffers;

            public void Execute()
            {
                var (uniqueKeys, uniqueCount) = ActiveAnimations.GetUniqueKeyArray(Allocator.Temp);

                foreach (var entity in DrivenEntitiesLastFrame)
                    if (!ActiveAnimations.ContainsKey(entity))
                        if (AnimationBuffers.TryGetBuffer(entity, out var buffer))
                            buffer.Clear();

                DrivenEntitiesLastFrame.Clear();

                for (var i = 0; i < uniqueCount; i++)
                {
                    var entity = uniqueKeys[i];

                    if (AnimationBuffers.TryGetBuffer(entity, out var buffer))
                    {
                        buffer.Clear();

                        foreach (var atp in ActiveAnimations.GetValuesForKey(entity)) buffer.Add(atp);
                    }

                    DrivenEntitiesLastFrame.Add(entity);
                }

                uniqueKeys.Dispose();
            }
        }
    }
}