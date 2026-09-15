using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace TillWinter.Unity
{
    /// <summary>Node icons from the packed atlas (Resources/NodeIcons, Kenney Game Icons). Keys come from <c>SkillNode.IconKey</c>.</summary>
    public static class NodeIcons
    {
        private static SpriteAtlas _atlas;
        private static bool _tried;

        public static SpriteAtlas Atlas
        {
            get
            {
                if (_atlas == null && !_tried)
                {
                    _tried = true;
                    _atlas = Resources.Load<SpriteAtlas>("NodeIcons");
                    if (_atlas == null) Debug.LogWarning("[NodeIcons] Resources/NodeIcons atlas missing; run art-setup.bat");
                }
                return _atlas;
            }
        }

        public static Sprite Get(string key)
        {
            if (string.IsNullOrEmpty(key) || Atlas == null) return null;
            return Atlas.GetSprite(key);
        }

        /// <summary>An Image showing the icon; falls back to a plain circle when the key is missing so the tree never shows a blank.</summary>
        public static Image Image(Transform parent, string key, Color color)
        {
            var go = new GameObject("Icon", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            var sprite = Get(key);
            img.sprite = sprite != null ? sprite : UiKit.Circle;
            img.color = color;
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }
    }
}
