#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class ShowAllSOandAtlasWindow : EditorWindow
{
    private enum ScanMode
    {
        ScriptableObject,
        SpriteAtlas
    }

    private ScanMode mode;

    private string[] assetPaths;
    private List<Object> foundObjects = new();

    private int index;
    private float progress;
    private bool isScanning;

    private static readonly HashSet<string> ExcludedScriptableObjectTypes = new()
    {
        "ShaderMeshAnimation",
        "SnapshotMeshAnimation"
    };


    // --------------------------------------------------
    // MENU
    // --------------------------------------------------

    [MenuItem("Sachin Saga Tools/6. Filter/Scriptable Objects", priority = 6)]
    private static void OpenSO()
    {
        var window = GetWindow<ShowAllSOandAtlasWindow>(
            utility: true,
            title: "Finding ScriptableObjects");

        window.StartScan(ScanMode.ScriptableObject);
    }

    [MenuItem("Sachin Saga Tools/6. Filter/Sprite Atlases", priority = 6)]
    private static void OpenAtlas()
    {
        var window = GetWindow<ShowAllSOandAtlasWindow>(
            utility: true,
            title: "Finding SpriteAtlases");

        window.StartScan(ScanMode.SpriteAtlas);
    }

    // --------------------------------------------------
    // SCAN START
    // --------------------------------------------------

    private void StartScan(ScanMode scanMode)
    {
        mode = scanMode;

        assetPaths = AssetDatabase
            .GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/"))
            .Where(p => !AssetDatabase.IsValidFolder(p))
            .ToArray();

        foundObjects.Clear();
        index = 0;
        progress = 0f;
        isScanning = true;

        EditorApplication.update -= ScanStep;
        EditorApplication.update += ScanStep;
    }


    // --------------------------------------------------
    // SCAN STEP
    // --------------------------------------------------

    private void ScanStep()
    {
        if (!isScanning)
            return;

        const int STEPS_PER_FRAME = 400; // 🔥 much faster

        for (int i = 0; i < STEPS_PER_FRAME && index < assetPaths.Length; i++)
        {
            string path = assetPaths[index];

            if (IsMatchFast(path))
            {
                var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (obj != null)
                    foundObjects.Add(obj);
            }

            index++;
        }

        progress = index / (float)assetPaths.Length;
        Repaint();

        if (index >= assetPaths.Length)
            Finish();
    }

    private bool IsMatchFast(string path)
    {
        // -------- SCRIPTABLE OBJECTS --------
        if (mode == ScanMode.ScriptableObject)
        {
            // 1️⃣ Must be a .asset file
            if (!path.EndsWith(".asset"))
                return false;

            // 2️⃣ Load MAIN asset instance (not sub-assets)
            var obj = AssetDatabase.LoadMainAssetAtPath(path);
            if (obj == null)
                return false;

            // 3️⃣ Must be ScriptableObject
            if (obj is not ScriptableObject so)
                return false;

            // 4️⃣ Must be editable & visible
            if ((so.hideFlags & (HideFlags.NotEditable | HideFlags.HideInHierarchy)) != 0)
                return false;

            if (ExcludedScriptableObjectTypes.Contains(so.GetType().Name))
                return false;


            // 5️⃣ Assembly filter (gameplay code only)
            var asmName = so.GetType().Assembly.GetName().Name;
            return asmName == "Assembly-CSharp"
                || asmName.StartsWith("MyGame");
        }




        // -------- SPRITE ATLASES --------
        if (mode == ScanMode.SpriteAtlas)
        {
            return path.EndsWith(".spriteatlas");
        }

        return false;
    }

    // --------------------------------------------------
    // FINISH / CANCEL
    // --------------------------------------------------

    private void Finish()
    {
        isScanning = false;
        EditorApplication.update -= ScanStep;

        if (foundObjects.Count > 0)
        {
            FilteredAssetListWindow.Show(
                mode == ScanMode.ScriptableObject
                    ? "Scriptable Objects"
                    : "Sprite Atlases",
                foundObjects
            );
        }
        else
        {
            EditorUtility.DisplayDialog(
                "Filter",
                $"No {mode}s found in the project.",
                "OK");
        }

        Close();
    }

    private void Cancel()
    {
        isScanning = false;
        EditorApplication.update -= ScanStep;
        Close();
    }

    private void OnDisable()
    {
        EditorApplication.update -= ScanStep;
    }

    // --------------------------------------------------
    // UI
    // --------------------------------------------------

    private void OnGUI()
    {
        GUILayout.Label(
            mode == ScanMode.ScriptableObject
                ? "Scanning project for ScriptableObjects…"
                : "Scanning project for SpriteAtlases…",
            EditorStyles.boldLabel);

        GUILayout.Space(8);

        Rect r = GUILayoutUtility.GetRect(1, 20);
        EditorGUI.ProgressBar(
            r,
            progress,
            $"{Mathf.RoundToInt(progress * 100f)}%");

        GUILayout.Space(8);

        if (GUILayout.Button("Cancel"))
            Cancel();
    }
}
#endif
