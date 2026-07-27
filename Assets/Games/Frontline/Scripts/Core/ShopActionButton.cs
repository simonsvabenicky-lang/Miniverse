using UnityEngine;
using UnityEngine.UI;

namespace Frontline
{
    /// <summary>
    /// A Shop row's BUY/UPGRADE button. Two sprites baked in by CanvasBuilder (SerializedObject,
    /// at scene-build time) -- same reason as CheckboxToggle: AssetDatabase is Editor-only and
    /// would silently return null in an actual build, so the sprite swap can't be done by
    /// loading a path at runtime.
    /// </summary>
    public class ShopActionButton : MonoBehaviour
    {
        [SerializeField] Sprite _affordable;
        [SerializeField] Sprite _unaffordable;
        Image _image;

        void Awake() => _image = GetComponent<Image>();

        public void SetAffordable(bool canAfford)
        {
            if (_image == null) _image = GetComponent<Image>();
            _image.sprite = canAfford ? _affordable : _unaffordable;
        }
    }
}
