#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using System;
using System.Linq;

public class GenericEnumPickerWindow : EditorWindow
{
    private string search = "";
    private Vector2 scroll;

    private Array enumValues;

    private Action<Enum> onSelect;

    public static void Open<T>(
    T current,
    Action<T> onSelect,
    string title = null,
    Vector2? size = null)
    where T : Enum
    {
        var window =
            CreateInstance<GenericEnumPickerWindow>();

        window.enumValues =
            Enum.GetValues(typeof(T));

        window.onSelect =
            value =>
            {
                onSelect?.Invoke(
                    (T)Enum.Parse(
                        typeof(T),
                        value.ToString()));
            };

        window.titleContent =
            new GUIContent(
                string.IsNullOrEmpty(title)
                    ? $"Select {typeof(T).Name}"
                    : title);

        Vector2 windowSize =
            size ?? new Vector2(420, 520);

        Vector2 mousePosition =
            GUIUtility.GUIToScreenPoint(
                Event.current != null
                    ? Event.current.mousePosition
                    : Vector2.zero);

        window.position =
            new Rect(
                mousePosition.x,
                mousePosition.y,
                windowSize.x,
                windowSize.y);

        window.ShowUtility();
    }

    private void OnGUI()
    {
        DrawSearchBar();
        DrawEnumList();
    }

    private void DrawSearchBar()
    {
        GUI.SetNextControlName("SearchBox");

        search =
            EditorGUILayout.TextField(
                "Search",
                search);

        EditorGUI.FocusTextInControl(
            "SearchBox");
    }

    private void DrawEnumList()
    {
        scroll =
            EditorGUILayout.BeginScrollView(
                scroll);

        foreach (Enum value in enumValues)
        {
            string name =
                value.ToString();

            if (!string.IsNullOrEmpty(search) &&
                !name.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (GUILayout.Button(
                    name,
                    EditorStyles.miniButton))
            {
                onSelect?.Invoke(value);

                Close();

                GUIUtility.ExitGUI();
            }
        }

        EditorGUILayout.EndScrollView();
    }
}

#endif