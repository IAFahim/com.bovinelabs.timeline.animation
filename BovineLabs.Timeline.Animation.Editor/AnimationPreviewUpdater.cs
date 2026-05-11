#if UNITY_EDITOR
using System;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace BovineLabs.Timeline.Animation.Editor
{
    [InitializeOnLoad]
    internal static class AnimationPreviewUpdater
    {
        private static PlayableDirector s_Director;
        private static double s_LastTime = -1;

        static AnimationPreviewUpdater()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            if (Application.isPlaying) return;
            if (EditorApplication.isCompiling) return;

            var director = s_Director;
            if (director == null)
            {
                director = Object.FindAnyObjectByType<PlayableDirector>();
                if (director == null) return;
                s_Director = director;
                s_LastTime = -1;
            }

            if (!director.playableGraph.IsValid()) return;

            var time = director.time;
            if (Math.Abs(time - s_LastTime) < 0.0001) return;
            s_LastTime = time;

            director.playableGraph.Evaluate();

            // Force ECS Editor World to tick so timeline systems + Rukhanka bone pipeline run
            foreach (var world in World.All)
                if ((world.Flags & WorldFlags.Editor) == WorldFlags.Editor)
                {
                    world.Update();
                    break;
                }
        }
    }
}
#endif