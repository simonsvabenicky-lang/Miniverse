using UnityEngine;
using UnityEngine.UI;

namespace Frontline
{
    /// <summary>
    /// Two sprite references baked in by CanvasBuilder (SerializedObject, at scene-build time),
    /// so flipping a checkbox at runtime never needs AssetDatabase -- which is Editor-only and
    /// would silently return null in an actual Windows/Android build. Same "structure/data from
    /// the generator, behaviour by name at runtime" split as everything else CanvasBuilder makes.
    /// </summary>
    public class CheckboxToggle : MonoBehaviour
    {
        [SerializeField] Sprite _off;
        [SerializeField] Sprite _on;
        Image _image;

        void Awake() => _image = GetComponent<Image>();

        public void SetOn(bool on)
        {
            if (_image == null) _image = GetComponent<Image>();
            _image.sprite = on ? _on : _off;
        }
    }
}
