using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // The Surface Browser's card icons: a favorite star to click, and a lock on what the browser doesn't change
    static class ChiselSurfaceBrowserIcons
    {
        // the star's click area and the lock's, in the thumbnail's top right and top left corners
        internal const float kStarSize = 18;
        internal const float kLockSize = 16;

        static readonly Color kFavorite  = new(1f, 0.8f, 0.2f);
        static readonly Color kNotYet    = new(1, 1, 1, 0.75f);
        static readonly Color kLock      = new(1, 1, 1, 0.9f);
        static readonly Color kOutline   = new(0.15f, 0.15f, 0.15f, 1);

        internal static Rect StarRect(Rect thumb) => new(thumb.xMax - kStarSize - 2, thumb.y + 2, kStarSize, kStarSize);
        internal static Rect LockRect(Rect thumb) => new(thumb.x + 2, thumb.y + 2, kLockSize, kLockSize);

        // Where a card's label (HS, Decal, Preset) goes: its bottom left corner, the use count in the bottom right
        internal static Rect LabelRect(Rect thumb, float width) => new(thumb.x + 2, thumb.yMax - 16, width, 14);

        // A star to click on a card: filled when a favorite, an outline when not; true when clicked
        internal static bool FavoriteButton(Rect thumb, bool favorite)
        {
            var rect = StarRect(thumb);
            var current = Event.current;
            var hover = rect.Contains(current.mousePosition);
            if (current.type == EventType.Repaint)
                DrawStar(rect, favorite, hover);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            GUI.Label(rect, new GUIContent(string.Empty, favorite ? "Remove from favorites" : "Add to favorites"));
            if (current.type == EventType.MouseDown && current.button == 0 && hover)
            {
                current.Use();
                return true;
            }
            return false;
        }

        static Vector3[] StarPoints(Vector2 center, float outer, float inner)
        {
            var points = new Vector3[10];
            for (int i = 0; i < 10; i++)
            {
                var angle = Mathf.PI * 0.5f + i * Mathf.PI / 5;
                var radius = i % 2 == 0 ? outer : inner;
                points[i] = new Vector3(center.x + Mathf.Cos(angle) * radius, center.y - Mathf.Sin(angle) * radius, 0);
            }
            return points;
        }

        static void DrawStar(Rect rect, bool filled, bool hover)
        {
            var points = StarPoints(rect.center, rect.width * 0.48f, rect.width * 0.2f);
            var color = filled ? kFavorite : kNotYet;
            if (hover) color = Color.Lerp(color, Color.white, 0.35f);
            if (filled)
            {
                Handles.color = color;
                for (int i = 0; i < points.Length; i++)
                    Handles.DrawAAConvexPolygon(rect.center, points[i], points[(i + 1) % points.Length]);
            }
            var loop = new Vector3[points.Length + 1];
            points.CopyTo(loop, 0);
            loop[points.Length] = points[0];
            // a dark outline around it: around the fill, or around the coloured outline of one that isn't a favorite
            Handles.color = kOutline;
            Handles.DrawAAPolyLine(filled ? 1.5f : 3.5f, loop);
            if (!filled)
            {
                Handles.color = color;
                Handles.DrawAAPolyLine(1.5f, loop);
            }
        }

        // A lock in a card's corner, why it is read only in its tooltip
        internal static void LockBadge(Rect thumb, string reason)
        {
            var rect = LockRect(thumb);
            if (Event.current.type == EventType.Repaint)
                DrawLock(rect);
            GUI.Label(rect, new GUIContent(string.Empty, reason));
        }

        // A padlock in a rect
        internal static void DrawLock(Rect rect)
        {
            var body = new Rect(rect.x + rect.width * 0.2f, rect.y + rect.height * 0.45f, rect.width * 0.6f, rect.height * 0.45f);
            EditorGUI.DrawRect(body, kLock);
            // the shackle: half a ring over the body
            var center = new Vector2(rect.center.x, body.y);
            var radius = body.width * 0.32f;
            var arc = new Vector3[9];
            for (int i = 0; i < arc.Length; i++)
            {
                var angle = Mathf.PI * i / (arc.Length - 1);
                arc[i] = new Vector3(center.x + Mathf.Cos(angle) * radius, center.y - Mathf.Sin(angle) * radius, 0);
            }
            Handles.color = kLock;
            Handles.DrawAAPolyLine(2f, arc);
        }
    }
}
