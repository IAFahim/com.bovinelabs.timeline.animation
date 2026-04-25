using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.PlayerInputs.Data;
using Rukhanka;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Hash128 = Unity.Entities.Hash128;

namespace BovineLabs.Timeline.Animation
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateBefore(typeof(TimelineAnimationUnificationSystem))]
    public partial struct TimelineAnimationBlendTree2DTrackSystem : ISystem
    {
        internal struct TrackClipData
        {
            public Entity Track;
            public float AbsoluteTime;
            public float2 Direction;
            public float Weight;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BlobDatabaseSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var blobDB = SystemAPI.GetSingleton<BlobDatabaseSingleton>();

            state.Dependency = new UpdateDynamicBlendParametersJob
            {
                PhysicsVelocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocity>(true),
                PlayerMoveInputLookup = SystemAPI.GetComponentLookup<PlayerMoveInput>(true)
            }.ScheduleParallel(state.Dependency);

            var clipCount = SystemAPI.QueryBuilder()
                .WithAll<BlendTree2DDirectionClipData, ClipActive, TrackBinding, Clip, LocalTime>()
                .Build().CalculateEntityCount();
            var clipDataMap = new NativeParallelMultiHashMap<Entity, TrackClipData>(
                math.max(1, clipCount), Allocator.TempJob);

            state.Dependency = new GatherClipDataJob
            {
                ClipDataMap = clipDataMap.AsParallelWriter(),
                ClipLookup = SystemAPI.GetComponentLookup<Clip>(true),
                ClipWeightLookup = SystemAPI.GetComponentLookup<ClipWeight>(true)
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new DecomposeAndAppendBlendTreeJob
            {
                ClipDataMap = clipDataMap.AsReadOnly(),
                AnimDB = blobDB.animations,
                TrackDataLookup = SystemAPI.GetComponentLookup<BlendAnimationTree2DTrackData>(true),
                MotionBufferLookup = SystemAPI.GetBufferLookup<BlendTree2DMotionData>(true),
                FallbackOverrideLookup = SystemAPI.GetComponentLookup<TrackFallbackOverride>(true),
                DefaultFallbackLookup = SystemAPI.GetComponentLookup<DefaultBlendGroupFallback>(true),
                GlobalDeltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(state.Dependency);

            clipDataMap.Dispose(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct UpdateDynamicBlendParametersJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<PhysicsVelocity> PhysicsVelocityLookup;
            [ReadOnly] public ComponentLookup<PlayerMoveInput> PlayerMoveInputLookup;

            private void Execute(ref BlendTree2DDirectionClipData clipData)
            {
                if (clipData.ReadKind == BlendDirectionReadKind.PhysicsLinearVelocityNormalized)
                {
                    if (PhysicsVelocityLookup.TryGetComponent(clipData.ReadEntity, out var pv))
                    {
                        var vel2d = new float2(pv.Linear.x, pv.Linear.z);
                        var lengthSq = math.lengthsq(vel2d);
                        clipData.Value = lengthSq > 0.0001f
                            ? vel2d / math.sqrt(lengthSq)
                            : float2.zero;
                    }
                    else
                    {
                        clipData.Value = float2.zero;
                    }
                }
                else if (clipData.ReadKind == BlendDirectionReadKind.PlayerMoveInput)
                {
                    if (PlayerMoveInputLookup.TryGetComponent(clipData.ReadEntity, out var moveInput))
                    {
                        var vel2d = moveInput.Value;
                        var lengthSq = math.lengthsq(vel2d);
                        clipData.Value = lengthSq > 1f
                            ? vel2d / math.sqrt(lengthSq)
                            : vel2d;
                    }
                    else
                    {
                        clipData.Value = float2.zero;
                    }
                }
            }
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct GatherClipDataJob : IJobEntity
        {
            public NativeParallelMultiHashMap<Entity, TrackClipData>.ParallelWriter ClipDataMap;
            [ReadOnly] public ComponentLookup<Clip> ClipLookup;
            [ReadOnly] public ComponentLookup<ClipWeight> ClipWeightLookup;

            private void Execute(Entity clipEntity, in BlendTree2DDirectionClipData directionData,
                in TrackBinding binding, in LocalTime localTime)
            {
                var weight = 1f;
                if (ClipWeightLookup.TryGetComponent(clipEntity, out var cw))
                    weight = cw.Value;

                if (weight <= 0f) return;

                var track = ClipLookup[clipEntity].Track;

                ClipDataMap.Add(binding.Value, new TrackClipData
                {
                    Track = track,
                    AbsoluteTime = (float)localTime.Value,
                    Direction = directionData.Value,
                    Weight = weight
                });
            }
        }

        [BurstCompile]
        private partial struct DecomposeAndAppendBlendTreeJob : IJobEntity
        {
            [ReadOnly] public NativeParallelMultiHashMap<Entity, TrackClipData>.ReadOnly ClipDataMap;
            [ReadOnly] public NativeHashMap<Hash128, BlobAssetReference<AnimationClipBlob>> AnimDB;
            [ReadOnly] public ComponentLookup<BlendAnimationTree2DTrackData> TrackDataLookup;
            [ReadOnly] public BufferLookup<BlendTree2DMotionData> MotionBufferLookup;
            [ReadOnly] public ComponentLookup<TrackFallbackOverride> FallbackOverrideLookup;
            [ReadOnly] public ComponentLookup<DefaultBlendGroupFallback> DefaultFallbackLookup;

            public float GlobalDeltaTime;

            private void Execute(
                Entity targetEntity,
                ref DynamicBuffer<BlendGroupEntry> blendGroupBuffer,
                ref DynamicBuffer<BlendTreePlaybackStateElement> playbackState,
                ref BlendGroupFallbackForNoAnimationToProcessComponent activeFallback)
            {
                var bestFallbackLayer = -1;
                TrackFallbackOverride bestFallback = default;
                var hasFallbackCandidate = false;

                var processedTracks = new NativeHashMap<Entity, PerTrackBlend>(16, Allocator.Temp);

                if (ClipDataMap.TryGetFirstValue(targetEntity, out var clipData, out var it))
                {
                    do
                    {
                        if (!processedTracks.TryGetValue(clipData.Track, out var blend))
                        {
                            blend = new PerTrackBlend { TrackEntity = clipData.Track };
                        }

                        blend.DirectionX += clipData.Direction.x * clipData.Weight;
                        blend.DirectionY += clipData.Direction.y * clipData.Weight;
                        blend.TotalWeight += clipData.Weight;

                        if (clipData.Weight > blend.BestWeight)
                        {
                            blend.BestWeight = clipData.Weight;
                            blend.AbsoluteTime = clipData.AbsoluteTime;
                        }

                        processedTracks[clipData.Track] = blend;
                    } while (ClipDataMap.TryGetNextValue(out clipData, ref it));
                }

                var trackKeys = processedTracks.GetKeyArray(Allocator.Temp);
                for (int k = 0; k < trackKeys.Length; k++)
                {
                    var trackEntity = trackKeys[k];
                    var blend = processedTracks[trackEntity];

                    if (blend.TotalWeight <= 0f) continue;

                    if (FallbackOverrideLookup.TryGetComponent(trackEntity, out var fo) &&
                        TrackDataLookup.TryGetComponent(trackEntity, out var td) &&
                        td.LayerIndex > bestFallbackLayer)
                    {
                        bestFallbackLayer = td.LayerIndex;
                        bestFallback = fo;
                        hasFallbackCandidate = true;
                    }

                    var totalWeight = math.saturate(blend.TotalWeight);
                    var blendedDirection = new float2(blend.DirectionX, blend.DirectionY) /
                                          math.max(0.0001f, blend.TotalWeight);

                    ProcessTrack(targetEntity, trackEntity, blendedDirection, totalWeight,
                        blend.AbsoluteTime, ref blendGroupBuffer, ref playbackState);
                }
                trackKeys.Dispose();
                processedTracks.Dispose();

                if (hasFallbackCandidate)
                {
                    var prev = activeFallback;
                    activeFallback = new BlendGroupFallbackForNoAnimationToProcessComponent
                    {
                        ClipHash = bestFallback.FallbackClipHash,
                        BlendInSpeed = bestFallback.BlendInSpeed,
                        BlendOutSpeed = bestFallback.BlendOutSpeed,
                        PlaybackMode = bestFallback.PlaybackMode,
                        LayerIndex = bestFallback.LayerIndex,
                        BlendMode = bestFallback.BlendMode,
                        AvatarMaskHash = bestFallback.AvatarMaskHash
                    };

                    if (prev.ClipHash != bestFallback.FallbackClipHash)
                        activeFallback.BlendInSpeed = bestFallback.BlendInSpeed;
                }
                else if (DefaultFallbackLookup.TryGetComponent(targetEntity, out var defaults))
                {
                    var prev = activeFallback;
                    activeFallback = new BlendGroupFallbackForNoAnimationToProcessComponent
                    {
                        ClipHash = defaults.ClipHash,
                        BlendInSpeed = defaults.BlendInSpeed,
                        BlendOutSpeed = defaults.BlendOutSpeed,
                        PlaybackMode = defaults.PlaybackMode,
                        LayerIndex = defaults.LayerIndex,
                        BlendMode = defaults.BlendMode,
                        AvatarMaskHash = defaults.AvatarMaskHash
                    };

                    if (prev.ClipHash != defaults.ClipHash)
                        activeFallback.BlendInSpeed = defaults.BlendInSpeed;
                }
            }

            private void ProcessTrack(
                Entity targetEntity,
                Entity trackEntity,
                float2 blendedDirection,
                float totalTimelineWeight,
                float absoluteTime,
                ref DynamicBuffer<BlendGroupEntry> blendGroupBuffer,
                ref DynamicBuffer<BlendTreePlaybackStateElement> playbackStateBuffer)
            {
                if (!MotionBufferLookup.TryGetBuffer(trackEntity, out var motions) ||
                    !TrackDataLookup.TryGetComponent(trackEntity, out var trackData)) return;

                var blendTreeClips = new NativeArray<BlobAssetReference<AnimationClipBlob>>(motions.Length, Allocator.Temp);
                var blendTreePositions = new NativeArray<ScriptedAnimator.BlendTree2DMotionElement>(motions.Length, Allocator.Temp);

                for (var i = 0; i < motions.Length; i++)
                {
                    var motionData = motions[i];
                    blendTreeClips[i] = AnimDB.TryGetValue(motionData.AnimationHash, out var cb)
                        ? cb
                        : BlobAssetReference<AnimationClipBlob>.Null;
                    blendTreePositions[i] = motionData.BlendTree2DMotionElement;
                }

                var internalWeights = trackData.BlendTreeType switch
                {
                    MotionBlob.Type.BlendTree2DSimpleDirectional =>
                        ScriptedAnimator.ComputeBlendTree2DSimpleDirectional(blendTreePositions.AsReadOnly(), blendedDirection),
                    MotionBlob.Type.BlendTree2DFreeformCartesian =>
                        ScriptedAnimator.ComputeBlendTree2DFreeformCartesian(blendTreePositions.AsReadOnly(), blendedDirection),
                    MotionBlob.Type.BlendTree2DFreeformDirectional =>
                        ScriptedAnimator.ComputeBlendTree2DFreeformDirectional(blendTreePositions.AsReadOnly(), blendedDirection),
                    _ => default
                };

                var weightedDuration = 0f;
                var totalBlendWeight = 0f;

                for (var i = 0; i < internalWeights.Length; i++)
                {
                    var mw = internalWeights[i];
                    if (blendTreeClips[mw.motionIndex].IsCreated)
                    {
                        weightedDuration += blendTreeClips[mw.motionIndex].Value.length * mw.weight;
                        totalBlendWeight += mw.weight;
                    }
                }

                if (totalBlendWeight > 0f) weightedDuration /= totalBlendWeight;
                if (weightedDuration <= 0.001f) weightedDuration = 1f;

                var normalizedTime = 0f;
                int stateIdx = -1;
                for (int i = 0; i < playbackStateBuffer.Length; i++)
                {
                    if (playbackStateBuffer[i].Track == trackEntity) { stateIdx = i; break; }
                }

                if (stateIdx == -1)
                {
                    stateIdx = playbackStateBuffer.Length;
                    playbackStateBuffer.Add(new BlendTreePlaybackStateElement { Track = trackEntity, IsInitialized = false });
                }

                var ps = playbackStateBuffer[stateIdx];

                if (!ps.IsInitialized)
                {
                    var initialTime = absoluteTime / weightedDuration;
                    ps.AccumulatedTime = initialTime;
                    ps.PreviousAbsoluteTime = absoluteTime;
                    ps.IsInitialized = true;
                    normalizedTime = math.frac(initialTime);
                }
                else
                {
                    var delta = absoluteTime - ps.PreviousAbsoluteTime;
                    if (math.abs(delta) > 1.0f) delta = GlobalDeltaTime;
                    ps.AccumulatedTime += delta / weightedDuration;
                    ps.PreviousAbsoluteTime = absoluteTime;
                    normalizedTime = math.frac(ps.AccumulatedTime);
                }

                playbackStateBuffer[stateIdx] = ps;

                for (var i = 0; i < internalWeights.Length; i++)
                {
                    var mw = internalWeights[i];
                    var clipBlob = blendTreeClips[mw.motionIndex];

                    if (clipBlob.IsCreated && mw.weight > 0f)
                    {
                        var clipHash = clipBlob.Value.hash;
                        blendGroupBuffer.Add(new BlendGroupEntry
                        {
                            LayerIndex = trackData.LayerIndex,
                            ClipHash = clipHash,
                            NormalizedTime = normalizedTime,
                            Weight = mw.weight * totalTimelineWeight,
                            AvatarMaskHash = default,
                            BlendMode = AnimationBlendingMode.Override,
                            MotionId = ComputeMotionId(trackEntity, trackData.LayerIndex, clipHash)
                        });
                    }
                }

                internalWeights.Dispose();
                blendTreeClips.Dispose();
                blendTreePositions.Dispose();
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

        private struct PerTrackBlend
        {
            public Entity TrackEntity;
            public float DirectionX;
            public float DirectionY;
            public float TotalWeight;
            public float BestWeight;
            public float AbsoluteTime;
        }
    }
}
