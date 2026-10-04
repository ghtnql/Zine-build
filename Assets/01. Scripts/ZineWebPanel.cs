using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Rounded-rectangle MaskableGraphic matching the existing web ranking style.
/// Fill: vertical gradient TopColor -> BottomColor. Plus a uniform-thickness border ring.
/// Runtime RectTransform size changes re-render via OnRectTransformDimensionsChange.
/// Caller may set raycastTarget = false. Graphic.color acts as a multiplicative tint.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class ZineWebPanel : MaskableGraphic
{
    public Color TopColor = new Color(0x14 / 255f, 0x24 / 255f, 0x17 / 255f, 1f);
    public Color BottomColor = new Color(0x05 / 255f, 0x0F / 255f, 0x09 / 255f, 1f);
    public Color BorderColor = new Color(0.2f, 1f, 0.2f, 0.3f);
    public float Radius = 22f;
    public float BorderWidth = 1f;

    private const int SegmentsPerCorner = 8;

 #if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        Radius = Mathf.Max(0f, Radius);
        BorderWidth = Mathf.Max(0f, BorderWidth);
        SetVerticesDirty();
    }

 #endif

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        if (IsActive())
            SetVerticesDirty();
    }

    public void SetStyle(Color top, Color bottom, Color border, float radius, float borderWidth)
    {
        TopColor = top;
        BottomColor = bottom;
        BorderColor = border;
        Radius = Mathf.Max(0f, radius);
        BorderWidth = Mathf.Max(0f, borderWidth);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = GetPixelAdjustedRect();
        float w = rect.width;
        float h = rect.height;
        if (w <= 0f || h <= 0f)
            return;

        float outerR = Mathf.Clamp(Radius, 0f, Mathf.Min(w, h) * 0.5f);
        float bw = Mathf.Max(0f, BorderWidth);
        // Clamp border so an inner contour can still exist (or fill-only if too thick).
        bw = Mathf.Min(bw, Mathf.Min(w, h) * 0.5f);

        Color tint = color;
        Color top = Multiply(TopColor, tint);
        Color bottom = Multiply(BottomColor, tint);
        Color border = Multiply(BorderColor, tint);

        List<Vector2> outer = GetContour(rect, outerR, SegmentsPerCorner);
        int n = outer.Count;
        if (n < 3)
            return;

        // Fill: triangle fan from rect center, per-vertex vertical gradient.
        UIVertex centerVert = UIVertex.simpleVert;
        centerVert.position = new Vector2(rect.center.x, rect.center.y);
        centerVert.color = GradientAt(rect, centerVert.position.y, top, bottom);
        centerVert.uv0 = new Vector2(0.5f, 0.5f);
        vh.AddVert(centerVert);
        int centerIndex = 0;

        List<int> ringIndices = new List<int>(n);
        for (int i = 0; i < n; i++)
        {
            UIVertex v = UIVertex.simpleVert;
            v.position = outer[i];
            v.color = GradientAt(rect, outer[i].y, top, bottom);
            v.uv0 = new Vector2((outer[i].x - rect.xMin) / w, (outer[i].y - rect.yMin) / h);
            vh.AddVert(v);
            ringIndices.Add(centerIndex + 1 + i);
        }

        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            vh.AddTriangle(centerIndex, ringIndices[i], ringIndices[next]);
        }

        // Border ring: quads between outer contour and inset inner contour.
        if (bw > 0f && border.a > 0f)
        {
            Rect innerRect = new Rect(rect.x + bw, rect.y + bw, w - bw * 2f, h - bw * 2f);
            if (innerRect.width > 0f && innerRect.height > 0f)
            {
                float innerR = Mathf.Max(0f, outerR - bw);
                innerR = Mathf.Clamp(innerR, 0f, Mathf.Min(innerRect.width, innerRect.height) * 0.5f);
                List<Vector2> inner = GetContour(innerRect, innerR, SegmentsPerCorner);
                if (inner.Count == n)
                {
                    for (int i = 0; i < n; i++)
                    {
                        int next = (i + 1) % n;
                        UIVertex o0 = UIVertex.simpleVert;
                        o0.position = outer[i];
                        o0.color = border;
                        o0.uv0 = Vector2.zero;
                        UIVertex o1 = UIVertex.simpleVert;
                        o1.position = outer[next];
                        o1.color = border;
                        o1.uv0 = Vector2.zero;
                        UIVertex i1 = UIVertex.simpleVert;
                        i1.position = inner[next];
                        i1.color = border;
                        i1.uv0 = Vector2.zero;
                        UIVertex i0 = UIVertex.simpleVert;
                        i0.position = inner[i];
                        i0.color = border;
                        i0.uv0 = Vector2.zero;

                        int baseIndex = vh.currentVertCount;
                        vh.AddVert(o0);
                        vh.AddVert(o1);
                        vh.AddVert(i1);
                        vh.AddVert(i0);
                        vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
                        vh.AddTriangle(baseIndex, baseIndex + 2, baseIndex + 3);
                    }
                }
            }
        }
    }

    private static Color GradientAt(Rect rect, float y, Color top, Color bottom)
    {
        float t = rect.height > 0f ? (y - rect.yMin) / rect.height : 0.5f;
        t = Mathf.Clamp01(t);
        // Slight diagonal is acceptable; keep pure vertical for predictable stops.
        return Color.Lerp(bottom, top, t);
    }

    private static Color Multiply(Color a, Color b)
    {
        return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
    }

    private static List<Vector2> GetContour(Rect rect, float radius, int segmentsPerCorner)
    {
        List<Vector2> pts = new List<Vector2>(segmentsPerCorner * 4);
        if (radius <= 0f)
        {
            pts.Add(new Vector2(rect.xMin, rect.yMin));
            pts.Add(new Vector2(rect.xMax, rect.yMin));
            pts.Add(new Vector2(rect.xMax, rect.yMax));
            pts.Add(new Vector2(rect.xMin, rect.yMax));
            return pts;
        }

        Vector2[] centers = new Vector2[4];
        centers[0] = new Vector2(rect.xMin + radius, rect.yMin + radius); // bottom-left
        centers[1] = new Vector2(rect.xMax - radius, rect.yMin + radius); // bottom-right
        centers[2] = new Vector2(rect.xMax - radius, rect.yMax - radius); // top-right
        centers[3] = new Vector2(rect.xMin + radius, rect.yMax - radius); // top-left
        float[] startDeg = { 180f, 270f, 0f, 90f };

        for (int c = 0; c < 4; c++)
        {
            for (int k = 0; k < segmentsPerCorner; k++)
            {
                float deg = startDeg[c] + (k / (float)segmentsPerCorner) * 90f;
                float rad = deg * Mathf.Deg2Rad;
                pts.Add(new Vector2(
                    centers[c].x + Mathf.Cos(rad) * radius,
                    centers[c].y + Mathf.Sin(rad) * radius));
            }
        }
        return pts;
    }
}
