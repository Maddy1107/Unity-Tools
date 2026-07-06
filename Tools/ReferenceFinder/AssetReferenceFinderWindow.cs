#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

public class AssetReferenceFinderWindow : EditorWindow
{
    // ---------------- UI STATE ----------------
    private List<Object> manualTargets = new();
    private Object[] targets;
    private ReferenceGraphView graphView;
    private bool showHelp;

    private Vector2 manualTargetScroll;              // NEW
    private const int MaxVisibleTargets = 5;          // NEW

    // ---------------- SCAN STATE ----------------
    private bool isScanning;
    private float scanProgress;
    private string scanLabel;

    private string[] scanPaths;
    private int scanIndex;
    private Dictionary<string, List<string>> graphData;

    // ---------------- RESULT STATE ----------------
    private int referencedCount = 0;
    private int unreferencedCount = 0;

    // ----------------------------------------------------

    [MenuItem("Assets/Find References", false, 2000)]
    private static void OpenFromContext()
    {
        var window = GetWindow<AssetReferenceFinderWindow>("Find References");

        foreach (var obj in Selection.objects)
        {
            if (obj != null && !window.manualTargets.Contains(obj))
                window.manualTargets.Add(obj);
        }

        window.targets = window.manualTargets.ToArray();
        window.StartScan();
    }

    [MenuItem("Sachin Saga Tools/5. Reference Finder", priority = 5)]
    private static void OpenFromMenu()
    {
        GetWindow<AssetReferenceFinderWindow>("Reference Finder");
    }

    // ----------------------------------------------------

    private void OnEnable()
    {
        rootVisualElement.Clear();

        var imgui = new IMGUIContainer(DrawIMGUI);
        imgui.style.flexShrink = 0;
        rootVisualElement.Add(imgui);

        graphView = new ReferenceGraphView();
        graphView.style.flexGrow = 1;
        rootVisualElement.Add(graphView);

        graphView.OnAssetsDropped = objs =>
        {
            foreach (var obj in objs)
            {
                if (obj != null && !manualTargets.Contains(obj))
                    manualTargets.Add(obj);
            }

            Repaint();
        };
    }

    private void OnDisable()
    {
        EditorApplication.update -= ScanStep;
    }

    // ----------------------------------------------------
    // IMGUI
    // ----------------------------------------------------

    private void DrawIMGUI()
    {
        HandleGlobalDragAndDrop();

        GUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Asset Reference Finder", EditorStyles.boldLabel);
        if (GUILayout.Button("?", EditorStyles.miniButton, GUILayout.Width(22)))
            showHelp = !showHelp;
        EditorGUILayout.EndHorizontal();

        if (showHelp)
        {
            EditorGUILayout.HelpBox(
                "• Drag & drop assets anywhere\n" +
                "• Single click → focus node\n" +
                "• Double click → ping asset",
                MessageType.Info);
        }

        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // ---------- SCROLLABLE TARGET LIST ----------
        int visible = Mathf.Min(manualTargets.Count, MaxVisibleTargets);
        float rowHeight = EditorGUIUtility.singleLineHeight + 6;
        float scrollHeight = visible * rowHeight;

        manualTargetScroll = EditorGUILayout.BeginScrollView(
            manualTargetScroll,
            GUILayout.Height(scrollHeight)
        );

        for (int i = 0; i < manualTargets.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            Rect rowRect = EditorGUILayout.GetControlRect();

            manualTargets[i] = EditorGUI.ObjectField(
                rowRect,
                manualTargets[i],
                typeof(Object),
                false
            );

            // CLICK HANDLING
            if (Event.current.type == EventType.MouseUp &&
                rowRect.Contains(Event.current.mousePosition) &&
                manualTargets[i] != null)
            {
                if (Event.current.clickCount == 2)
                {
                    EditorGUIUtility.PingObject(manualTargets[i]);
                }
                else
                {
                    string path = AssetDatabase.GetAssetPath(manualTargets[i]);
                    graphView.FocusNode(path);
                }

                Event.current.Use();
            }

            if (GUILayout.Button("✕", GUILayout.Width(24)))
            {
                manualTargets.RemoveAt(i);
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Add Asset"))
            manualTargets.Add(null);

        if (GUILayout.Button("Clear"))
        {
            manualTargets.Clear();
            graphView.ClearGraph();
            referencedCount = 0;
            unreferencedCount = 0;
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUILayout.Label($"Referenced: {referencedCount}", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"Unreferenced: {unreferencedCount}", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);

        if (isScanning)
        {
            Rect r = GUILayoutUtility.GetRect(1, 20);
            EditorGUI.ProgressBar(r, scanProgress, $"Scanning: {scanLabel}");

            if (GUILayout.Button("Cancel Scan"))
                CancelScan();
        }
        else
        {
            using (new EditorGUI.DisabledScope(manualTargets.Count == 0))
            {
                if (GUILayout.Button("Find References", GUILayout.Height(32)))
                {
                    targets = manualTargets
                        .Where(o => o != null)
                        .Distinct()
                        .ToArray();

                    StartScan();
                }
            }
        }
    }

    // ----------------------------------------------------
    // DRAG & DROP
    // ----------------------------------------------------

    private void HandleGlobalDragAndDrop()
    {
        Event evt = Event.current;

        if (evt.type != EventType.DragUpdated &&
            evt.type != EventType.DragPerform)
            return;

        if (DragAndDrop.objectReferences == null ||
            DragAndDrop.objectReferences.Length == 0)
            return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

        if (evt.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();

            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj != null && !manualTargets.Contains(obj))
                    manualTargets.Add(obj);
            }

            Repaint();
        }

        evt.Use();
    }

    // ----------------------------------------------------
    // SCANNING
    // ----------------------------------------------------

    private void StartScan()
    {
        if (targets == null || targets.Length == 0)
            return;

        referencedCount = 0;
        unreferencedCount = 0;

        var targetPaths = targets
            .Select(AssetDatabase.GetAssetPath)
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct()
            .ToList();

        scanPaths = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/"))
            .Where(p => !AssetDatabase.IsValidFolder(p))
            .ToArray();

        graphData = new Dictionary<string, List<string>>();
        foreach (var t in targetPaths)
            graphData[t] = new List<string>();

        scanIndex = 0;
        scanProgress = 0f;
        isScanning = true;

        EditorApplication.update -= ScanStep;
        EditorApplication.update += ScanStep;
    }

    private void ScanStep()
    {
        if (!isScanning)
            return;

        for (int i = 0; i < 50 && scanIndex < scanPaths.Length; i++)
        {
            string path = scanPaths[scanIndex];
            scanLabel = path;

            var deps = DependencyCache.Get(path);

            foreach (var target in graphData.Keys)
            {
                if (deps.Contains(target))
                    graphData[target].Add(path);
            }

            scanIndex++;
        }

        scanProgress = scanIndex / (float)scanPaths.Length;
        Repaint();

        if (scanIndex >= scanPaths.Length)
            FinishScan();
    }

    private void FinishScan()
    {
        isScanning = false;
        EditorApplication.update -= ScanStep;

        foreach (var kvp in graphData)
        {
            if (kvp.Value.Count > 0)
                referencedCount++;
            else
                unreferencedCount++;
        }

        graphView.BuildSeparate(graphData);
        Repaint();
    }

    private void CancelScan()
    {
        isScanning = false;
        EditorApplication.update -= ScanStep;
        Repaint();
    }
}
#endif
