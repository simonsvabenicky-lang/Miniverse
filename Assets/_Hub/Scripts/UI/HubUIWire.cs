using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Miniverse.Hub
{
    /// <summary>
    /// Same helper as Frontline's UIWire (Assets/Games/Frontline/Scripts/Core/UIWire.cs), copied
    /// rather than shared across the game/hub boundary: finds a child by name and hooks its
    /// Button.onClick, since a Button's onClick listener is a C# delegate and delegates don't
    /// survive EditorSceneManager.SaveScene -- structure comes from HubCanvasBuilder, behaviour
    /// is wired here at runtime.
    /// </summary>
    public static class HubUIWire
    {
        public static void Click(GameObject root, string childName, UnityAction onClick)
        {
            Transform t = root.transform.Find(childName);
            Button button = t != null ? t.GetComponent<Button>() : null;
            if (button == null) { Debug.LogWarning($"[Miniverse] {childName} not found under {root.name}."); return; }
            button.onClick.AddListener(onClick);
        }
    }
}
