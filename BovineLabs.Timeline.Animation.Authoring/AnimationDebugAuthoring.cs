using Unity.Entities;
using UnityEngine;

namespace BovineLabs.Timeline.Animation.Authoring
{
    public class AnimationDebugAuthoring : MonoBehaviour
    {
        public class Baker : Baker<AnimationDebugAuthoring>
        {
            public override void Bake(AnimationDebugAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new AnimationDebugState());
            }
        }
    }
}
