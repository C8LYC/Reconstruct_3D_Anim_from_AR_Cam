using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PCBoneController))]
public class PCBoneControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var controller = (PCBoneController)target;
        if (GUILayout.Button("Initialize Skeleton Joints"))
        {
            controller.InitializeSkeletonJoints();
            EditorUtility.SetDirty(controller);
        }

        if (GUILayout.Button("Find Selected Bone Index"))
        {
            var selected = Selection.activeTransform;
            if (selected == null)
            {
                Debug.LogWarning("No Transform selected.");
                return;
            }

            int index = controller.GetBoneMappingIndex(selected);
            if (index >= 0)
            {
                Debug.Log($"Selected bone '{selected.name}' is at m_BoneMapping[{index}].");
            }
            else
            {
                Debug.LogWarning($"Selected bone '{selected.name}' was not found in m_BoneMapping.");
            }
        }
    }
}
