#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class FilteredAssetListWindow : EditorWindow
{
    private List<Object> results;
    private Vector2 scroll;
    private string titleText;

    // 🔍 Search state
    private string searchQuery = "";

    public static void Show(string title, List<Object> assets)
    {
        var window = GetWindow<FilteredAssetListWindow>(true, title);
        window.results = assets;
        window.titleText = title;
        window.minSize = new Vector2(300, 400);
        window.Show();
    }

    private void OnGUI()
    {
        // ---------- Title ----------
        GUILayout.Label(titleText, EditorStyles.boldLabel);
        GUILayout.Space(4);

        // ---------- Search Bar ----------
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            searchQuery = GUILayout.TextField(
                searchQuery,
                GUI.skin.FindStyle("ToolbarSearchTextField"));

            if (GUILayout.Button(
                    "",
                    GUI.skin.FindStyle("ToolbarSearchCancelButton")))
            {
                searchQuery = "";
                GUI.FocusControl(null);
            }
        }

        GUILayout.Space(4);

        // ---------- Results ----------
        scroll = EditorGUILayout.BeginScrollView(scroll);

        foreach (var obj in results)
        {
            if (obj == null)
                continue;

            // 🔍 Apply search filter
            if (!string.IsNullOrEmpty(searchQuery))
            {
                string name = obj.name;
                string path = AssetDatabase.GetAssetPath(obj);

                if (!name.Contains(searchQuery, System.StringComparison.OrdinalIgnoreCase) &&
                    !path.Contains(searchQuery, System.StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            Rect row = EditorGUILayout.BeginHorizontal();

            EditorGUILayout.ObjectField(obj, typeof(Object), false);

            // Click row → Ping
            if (Event.current.type == EventType.MouseDown &&
                row.Contains(Event.current.mousePosition))
            {
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
                Event.current.Use();
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }
}
#endif
