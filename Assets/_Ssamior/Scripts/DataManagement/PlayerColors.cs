using UnityEngine;

namespace Game
{
    /// <summary>
    /// Shared palette of selectable player colors. The index of an entry is the
    /// value that gets persisted (PlayerPrefs) and synced (session player
    /// property), so entries must never be reordered or removed.
    /// </summary>
    public static class PlayerColors
    {
        public readonly struct Entry
        {
            public readonly string Name;
            public readonly Color Color;

            public Entry(string name, Color color)
            {
                Name = name;
                Color = color;
            }
        }

        // Order is significant: the index is the persisted/synced color id.
        public static readonly Entry[] All =
        {
            new("Red",    new Color(0.90f, 0.22f, 0.22f)),
            new("Blue",   new Color(0.22f, 0.47f, 0.90f)),
            new("Green",  new Color(0.27f, 0.75f, 0.32f)),
            new("Yellow", new Color(0.95f, 0.85f, 0.22f)),
            new("Orange", new Color(0.95f, 0.55f, 0.16f)),
            new("Purple", new Color(0.60f, 0.32f, 0.82f)),
            new("Pink",   new Color(0.95f, 0.47f, 0.72f)),
            new("Cyan",   new Color(0.22f, 0.80f, 0.85f)),
        };

        public static int Count => All.Length;

        public static int Clamp(int index) => Mathf.Clamp(index, 0, All.Length - 1);

        public static Color ColorAt(int index) => All[Clamp(index)].Color;

        public static string NameAt(int index) => All[Clamp(index)].Name;
    }
}
