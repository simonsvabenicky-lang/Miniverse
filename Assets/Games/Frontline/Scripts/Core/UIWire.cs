using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Frontline
{
    /// <summary>
    /// Finds a Canvas child by name and hooks its Button.onClick -- the runtime half of every
    /// CanvasBuilder screen. A Button's onClick listener is a C# delegate, and delegates don't
    /// survive EditorSceneManager.SaveScene, so structure comes from the generated scene and
    /// behaviour is wired here, at Awake, every time the scene loads.
    /// </summary>
    public static class UIWire
    {
        public static void Click(GameObject root, string childName, UnityAction onClick)
        {
            Transform t = root.transform.Find(childName);
            Button button = t != null ? t.GetComponent<Button>() : null;
            if (button == null) { Debug.LogWarning($"[Frontline] {childName} not found under {root.name}."); return; }
            button.onClick.AddListener(onClick);
        }
    }
}
