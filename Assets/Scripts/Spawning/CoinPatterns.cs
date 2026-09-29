using System.Collections.Generic;
using UnityEngine;

namespace JetpackRide.Spawning
{
    public enum CoinPatternKind { Line, Arc, Arrow, Box, Rect }

    public struct CoinPattern
    {
        public CoinPatternKind Kind;
        // Local offsets; the pattern's bounds start at (0, 0).
        public Vector2[] Offsets;
        public Rect Bounds;
    }

    // Pure coin batch shapes. Coins only ever come in batches (never a single coin).
    public static class CoinPatterns
    {
        public const float ShapesUnlockRamp = 0.25f;
        public const float RectUnlockRamp = 0.5f;

        public static CoinPattern Line(int count, float spacing)
        {
            var offsets = new Vector2[Mathf.Max(2, count)];
            for (int i = 0; i < offsets.Length; i++) offsets[i] = new Vector2(i * spacing, 0f);
            return Build(CoinPatternKind.Line, offsets);
        }

        public static CoinPattern Arc(int count, float spacing, float arcHeight)
        {
            count = Mathf.Max(3, count);
            var offsets = new Vector2[count];
            for (int i = 0; i < count; i++)
                offsets[i] = new Vector2(i * spacing, arcHeight * Mathf.Sin(Mathf.PI * i / (count - 1)));
            return Build(CoinPatternKind.Arc, offsets);
        }

        // ">" chevron: two arms of `armLength` coins meeting at a shared tip on the right.
        public static CoinPattern Arrow(int armLength, float spacing)
        {
            armLength = Mathf.Max(2, armLength);
            var offsets = new List<Vector2>(2 * armLength - 1);
            for (int i = 0; i < armLength; i++)
            {
                float rise = (armLength - 1 - i) * spacing;
                offsets.Add(new Vector2(i * spacing, rise));
                if (rise > 0f) offsets.Add(new Vector2(i * spacing, -rise));
            }
            return Build(CoinPatternKind.Arrow, offsets.ToArray());
        }

        // Hollow cols x rows square outline.
        public static CoinPattern Box(int cols, int rows, float spacing)
        {
            cols = Mathf.Max(2, cols);
            rows = Mathf.Max(2, rows);
            var offsets = new List<Vector2>();
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                    if (c == 0 || c == cols - 1 || r == 0 || r == rows - 1)
                        offsets.Add(new Vector2(c * spacing, r * spacing));
            return Build(CoinPatternKind.Box, offsets.ToArray());
        }

        // Filled cols x rows grid.
        public static CoinPattern Rect(int cols, int rows, float spacing)
        {
            cols = Mathf.Max(1, cols);
            rows = Mathf.Max(1, rows);
            if (cols * rows < 2) cols = 2;
            var offsets = new Vector2[cols * rows];
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                    offsets[c * rows + r] = new Vector2(c * spacing, r * spacing);
            return Build(CoinPatternKind.Rect, offsets);
        }

        // Line/arc early; arrow and box from ShapesUnlockRamp, filled rect from RectUnlockRamp.
        // random01 picks the kind uniformly among those unlocked; size grows with rampT.
        public static CoinPattern Pick(float rampT, float random01, float spacing, float arcHeight)
        {
            int unlocked = rampT < ShapesUnlockRamp ? 2 : rampT < RectUnlockRamp ? 4 : 5;
            var kind = (CoinPatternKind)Mathf.Min(unlocked - 1, (int)(random01 * unlocked));
            int Size(int min, int max) => Mathf.RoundToInt(Mathf.Lerp(min, max, Mathf.Clamp01(rampT)));

            return kind switch
            {
                CoinPatternKind.Line => Line(Size(4, 8), spacing),
                CoinPatternKind.Arc => Arc(Size(5, 9), spacing, arcHeight),
                CoinPatternKind.Arrow => Arrow(Size(3, 5), spacing),
                CoinPatternKind.Box => Box(Size(3, 5), Size(3, 4), spacing),
                _ => Rect(Size(3, 6), Size(2, 3), spacing),
            };
        }

        // Normalises offsets so the bounds start at (0, 0).
        private static CoinPattern Build(CoinPatternKind kind, Vector2[] offsets)
        {
            Vector2 min = offsets[0], max = offsets[0];
            foreach (var o in offsets)
            {
                min = Vector2.Min(min, o);
                max = Vector2.Max(max, o);
            }
            for (int i = 0; i < offsets.Length; i++) offsets[i] -= min;
            return new CoinPattern { Kind = kind, Offsets = offsets, Bounds = new UnityEngine.Rect(Vector2.zero, max - min) };
        }
    }
}
