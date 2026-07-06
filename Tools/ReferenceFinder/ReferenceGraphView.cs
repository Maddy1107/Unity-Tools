#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class ReferenceGraphView : VisualElement
{
    private const float ROOT_X = 60f;
    private const float REF_X = 480f;
    private const float START_Y = 60f;
    private const float Y_SPACING = 80f;
    private const int GRAPHS_PER_ROW = 4;
    private const float GRAPH_WIDTH = 1020f;   // root + refs width
    private const float GRAPH_GAP_X = 40f;
    private const float GRAPH_GAP_Y = 80f;


    // ---------------- COLORS ----------------
    private static readonly Color[] RootPalette =
    {
        new(0.55f, 0.8f, 1f),
        new(0.6f, 1f, 0.6f),
        new(1f, 0.7f, 0.7f),
        new(0.9f, 0.8f, 1f),
        new(1f, 0.9f, 0.6f)
    };

    private static readonly Color PrefabColor        = new(0.35f, 0.65f, 1f);
    private static readonly Color SpriteAtlasColor   = new(0.7f, 0.45f, 1f);
    private static readonly Color ScriptableObjColor = new(1f, 0.65f, 0.25f);
    private static readonly Color SceneColor         = new(0.65f, 0.55f, 0.35f);
    private static readonly Color ScriptColor        = new(0.4f, 0.85f, 0.5f);
    private static readonly Color UnknownColor       = new(0.75f, 0.75f, 0.75f);

    // ---------------- FILTER STATE (SHARED) ----------------
    private bool showPrefabs = true;
    private bool showAtlases = true;
    private bool showScriptables = true;
    private bool showScenes = true;
    private bool showScripts = true;

    // ---------------- UI ----------------
    private SimpleGraphView referencedGraph;
    private SimpleGraphView unusedGraph;

    private VisualElement referencedColumn;
    private VisualElement unusedColumn;
    private VisualElement separator;
    private VisualElement filtersContainer;
    private bool referencedCollapsed = false;
    private bool unusedCollapsed = false;

    // ---------------- STATE ----------------
    private Dictionary<string, List<string>> cachedGraphs;
    private Dictionary<string, ReferenceNode> nodeLookup = new();
    public System.Action<Object[]> OnAssetsDropped;

    // ----------------------------------------------------

    private HashSet<(string root, string reference)> ignoredEdges = new();

    public ReferenceGraphView()
    {
        style.flexGrow = 1;
        style.flexDirection = FlexDirection.Row;

        referencedGraph = CreateGraphView();
        unusedGraph = CreateGraphView();

        referencedColumn = CreateGraphColumn(
            "Referenced",
            referencedGraph,
            collapsed =>
            {
                referencedCollapsed = collapsed;
                UpdateColumnLayout();
            });

        unusedColumn = CreateGraphColumn(
            "Unused",
            unusedGraph,
            collapsed =>
            {
                unusedCollapsed = collapsed;
                UpdateColumnLayout();
            });


        separator = new VisualElement
        {
            style =
            {
                width = 2,
                backgroundColor = new Color(1f, 1f, 1f, 0.15f)
            }
        };

        var contentRow = new VisualElement
        {
            style =
            {
                flexGrow = 1,
                flexDirection = FlexDirection.Row
            }
        };

        contentRow.Add(referencedColumn);
        contentRow.Add(separator);
        contentRow.Add(unusedColumn);
        Add(contentRow);

        unusedColumn.style.display = DisplayStyle.None;
        separator.style.display = DisplayStyle.None;
    }

    // ----------------------------------------------------
    // PUBLIC API
    // ----------------------------------------------------

    public void ClearGraph()
    {
        nodeLookup.Clear();
        referencedGraph.DeleteElements(referencedGraph.graphElements.ToList());
        unusedGraph.DeleteElements(unusedGraph.graphElements.ToList());
    }

    public void BuildSeparate(Dictionary<string, List<string>> graphs)
    {
        cachedGraphs = graphs;
        ClearGraph();

        var referenced = graphs.Where(g => g.Value.Count > 0).ToList();
        var unused = graphs.Where(g => g.Value.Count == 0).ToList();

        bool split = referenced.Count > 0 && unused.Count > 0;

        unusedColumn.style.display = split ? DisplayStyle.Flex : DisplayStyle.None;
        separator.style.display = split ? DisplayStyle.Flex : DisplayStyle.None;

        if (!split)
        {
            DrawGraphs(referencedGraph, graphs);
            return;
        }

        DrawGraphs(referencedGraph, referenced);
        DrawGraphs(unusedGraph, unused);
    }

    public void FocusNode(string assetPath)
    {
        if (!nodeLookup.TryGetValue(assetPath, out var node))
            return;

        var gv = node.GetFirstAncestorOfType<GraphView>();
        gv?.ClearSelection();
        gv?.AddToSelection(node);
        gv?.FrameSelection();
    }

    // ----------------------------------------------------
    // GRAPH BUILDING
    // ----------------------------------------------------

    private void DrawGraphs(
    GraphView view,
    IEnumerable<KeyValuePair<string, List<string>>> data)
    {
        float startX = ROOT_X;
        float x = startX;
        float y = START_Y;

        int col = 0;
        int colorIndex = 0;
        float rowMaxHeight = 0f;

        foreach (var kvp in data)
        {
            // ---- Calculate graph block height ----
            int visibleRefs = kvp.Value.Count(IsVisible);

            float graphHeight =
                70f +                  // root node
                (visibleRefs * Y_SPACING) +
                40f;                   // padding

            // ---- Root node ----
            var root = CreateNode(kvp.Key, RootPalette[colorIndex++ % RootPalette.Length]);
            root.SetPosition(new Rect(x, y, 340, 70));
            view.AddElement(root);
            nodeLookup[kvp.Key] = root;

            // ---- Reference nodes ----
            float ry = y + 80f;

            foreach (var dep in kvp.Value.Where(IsVisible).OrderBy(p => p))
            {
                var node = CreateNode(dep, GetReferenceColor(dep));
                node.SetPosition(new Rect(x + (REF_X - ROOT_X), ry, 340, 70));
                view.AddElement(node);

                var edge = root.OutputPort.ConnectTo(node.InputPort);
                view.AddElement(edge);

                ry += Y_SPACING;
            }

            // ---- Track tallest graph in this row ----
            rowMaxHeight = Mathf.Max(rowMaxHeight, graphHeight);

            // ---- Advance column ----
            col++;
            if (col >= GRAPHS_PER_ROW)
            {
                // Move to next row
                col = 0;
                x = startX;
                y += rowMaxHeight + GRAPH_GAP_Y;
                rowMaxHeight = 0f;
            }
            else
            {
                // Move to next column
                x += GRAPH_WIDTH + GRAPH_GAP_X;
            }
        }
    }

    // ----------------------------------------------------
    // FILTERING
    // ----------------------------------------------------

    private bool IsVisible(string path)
    {
        if (path.EndsWith(".unity")) return showScenes;
        if (path.EndsWith(".prefab")) return showPrefabs;
        if (path.EndsWith(".spriteatlas")) return showAtlases;
        if (path.EndsWith(".cs")) return showScripts;

        if (path.EndsWith(".asset") &&
            AssetDatabase.LoadAssetAtPath<Object>(path) is ScriptableObject)
            return showScriptables;

        return true;
    }

    // ----------------------------------------------------
    // GRAPH COLUMN + FLOATING FILTER
    // ----------------------------------------------------

    private VisualElement CreateGraphColumn(
        string title,
        GraphView graph,
        System.Action<bool> onCollapseChanged)
    {
        bool collapsed = false;

        var root = new VisualElement
        {
            style =
            {
                flexGrow = 1,
                position = Position.Relative
            }
        };

        // ---------- HEADER ----------
        var header = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                backgroundColor = new Color(0, 0, 0, 0.6f),
                paddingLeft = 8,
                paddingRight = 6,
                paddingTop = 4,
                paddingBottom = 4
            }
        };

        var titleLabel = new Label(title)
        {
            style =
            {
                unityFontStyleAndWeight = FontStyle.Bold,
                color = Color.white,
                flexGrow = 1
            }
        };

        VisualElement contentContainer = new VisualElement
        {
            style =
            {
                flexGrow = 1,
                position = Position.Relative
            }
        };

        Button collapseBtn = null;

        collapseBtn = new Button(() =>
        {
            bool collapsedNow = contentContainer.style.display != DisplayStyle.None;

            contentContainer.style.display =
                collapsedNow ? DisplayStyle.None : DisplayStyle.Flex;

            collapseBtn.text = collapsedNow ? "▸" : "▾";

            onCollapseChanged(collapsedNow);
        })
        {
            text = "▾"
        };


        collapseBtn.style.width = 20;
        collapseBtn.style.unityTextAlign = TextAnchor.MiddleCenter;

        header.Add(titleLabel);
        header.Add(collapseBtn);

        // ---------- CONTENT ----------
        contentContainer.Add(graph);
        contentContainer.Add(CreateFloatingFilter());

        root.Add(header);
        root.Add(contentContainer);

        return root;
    }

    private void UpdateColumnLayout()
    {
        // Both visible
        if (!referencedCollapsed && !unusedCollapsed)
        {
            referencedColumn.style.display = DisplayStyle.Flex;
            unusedColumn.style.display = DisplayStyle.Flex;

            referencedColumn.style.flexGrow = 1;
            unusedColumn.style.flexGrow = 1;
            separator.style.display = DisplayStyle.Flex;
            return;
        }

        // Referenced collapsed
        if (referencedCollapsed && !unusedCollapsed)
        {
            referencedColumn.style.flexGrow = 0;
            unusedColumn.style.flexGrow = 1;
            separator.style.display = DisplayStyle.None;
            return;
        }

        // Unused collapsed
        if (!referencedCollapsed && unusedCollapsed)
        {
            unusedColumn.style.flexGrow = 0;
            referencedColumn.style.flexGrow = 1;
            separator.style.display = DisplayStyle.None;
            return;
        }

        // Both collapsed (headers only)
        separator.style.display = DisplayStyle.None;
    }


    private VisualElement CreateFloatingFilter()
    {
        bool collapsed = false;

        var panel = new VisualElement
        {
            style =
            {
                position = Position.Absolute,
                top = 36,
                right = 8,
                backgroundColor = new Color(0, 0, 0, 0.75f),
                paddingLeft = 8,
                paddingRight = 8,
                paddingTop = 6,
                paddingBottom = 6,
                borderTopLeftRadius = 6,
                borderTopRightRadius = 6,
                borderBottomLeftRadius = 6,
                borderBottomRightRadius = 6
            }
        };

        // ----- HEADER -----
        var header = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                marginBottom = 4
            }
        };

        var title = new Label("Filters")
        {
            style =
            {
                unityFontStyleAndWeight = FontStyle.Bold,
                color = Color.white,
                flexGrow = 1
            }
        };

        Button toggleBtn = null;

        toggleBtn = new Button(() =>
        {
            collapsed = !collapsed;
            filtersContainer.style.display =
                collapsed ? DisplayStyle.None : DisplayStyle.Flex;

            toggleBtn.text = collapsed ? "▸" : "▾";
        })
        {
            text = "▾"
        };

        toggleBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
        toggleBtn.style.width = 20;

        header.Add(title);
        header.Add(toggleBtn);

        panel.Add(header);

        // ----- FILTER CONTENT -----
        filtersContainer = new VisualElement();
        filtersContainer.style.flexDirection = FlexDirection.Column;

        filtersContainer.Add(CreateToggle("Prefabs", PrefabColor, v => { showPrefabs = v; Rebuild(); }));
        filtersContainer.Add(CreateToggle("Atlases", SpriteAtlasColor, v => { showAtlases = v; Rebuild(); }));
        filtersContainer.Add(CreateToggle("Scriptables", ScriptableObjColor, v => { showScriptables = v; Rebuild(); }));
        filtersContainer.Add(CreateToggle("Scenes", SceneColor, v => { showScenes = v; Rebuild(); }));
        filtersContainer.Add(CreateToggle("Scripts", ScriptColor, v => { showScripts = v; Rebuild(); }));

        panel.Add(filtersContainer);

        return panel;
    }


    private VisualElement CreateToggle(string label, Color color, System.Action<bool> onChange)
    {
        var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };

        var swatch = new VisualElement
        {
            style =
            {
                width = 10,
                height = 10,
                backgroundColor = color,
                marginRight = 6,
                marginTop = 4
            }
        };

        var toggle = new Toggle(label) { value = true };
        toggle.style.color = Color.white;
        toggle.RegisterValueChangedCallback(e => onChange(e.newValue));

        row.Add(swatch);
        row.Add(toggle);
        return row;
    }

    private void Rebuild()
    {
        if (cachedGraphs != null)
            BuildSeparate(cachedGraphs);
    }

    private SimpleGraphView CreateGraphView()
    {
        var gv = new SimpleGraphView { style = { flexGrow = 1 } };

        gv.RegisterCallback<DragUpdatedEvent>(evt =>
        {
            if (DragAndDrop.objectReferences?.Length > 0)
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            evt.StopPropagation();
        });

        gv.RegisterCallback<DragPerformEvent>(evt =>
        {
            if (DragAndDrop.objectReferences == null)
                return;

            DragAndDrop.AcceptDrag();
            OnAssetsDropped?.Invoke(DragAndDrop.objectReferences);

            evt.StopPropagation();
        });

        gv.SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        gv.AddManipulator(new ContentDragger());
        gv.AddManipulator(new SelectionDragger());
        gv.AddManipulator(new RectangleSelector());

        var grid = new GridBackground();
        gv.Insert(0, grid);
        grid.StretchToParentSize();

        return gv;
    }

    private ReferenceNode CreateNode(string path, Color color)
    {
        return new ReferenceNode(path, color);
    }

    private static Color GetReferenceColor(string path)
    {
        if (path.EndsWith(".unity")) return SceneColor;
        if (path.EndsWith(".prefab")) return PrefabColor;
        if (path.EndsWith(".spriteatlas")) return SpriteAtlasColor;
        if (path.EndsWith(".cs")) return ScriptColor;

        if (path.EndsWith(".asset") &&
            AssetDatabase.LoadAssetAtPath<Object>(path) is ScriptableObject)
            return ScriptableObjColor;

        return UnknownColor;
    }
}

// =======================================================================
// CONCRETE GRAPH VIEW
// =======================================================================

public class SimpleGraphView : GraphView { }

// =======================================================================
// NODE
// =======================================================================

public class ReferenceNode : Node
{
    public Port InputPort;
    public Port OutputPort;

    private readonly string assetPath;
    private IMGUIContainer preview;

    public ReferenceNode(string path, Color color)
    {
        assetPath = path;
        title = System.IO.Path.GetFileName(path);

        titleContainer.style.backgroundColor = color;

        var titleLabel = titleContainer.Q<Label>();
        if (titleLabel != null)
        {
            titleLabel.style.color = Color.white;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 12;
        }

        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(object));
        OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(object));

        inputContainer.Add(InputPort);
        outputContainer.Add(OutputPort);

        extensionContainer.Add(new Label(path));

        preview = new IMGUIContainer(DrawPreview);
        preview.style.display = DisplayStyle.None;
        extensionContainer.Add(preview);

        RegisterCallback<MouseEnterEvent>(_ => preview.style.display = DisplayStyle.Flex);
        RegisterCallback<MouseLeaveEvent>(_ => preview.style.display = DisplayStyle.None);
        RegisterCallback<MouseDownEvent>(OnClick);

        RefreshExpandedState();
        RefreshPorts();
    }

    private void DrawPreview()
    {
        var obj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
        if (!obj) return;

        var tex = AssetPreview.GetAssetPreview(obj) ?? AssetPreview.GetMiniThumbnail(obj);
        if (tex)
            GUILayout.Label(tex, GUILayout.Width(64), GUILayout.Height(64));
    }

    private void OnClick(MouseDownEvent evt)
    {
        if (evt.button != 0) return;

        var obj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
        Selection.activeObject = obj;
        EditorGUIUtility.PingObject(obj);
    }

}
#endif
