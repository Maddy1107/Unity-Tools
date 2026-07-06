#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;

[InitializeOnLoad]
public static class DependencyCache
{
    private static readonly Dictionary<string, string[]> cache = new();

    static DependencyCache()
    {
        AssetPostprocessorHook.OnAssetsChanged += Clear;
    }

    public static string[] Get(string path)
    {
        if (!cache.TryGetValue(path, out var deps))
        {
            deps = AssetDatabase.GetDependencies(path, false);
            cache[path] = deps;
        }
        return deps;
    }

    public static void Clear()
    {
        cache.Clear();
    }

    private class AssetPostprocessorHook : AssetPostprocessor
    {
        public static event System.Action OnAssetsChanged;

        static void OnPostprocessAllAssets(
            string[] imported,
            string[] deleted,
            string[] moved,
            string[] movedFrom)
        {
            OnAssetsChanged?.Invoke();
        }
    }
}
#endif
