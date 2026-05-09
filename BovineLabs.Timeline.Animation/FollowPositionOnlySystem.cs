using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace BovineLabs.Timeline.Animation
{
    [UpdateInGroup(typeof(TransformSystemGroup))]
    [UpdateAfter(typeof(LocalToWorldSystem))][BurstCompile]
    public partial struct FollowPositionOnlySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new FollowPositionJob
            {
                L2WLookup = SystemAPI.GetComponentLookup<LocalToWorld>(false)
            };

            job.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct FollowPositionJob : IJobEntity
        {
            [NativeDisableParallelForRestriction] 
            public ComponentLookup<LocalToWorld> L2WLookup;

            public void Execute(Entity entity, in FollowPositionOnly follow, ref LocalTransform lt)
            {
                if (L2WLookup.TryGetComponent(follow.TargetBone, out var targetL2W))
                {
                    // Update LocalTransform
                    lt.Position = targetL2W.Position;
                    
                    // Update LocalToWorld directly via lookup
                    if (L2WLookup.TryGetComponent(entity, out var selfL2W))
                    {
                        selfL2W.Value.c3.xyz = targetL2W.Position;
                        L2WLookup[entity] = selfL2W;
                    }
                }
            }
        }
    }
}