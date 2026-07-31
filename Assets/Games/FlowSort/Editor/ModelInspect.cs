using UnityEditor;
using UnityEngine;

namespace FlowSort.EditorTools
{
    /// <summary>
    /// Dumps mesh bounds and material names for imported FBX models. Exists because neither the
    /// correct stand-up rotation for a top-down-authored asset nor the submesh->material mapping
    /// can be guessed reliably: the long axis of the bounds tells you which way a barrel points,
    /// and the material names are what the track builder keys its colours off.
    /// </summary>
    public static class ModelInspect
    {
        static string[] Models => new[]
        {
            ProjectPaths.TowerDefense + "/weapon-turret.fbx",
            ProjectPaths.TowerDefense + "/weapon-cannon.fbx",
            ProjectPaths.RacingKit + "/roadStraight.fbx",
            ProjectPaths.RacingKit + "/roadCornerSmall.fbx",
            ProjectPaths.RacingKit + "/roadCornerSmallBorder.fbx",
            ProjectPaths.RacingKit + "/overheadRoundColored.fbx",
            ProjectPaths.RacingKit + "/barrierRed.fbx",
            ProjectPaths.RacingKit + "/barrierWhite.fbx",
        };

        [MenuItem("FlowSort/Log Model Bounds")]
        public static void LogBounds()
        {
            foreach (var path in Models)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null)
                {
                    Debug.LogError($"[FlowSort] missing {path}");
                    continue;
                }

                var combined = new Bounds();
                bool first = true;

                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.sharedMesh == null) continue;
                    var b = mf.sharedMesh.bounds;
                    if (first) { combined = b; first = false; }
                    else combined.Encapsulate(b);

                    var r = mf.GetComponent<MeshRenderer>();
                    string mats = r == null ? "(no renderer)" : Join(r.sharedMaterials);
                    Debug.Log($"[FlowSort]   {System.IO.Path.GetFileName(path)} / {mf.name}: " +
                              $"submeshes={mf.sharedMesh.subMeshCount} mats=[{mats}] " +
                              $"hasUV={mf.sharedMesh.uv.Length > 0} hasColors={mf.sharedMesh.colors.Length > 0}");
                }

                Debug.Log($"[FlowSort] {System.IO.Path.GetFileName(path)}  " +
                          $"center={combined.center}  size={combined.size}  " +
                          $"longestAxis={LongestAxis(combined.size)}");
            }
        }

        static string Join(Material[] mats)
        {
            if (mats == null || mats.Length == 0) return "";
            var parts = new string[mats.Length];
            for (int i = 0; i < mats.Length; i++)
                parts[i] = mats[i] == null ? "<null>" : $"{mats[i].name}:{mats[i].shader?.name}";
            return string.Join(", ", parts);
        }

        static string LongestAxis(Vector3 s)
        {
            if (s.x >= s.y && s.x >= s.z) return $"X ({s.x:F2})";
            if (s.y >= s.x && s.y >= s.z) return $"Y ({s.y:F2})";
            return $"Z ({s.z:F2})";
        }
    }
}
