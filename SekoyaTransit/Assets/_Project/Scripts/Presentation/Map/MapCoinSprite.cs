#nullable enable
using UnityEngine;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// Draws the small gold coin of the cash popups at runtime (Issue #284), so no art asset is needed.
    /// </summary>
    public static class MapCoinSprite
    {
        private const int Size = 32;

        /// <summary>
        /// Creates a gold coin sprite with a darker rim and a lighter inner disc.
        /// </summary>
        public static Sprite Create()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "CashCoin",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[Size * Size];
            var rim = new Color32(150, 100, 10, 255);
            var gold = new Color32(250, 200, 40, 255);
            var shine = new Color32(255, 235, 140, 255);
            float center = (Size - 1) * 0.5f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                    Color32 color = new Color32(0, 0, 0, 0);
                    if (distance <= 15f)
                    {
                        color = distance > 12.5f ? rim : (distance < 7f && dx + dy < 0f ? shine : gold);
                    }

                    pixels[(y * Size) + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }
    }
}
