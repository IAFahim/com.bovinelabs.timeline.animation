using Rukhanka;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Hash128 = Unity.Entities.Hash128;

namespace BovineLabs.Timeline.Animation
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateBefore(typeof(AnimationProcessSystem))]
    public partial struct TimelineAnimationUnificationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BlobDatabaseSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var blobDB = SystemAPI.GetSingleton<BlobDatabaseSingleton>();

            var job = new UnifyAnimationsJob
            {
                AnimDB = blobDB.animations,
                DeltaTime = SystemAPI.Time.DeltaTime
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct UnifyAnimationsJob : IJobEntity
        {
            [ReadOnly] public NativeHashMap<Hash128, BlobAssetReference<AnimationClipBlob>> AnimDB;
            public float DeltaTime;

            public void Execute(
                Entity entity,
                ref BlendGroupTimer timer,
                ref FallbackBlend fallbackData,
                ref DynamicBuffer<BlendGroupEntry> blendEntries,
                ref DynamicBuffer<SmoothBlendGroupEntry> smoothEntries,
                ref DynamicBuffer<AnimationToProcessComponent> atps)
            {
                atps.Clear();

                for (var i = 0; i < smoothEntries.Length; i++)
                {
                    var s = smoothEntries[i];
                    s.TargetWeight = 0f;
                    smoothEntries[i] = s;
                }

                var smoothIndex = new NativeHashMap<uint, int>(smoothEntries.Length, Allocator.Temp);
                for (var j = 0; j < smoothEntries.Length; j++)
                    smoothIndex.TryAdd(smoothEntries[j].MotionId, j);

                for (var i = 0; i < blendEntries.Length; i++)
                {
                    var request = blendEntries[i];

                    if (smoothIndex.TryGetValue(request.MotionId, out var j))
                    {
                        var s = smoothEntries[j];
                        s.TargetWeight = request.Weight;
                        s.NormalizedTime = request.NormalizedTime;
                        s.LayerIndex = request.LayerIndex;
                        s.BlendMode = request.BlendMode;
                        s.AvatarMaskHash = request.AvatarMaskHash;
                        s.MotionId = request.MotionId;
                        smoothEntries[j] = s;
                    }
                    else
                    {
                        smoothEntries.Add(new SmoothBlendGroupEntry
                        {
                            LayerIndex = request.LayerIndex,
                            ClipHash = request.ClipHash,
                            NormalizedTime = request.NormalizedTime,
                            CurrentWeight = 0f,
                            TargetWeight = request.Weight,
                            BlendMode = request.BlendMode,
                            AvatarMaskHash = request.AvatarMaskHash,
                            MotionId = request.MotionId
                        });
                        smoothIndex.Add(request.MotionId, smoothEntries.Length - 1);
                    }
                }

                smoothIndex.Dispose();

                var totalOverrideWeight = 0f;

                for (var i = smoothEntries.Length - 1; i >= 0; i--)
                {
                    var s = smoothEntries[i];
                    var speed = s.CurrentWeight < s.TargetWeight
                        ? fallbackData.BlendInSpeed
                        : fallbackData.BlendOutSpeed;

                    if (s.CurrentWeight < s.TargetWeight)
                        s.CurrentWeight = math.min(s.TargetWeight, s.CurrentWeight + speed * DeltaTime);
                    else if (s.CurrentWeight > s.TargetWeight)
                        s.CurrentWeight = math.max(s.TargetWeight, s.CurrentWeight - speed * DeltaTime);

                    if (s.CurrentWeight <= 0.0001f && s.TargetWeight <= 0.0001f)
                    {
                        smoothEntries.RemoveAtSwapBack(i);
                        continue;
                    }

                    if (s.TargetWeight <= 0.0001f && AnimDB.TryGetValue(s.ClipHash, out var clipBlob) &&
                        clipBlob.IsCreated)
                    {
                        var duration = math.max(0.001f, clipBlob.Value.length);
                        s.NormalizedTime += DeltaTime / duration;
                        s.NormalizedTime = math.frac(s.NormalizedTime);
                    }

                    if (s.BlendMode == AnimationBlendingMode.Override) totalOverrideWeight += s.CurrentWeight;

                    smoothEntries[i] = s;
                }

                var normalizeFactor = 1.0f;
                if (totalOverrideWeight > 1.0f)
                {
                    normalizeFactor = 1.0f / totalOverrideWeight;
                    totalOverrideWeight = 1.0f;
                }

                var fallbackWeight = 1.0f - totalOverrideWeight;
                if (fallbackWeight > 0.0001f && fallbackData.ClipHash.IsValid)
                    if (AnimDB.TryGetValue(fallbackData.ClipHash, out var fallbackClip) && fallbackClip.IsCreated)
                    {
                        if (timer.PreviousFallbackClipHash != fallbackData.ClipHash)
                        {
                            timer.FallbackAccumulatedTime = 0f;
                            timer.PreviousFallbackClipHash = fallbackData.ClipHash;
                        }

                        var duration = math.max(0.001f, fallbackClip.Value.length);
                        timer.FallbackAccumulatedTime += DeltaTime / duration;

                        var fallbackTime = fallbackData.PlaybackMode switch
                        {
                            FallbackPlaybackMode.Clamp => math.min(timer.FallbackAccumulatedTime, 1f),
                            FallbackPlaybackMode.Hold => 1f,
                            _ => math.frac(timer.FallbackAccumulatedTime)
                        };

                        atps.Add(new AnimationToProcessComponent
                        {
                            animation = fallbackClip,
                            time = fallbackTime,
                            weight = fallbackWeight,
                            blendMode = fallbackData.BlendMode,
                            layerIndex = fallbackData.LayerIndex,
                            layerWeight = 1.0f,
                            motionId = 0xFFFFFFFF
                        });
                    }

                for (var i = 0; i < smoothEntries.Length; i++)
                {
                    var s = smoothEntries[i];
                    if (AnimDB.TryGetValue(s.ClipHash, out var clipBlob) && clipBlob.IsCreated)
                    {
                        var appliedWeight = s.BlendMode == AnimationBlendingMode.Override
                            ? s.CurrentWeight * normalizeFactor
                            : s.CurrentWeight;

                        atps.Add(new AnimationToProcessComponent
                        {
                            animation = clipBlob,
                            time = s.NormalizedTime,
                            weight = appliedWeight,
                            blendMode = s.BlendMode,
                            layerIndex = s.LayerIndex,
                            layerWeight = 1.0f,
                            motionId = s.MotionId
                        });
                    }
                }

                blendEntries.Clear();
            }
        }
    }
}