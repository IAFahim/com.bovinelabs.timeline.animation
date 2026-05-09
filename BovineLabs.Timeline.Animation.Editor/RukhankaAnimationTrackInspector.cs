// using BovineLabs.Timeline.Animation.Authoring;
// using UnityEditor;
// using UnityEngine;
//
// namespace BovineLabs.Timeline.Animation.Editor
// {
//     [CustomEditor(typeof(RukhankaAnimationTrack)), CanEditMultipleObjects]
//     public class RukhankaAnimationTrackInspector : UnityEditor.Editor
//     {
//         SerializedProperty m_LayerIndex;
//         SerializedProperty m_TrackOffset;
//         SerializedProperty m_PositionOffset;
//         SerializedProperty m_EulerAnglesOffset;
//         SerializedProperty m_AvatarMask;
//         SerializedProperty m_ApplyAvatarMask;
//
//         void OnEnable()
//         {
//             m_LayerIndex = serializedObject.FindProperty("LayerIndex");
//             m_TrackOffset = serializedObject.FindProperty("trackOffset");
//             m_PositionOffset = serializedObject.FindProperty("positionOffset");
//             m_EulerAnglesOffset = serializedObject.FindProperty("eulerAnglesOffset");
//             m_AvatarMask = serializedObject.FindProperty("avatarMask");
//             m_ApplyAvatarMask = serializedObject.FindProperty("applyAvatarMask");
//         }
//
//         public override void OnInspectorGUI()
//         {
//             serializedObject.Update();
//
//             EditorGUILayout.PropertyField(m_LayerIndex);
//
//             EditorGUILayout.Space();
//             EditorGUILayout.LabelField("Track Offsets", EditorStyles.boldLabel);
//             EditorGUILayout.PropertyField(m_TrackOffset);
//             
//             // Only draw offset fields if mode is Transform Offsets
//             if (m_TrackOffset.enumValueIndex == (int)UnityEngine.Timeline.TrackOffset.ApplyTransformOffsets)
//             {
//                 EditorGUI.indentLevel++;
//                 EditorGUILayout.PropertyField(m_PositionOffset, new GUIContent("Position"));
//                 EditorGUILayout.PropertyField(m_EulerAnglesOffset, new GUIContent("Rotation"));
//                 EditorGUI.indentLevel--;
//             }
//
//             EditorGUILayout.Space();
//             EditorGUILayout.PropertyField(m_ApplyAvatarMask);
//             if (m_ApplyAvatarMask.boolValue || m_ApplyAvatarMask.hasMultipleDifferentValues)
//             {
//                 EditorGUI.indentLevel++;
//                 EditorGUILayout.PropertyField(m_AvatarMask);
//                 EditorGUI.indentLevel--;
//             }
//
//             serializedObject.ApplyModifiedProperties();
//         }
//     }
// }