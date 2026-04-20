using BovineLabs.Core.Jobs;
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
        private TrackBlendImpl<float2, BlendTree2DDirectionClipData> _blendImpl;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BlobDatabaseSingleton>();
            _blendImpl.OnCreate(ref state);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _blendImpl.OnDestroy(ref state);
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

            var blendData = _blendImpl.Update(ref state);

            var targetToTrackMap = new NativeParallelHashMap<Entity, Entity>(64, Allocator.TempJob);
            var targetToTimeMap = new NativeParallelHashMap<Entity, float>(64, Allocator.TempJob);

            state.Dependency = new GatherTrackInfoJob
            {
                TargetToTrack = targetToTrackMap.AsParallelWriter(),
                TargetToTime = targetToTimeMap.AsParallelWriter()
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new DecomposeAndAppendBlendTreeJob
            {
                BlendData = blendData,
                TargetToTrack = targetToTrackMap.AsReadOnly(),
                TargetToTime = targetToTimeMap.AsReadOnly(),
                AnimDB = blobDB.animations,
                TrackDataLookup = SystemAPI.GetComponentLookup<BlendAnimationTree2DTrackData>(true),
                MotionBufferLookup = SystemAPI.GetBufferLookup<BlendTree2DMotionData>(true),
                PlaybackStateLookup = SystemAPI.GetBufferLookup<BlendTreePlaybackStateElement>(),
                FallbackOverrideLookup = SystemAPI.GetComponentLookup<TrackFallbackOverride>(true),
                BlendGroupLookup = SystemAPI.GetBufferLookup<BlendGroupEntry>(),
                FallbackBufferLookup = SystemAPI.GetComponentLookup<BlendGroupFallBackForNoAnimationToProcessComponent>(),
                GlobalDeltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(blendData, 64, state.Dependency);

            targetToTrackMap.Dispose(state.Dependency);
            targetToTimeMap.Dispose(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct UpdateDynamicBlendParametersJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<PhysicsVelocity> PhysicsVelocityLookup;
            [ReadOnly] public ComponentLookup<PlayerMoveInput> PlayerMoveInputLookup;

            private void Execute(ref BlendTree2DDirectionClipData clipData)
            {
                if (clipData.BlendDirectionComponentTarget == BlendDirectionComponentTarget.PhysicsLinearVelocityNormalized)
                {
                    if (PhysicsVelocityLookup.TryGetComponent(clipData.BlendDirectionEntityTarget, out var pv))
                    {
                        var vel2d = new float2(pv.Linear.x, pv.Linear.z);
                        var lengthSq = math.lengthsq(vel2d);
                        if (lengthSq > 0.0001f)
                            clipData.Value = vel2d / math.sqrt(lengthSq);
                        else
                            clipData.Value = float2.zero;
                    }
                }
                else if (clipData.BlendDirectionComponentTarget == BlendDirectionComponentTarget.PlayerMoveInput)
                {
                    if (PlayerMoveInputLookup.TryGetComponent(clipData.BlendDirectionEntityTarget, out var moveInput))
                    {
                        var vel2d = moveInput.Value;
                        var lengthSq = math.lengthsq(vel2d);

                        if (lengthSq > 1f)
                            clipData.Value = vel2d / math.sqrt(lengthSq);
                        else
                            clipData.Value = vel2d;
                    }
                }
            }
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct GatherTrackInfoJob : IJobEntity
        {
            public NativeParallelHashMap<Entity, Entity>.ParallelWriter TargetToTrack;
            public NativeParallelHashMap<Entity, float>.ParallelWriter TargetToTime;

            public void Execute(in TrackBinding binding, in Clip clip, in LocalTime localTime)
            {
                TargetToTrack.TryAdd(binding.Value, clip.Track);
                TargetToTime.TryAdd(binding.Value, (float)localTime.Value);
            }
        }

        [BurstCompile]
        private struct DecomposeAndAppendBlendTreeJob : IJobParallelHashMapDefer
        {
            [ReadOnly] public NativeParallelHashMap<Entity, MixData<float2>>.ReadOnly BlendData;
            [ReadOnly] public NativeParallelHashMap<Entity, Entity>.ReadOnly TargetToTrack;
            [ReadOnly] public NativeParallelHashMap<Entity, float>.ReadOnly TargetToTime;
            [ReadOnly] public NativeHashMap<Hash128, BlobAssetReference<AnimationClipBlob>> AnimDB;
            [ReadOnly] public ComponentLookup<BlendAnimationTree2DTrackData> TrackDataLookup;
            [ReadOnly] public BufferLookup<BlendTree2DMotionData> MotionBufferLookup;
            [ReadOnly] public ComponentLookup<TrackFallbackOverride> FallbackOverrideLookup;

            [NativeDisableParallelForRestriction] public BufferLookup<BlendTreePlaybackStateElement> PlaybackStateLookup;
            [NativeDisableParallelForRestriction] public BufferLookup<BlendGroupEntry> BlendGroupLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<BlendGroupFallBackForNoAnimationToProcessComponent> FallbackBufferLookup;

            public float GlobalDeltaTime;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(BlendData, entryIndex, out var targetEntity, out var mixData);

                var blendedDirection = JobHelpers.Blend<float2, Float2Mixer>(ref mixData, float2.zero);
                var totalTimelineWeight = math.min(1f, mixData.Weights.x + mixData.Weights.y + mixData.Weights.z + mixData.Weights.w);

                if (totalTimelineWeight <= 0f || !TargetToTrack.TryGetValue(targetEntity, out var trackEntity) ||
                    !TargetToTime.TryGetValue(targetEntity, out var absoluteTime) ||
                    !MotionBufferLookup.TryGetBuffer(trackEntity, out var motions) ||
                    !TrackDataLookup.TryGetComponent(trackEntity, out var trackData) || 
                    !BlendGroupLookup.TryGetBuffer(targetEntity, out var blendGroupBuffer)) return;

                if (FallbackOverrideLookup.TryGetComponent(trackEntity, out var fallbackOverride) && FallbackBufferLookup.HasComponent(targetEntity))
                {
                    FallbackBufferLookup[targetEntity] = new BlendGroupFallBackForNoAnimationToProcessComponent
                    {
                        ClipHash = fallbackOverride.FallbackClipHash,
                        BlendInSpeed = fallbackOverride.BlendInSpeed,
                        BlendOutSpeed = fallbackOverride.BlendOutSpeed
                    };
                }

                var blendTreeClips = new NativeArray<BlobAssetReference<AnimationClipBlob>>(motions.Length, Allocator.Temp);
                var blendTreePositions = new NativeArray<ScriptedAnimator.BlendTree2DMotionElement>(motions.Length, Allocator.Temp);

                for (var i = 0; i < motions.Length; i++)
                {
                    var motionData = motions[i];
                    if (AnimDB.TryGetValue(motionData.AnimationHash, out var clipBlob))
                        blendTreeClips[i] = clipBlob;
                    else
                        blendTreeClips[i] = BlobAssetReference<AnimationClipBlob>.Null;

                    blendTreePositions[i] = motionData.BlendTree2DMotionElement;
                }

                var internalWeights = trackData.BlendTreeType switch
                {
                    MotionBlob.Type.BlendTree2DSimpleDirectional =>
                        ScriptedAnimator.ComputeBlendTree2DSimpleDirectional(blendTreePositions.AsReadOnly(), blendedDirection),
                    MotionBlob.Type.BlendTree2DFreeformCartesian =>
                        ScriptedAnimator.ComputeBlendTree2DFreeformCartesian(blendTreePositions.AsReadOnly(), blendedDirection),
                    MotionBlob.Type.BlendTree2DFreeformDirectional => ScriptedAnimator
                        .ComputeBlendTree2DFreeformDirectional(blendTreePositions.AsReadOnly(), blendedDirection),
                    _ => default
                };

                var weightedDuration = 0f;
                var totalBlendWeight = 0f;

                for (var i = 0; i < internalWeights.Length; i++)
                {
                    var mw = internalWeights[i];
                    var clipBlob = blendTreeClips[mw.motionIndex];
                    if (clipBlob.IsCreated)
                    {
                        weightedDuration += clipBlob.Value.length * mw.weight;
                        totalBlendWeight += mw.weight;
                    }
                }

                if (totalBlendWeight > 0f) weightedDuration /= totalBlendWeight;
                if (weightedDuration <= 0.001f) weightedDuration = 1f;

                var normalizedTime = 0f;

                if (PlaybackStateLookup.TryGetBuffer(targetEntity, out var stateBuffer))
                {
                    int stateIdx = -1;
                    for (int i = 0; i < stateBuffer.Length; i++)
                    {
                        if (stateBuffer[i].Track == trackEntity) { stateIdx = i; break; }
                    }

                    if (stateIdx == -1)
                    {
                        stateIdx = stateBuffer.Length;
                        stateBuffer.Add(new BlendTreePlaybackStateElement { Track = trackEntity, IsInitialized = false });
                    }

                    var playbackState = stateBuffer[stateIdx];

                    if (!playbackState.IsInitialized)
                    {
                        var initialTime = absoluteTime / weightedDuration;
                        playbackState.AccumulatedTime = initialTime;
                        playbackState.PreviousAbsoluteTime = absoluteTime;
                        playbackState.IsInitialized = true;
                        normalizedTime = math.frac(initialTime);
                    }
                    else
                    {
                        var delta = absoluteTime - playbackState.PreviousAbsoluteTime;
                        if (math.abs(delta) > 1.0f) delta = GlobalDeltaTime;
                        playbackState.AccumulatedTime += delta / weightedDuration;
                        playbackState.PreviousAbsoluteTime = absoluteTime;
                        normalizedTime = math.frac(playbackState.AccumulatedTime);
                    }

                    stateBuffer[stateIdx] = playbackState;
                }

                for (var i = 0; i < internalWeights.Length; i++)
                {
                    var mw = internalWeights[i];
                    var clipBlob = blendTreeClips[mw.motionIndex];

                    if (clipBlob.IsCreated && mw.weight > 0f)
                    {
                        blendGroupBuffer.Add(new BlendGroupEntry
                        {
                            LayerIndex = trackData.LayerIndex,
                            ClipHash = clipBlob.Value.hash,
                            NormalizedTime = normalizedTime,
                            Weight = mw.weight * totalTimelineWeight,
                            AvatarMaskHash = default,
                            BlendMode = AnimationBlendingMode.Override
                        });
                    }
                }

                internalWeights.Dispose();
                blendTreeClips.Dispose();
                blendTreePositions.Dispose();
            }
        }
    }
}