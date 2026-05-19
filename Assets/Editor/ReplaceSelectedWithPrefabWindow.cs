using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class ReplaceSelectedWithPrefabWindow : EditorWindow
{
    private GameObject replacementPrefab;

    private bool preserveName = true;
    private bool preserveLayerAndTag = true;
    private bool preserveStaticFlags = true;
    private bool copyScaleToTwoMaterialRoadCubeSize = true;
    private bool deleteOriginal = true;

    [MenuItem("Tools/Sadness/Replace Selected With Prefab")]
    private static void Open()
    {
        GetWindow<ReplaceSelectedWithPrefabWindow>("Replace With Prefab");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Replace Selected Objects", EditorStyles.boldLabel);

        replacementPrefab = (GameObject)EditorGUILayout.ObjectField(
            "Replacement Prefab",
            replacementPrefab,
            typeof(GameObject),
            false
        );

        EditorGUILayout.Space();

        preserveName = EditorGUILayout.Toggle("Preserve Name", preserveName);
        preserveLayerAndTag = EditorGUILayout.Toggle("Preserve Layer And Tag", preserveLayerAndTag);
        preserveStaticFlags = EditorGUILayout.Toggle("Preserve Static Flags", preserveStaticFlags);

        EditorGUILayout.Space();

        copyScaleToTwoMaterialRoadCubeSize = EditorGUILayout.Toggle(
            "Copy Scale To RoadCube Size",
            copyScaleToTwoMaterialRoadCubeSize
        );

        deleteOriginal = EditorGUILayout.Toggle("Delete Original Objects", deleteOriginal);

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(replacementPrefab == null || Selection.gameObjects.Length == 0))
        {
            if (GUILayout.Button("Replace Selected"))
            {
                ReplaceSelectedObjects();
            }
        }

        EditorGUILayout.HelpBox(
            "Select the old Cube objects in the Hierarchy, assign your RoadCube prefab here, then click Replace Selected. Use Ctrl+Z if the result is wrong.",
            MessageType.Info
        );
    }

    private void ReplaceSelectedObjects()
    {
        if (replacementPrefab == null)
        {
            Debug.LogWarning("No replacement prefab assigned.");
            return;
        }

        GameObject[] selectedObjects = Selection.gameObjects;

        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("No objects selected.");
            return;
        }

        List<GameObject> newObjects = new List<GameObject>();

        Undo.SetCurrentGroupName("Replace Selected With Prefab");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (GameObject oldObject in selectedObjects)
        {
            if (oldObject == null)
                continue;

            if (EditorUtility.IsPersistent(oldObject))
                continue;

            Transform oldTransform = oldObject.transform;
            Transform oldParent = oldTransform.parent;

            Vector3 oldLocalPosition = oldTransform.localPosition;
            Quaternion oldLocalRotation = oldTransform.localRotation;
            Vector3 oldLocalScale = oldTransform.localScale;

            int oldSiblingIndex = oldTransform.GetSiblingIndex();

            string oldName = oldObject.name;
            int oldLayer = oldObject.layer;
            string oldTag = oldObject.tag;
            StaticEditorFlags oldStaticFlags = GameObjectUtility.GetStaticEditorFlags(oldObject);

            GameObject newObject = PrefabUtility.InstantiatePrefab(replacementPrefab, oldParent) as GameObject;

            if (newObject == null)
            {
                newObject = Instantiate(replacementPrefab, oldParent);
            }

            Undo.RegisterCreatedObjectUndo(newObject, "Create Replacement Object");

            Transform newTransform = newObject.transform;
            newTransform.localPosition = oldLocalPosition;
            newTransform.localRotation = oldLocalRotation;

            bool sizeCopiedToRoadCube = false;

            if (copyScaleToTwoMaterialRoadCubeSize)
            {
                Component roadCubeComponent = newObject.GetComponent("TwoMaterialRoadCube");

                if (roadCubeComponent != null)
                {
                    Undo.RecordObject(roadCubeComponent, "Set RoadCube Size");

                    SerializedObject serializedRoadCube = new SerializedObject(roadCubeComponent);
                    SerializedProperty sizeProperty = serializedRoadCube.FindProperty("size");

                    if (sizeProperty != null && sizeProperty.propertyType == SerializedPropertyType.Vector3)
                    {
                        sizeProperty.vector3Value = new Vector3(
                            Mathf.Abs(oldLocalScale.x),
                            Mathf.Abs(oldLocalScale.y),
                            Mathf.Abs(oldLocalScale.z)
                        );

                        serializedRoadCube.ApplyModifiedProperties();

                        newTransform.localScale = Vector3.one;
                        sizeCopiedToRoadCube = true;

                        EditorUtility.SetDirty(roadCubeComponent);
                    }
                }
            }

            if (!sizeCopiedToRoadCube)
            {
                newTransform.localScale = oldLocalScale;
            }

            newTransform.SetSiblingIndex(oldSiblingIndex);

            if (preserveName)
            {
                newObject.name = oldName;
            }

            if (preserveLayerAndTag)
            {
                newObject.layer = oldLayer;
                newObject.tag = oldTag;
            }

            if (preserveStaticFlags)
            {
                GameObjectUtility.SetStaticEditorFlags(newObject, oldStaticFlags);
            }

            newObjects.Add(newObject);

            if (deleteOriginal)
            {
                Undo.DestroyObjectImmediate(oldObject);
            }

            EditorSceneManager.MarkSceneDirty(newObject.scene);
        }

        Selection.objects = newObjects.ToArray();

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"Replaced {newObjects.Count} object(s) with prefab: {replacementPrefab.name}");
    }
}