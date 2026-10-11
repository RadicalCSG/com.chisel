using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using UnityEngine;

namespace Chisel.Core
{
    // Layouts written by other tools: Valve's .rect files (Hammer++, Strata Hammer, Source 2), SVG (drawing
    // programs) and colour masks (one flat colour per tile). Each replaces the layout's tiles and finds their lines.
    public sealed partial class ChiselHotspotLayout
    {
        // Valve's .rect: "Rectangles" { "rectangle" { "min" "x y" "max" "x y" [rotate|reflect|alt|tile 1] } ... },
        // pixels from the top left. It has no size: size is the texture's.
        public static bool TryReadRect(string text, Vector2Int size, ChiselHotspotLayout layout, out string error)
        {
            if (size.x <= 0 || size.y <= 0) { error = "a .rect file has no size: the layout needs its texture or a size first"; return false; }
            var tokens = KeyValueTokens(text);
            var tiles = new List<HotspotTile>();
            int depth = 0;
            Dictionary<string, string> rectangle = null;
            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (token == "{") { depth++; continue; }
                if (token == "}")
                {
                    depth--;
                    if (depth == 1 && rectangle != null)
                    {
                        if (!RectTile(rectangle, size, tiles.Count + 1, out var tile, out error))
                            return false;
                        tiles.Add(tile);
                        rectangle = null;
                    }
                    continue;
                }
                if (depth == 1 && string.Equals(token, "rectangle", StringComparison.OrdinalIgnoreCase))
                {
                    rectangle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    continue;
                }
                if (depth == 2 && rectangle != null && i + 1 < tokens.Count && tokens[i + 1] != "{" && tokens[i + 1] != "}")
                {
                    rectangle[token] = tokens[i + 1];
                    i++;
                }
            }
            if (tiles.Count == 0) { error = "no rectangles"; return false; }
            layout.size  = size;
            layout.tiles = tiles;
            layout.BuildCuts(out _);
            error = null;
            return true;
        }

        static bool RectTile(Dictionary<string, string> keys, Vector2Int size, int id, out HotspotTile tile, out string error)
        {
            tile = default;
            if (!keys.TryGetValue("min", out var minText) || !keys.TryGetValue("max", out var maxText) ||
                !TryPair(minText, out var min) || !TryPair(maxText, out var max) || max.x <= min.x || max.y <= min.y)
            {
                error = $"rectangle {id} has no min and max";
                return false;
            }
            bool On(string key) => keys.TryGetValue(key, out var value) && value.Trim() != "0";
            var flags = HotspotTileFlags.None;
            if (On("rotate"))  flags |= HotspotTileFlags.AllowRotation;
            if (On("reflect")) flags |= HotspotTileFlags.AllowMirror;
            if (On("alt"))     flags |= HotspotTileFlags.Alternate;
            if (On("tile"))
            {
                // it tiles along an axis on which it reaches both sides of the texture
                if (min.x <= 0 && max.x >= size.x) flags |= HotspotTileFlags.RepeatU;
                if (min.y <= 0 && max.y >= size.y) flags |= HotspotTileFlags.RepeatV;
            }
            tile = new HotspotTile { id = id, name = "R" + id, flags = flags, points = RectanglePoints(new RectInt(min.x, min.y, max.x - min.x, max.y - min.y)) };
            error = null;
            return true;
        }

        static bool TryPair(string text, out Vector2Int pair)
        {
            pair = default;
            var parts = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 ||
                !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                return false;
            pair = new Vector2Int(Mathf.RoundToInt(x), Mathf.RoundToInt(y));
            return true;
        }

        // Quoted strings, bare words and braces; // comments skipped
        static List<string> KeyValueTokens(string text)
        {
            var tokens = new List<string>();
            int i = 0;
            while (i < text.Length)
            {
                var c = text[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }
                if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
                {
                    while (i < text.Length && text[i] != '\n') i++;
                    continue;
                }
                if (c == '{' || c == '}') { tokens.Add(c.ToString()); i++; continue; }
                if (c == '"')
                {
                    var end = text.IndexOf('"', i + 1);
                    if (end < 0) end = text.Length;
                    tokens.Add(text.Substring(i + 1, end - i - 1));
                    i = end + 1;
                    continue;
                }
                var start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '{' && text[i] != '}' && text[i] != '"') i++;
                tokens.Add(text.Substring(start, i - start));
            }
            return tokens;
        }

        // An SVG path's loops of straight lines (absolute and relative M, L, H, V, Z), in pixels
        static bool TryReadPath(string d, Vector2 origin, out List<Vector2Int[]> loops, out string error)
        {
            var found = new List<Vector2Int[]>();
            loops = found;
            error = null;
            var loop = new List<Vector2Int>();
            var current = Vector2.zero; var start = Vector2.zero;
            var command = ' ';
            var i = 0;
            void Close()
            {
                if (loop.Count >= 3)
                {
                    if (loop[0] == loop[loop.Count - 1])
                        loop.RemoveAt(loop.Count - 1);
                    if (loop.Count >= 3)
                        found.Add(loop.ToArray());
                }
                loop.Clear();
            }
            bool Number(out float value)
            {
                value = 0;
                while (i < d.Length && (char.IsWhiteSpace(d[i]) || d[i] == ','))
                    i++;
                var begin = i;
                if (i < d.Length && (d[i] == '-' || d[i] == '+')) i++;
                while (i < d.Length && (char.IsDigit(d[i]) || d[i] == '.')) i++;
                if (i < d.Length && (d[i] == 'e' || d[i] == 'E'))
                {
                    i++;
                    if (i < d.Length && (d[i] == '-' || d[i] == '+')) i++;
                    while (i < d.Length && char.IsDigit(d[i])) i++;
                }
                return i > begin && float.TryParse(d.Substring(begin, i - begin), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            }
            void Add(Vector2 point)
            {
                current = point;
                loop.Add(Vector2Int.RoundToInt(point - origin));
            }
            while (true)
            {
                while (i < d.Length && (char.IsWhiteSpace(d[i]) || d[i] == ','))
                    i++;
                if (i >= d.Length)
                    break;
                if (char.IsLetter(d[i]))
                {
                    command = d[i++];
                    if (command == 'Z' || command == 'z')
                    {
                        Close();
                        current = start;
                        continue;
                    }
                    if ("MmLlHhVv".IndexOf(command) < 0)
                    {
                        error = $"has '{command}': only straight lines (M, L, H, V, Z) are read";
                        return false;
                    }
                }
                var relative = char.IsLower(command);
                switch (char.ToUpperInvariant(command))
                {
                    case 'M':
                    {
                        if (!Number(out var x) || !Number(out var y)) { error = "has a broken M"; return false; }
                        Close();
                        start = relative ? current + new Vector2(x, y) : new Vector2(x, y);
                        Add(start);
                        command = relative ? 'l' : 'L';  // further pairs are lines
                        break;
                    }
                    case 'L':
                    {
                        if (!Number(out var x) || !Number(out var y)) { error = "has a broken L"; return false; }
                        Add(relative ? current + new Vector2(x, y) : new Vector2(x, y));
                        break;
                    }
                    case 'H':
                    {
                        if (!Number(out var x)) { error = "has a broken H"; return false; }
                        Add(new Vector2(relative ? current.x + x : x, current.y));
                        break;
                    }
                    case 'V':
                    {
                        if (!Number(out var y)) { error = "has a broken V"; return false; }
                        Add(new Vector2(current.x, relative ? current.y + y : y));
                        break;
                    }
                    default:
                        error = "does not start with M";
                        return false;
                }
            }
            Close();
            if (loops.Count == 0) { error = "has no closed loop of three points or more"; return false; }
            return true;
        }

        // SVG: every rect, every polygon (one that is not a triangle or an upright rectangle a shaped tile), and every path
        // of straight lines (M, L, H, V, Z; its first loop the outline, the others holes: a shaped tile), in user units as
        // pixels;
        // the size from the viewBox, or width and height. A tile's name is its id (or inkscape:label), its tags its
        // class (or data-tags); "border-N" in them is its border; data-inside-corners names the tile with its inside corner
        // pieces. Elements under a transform are refused.
        public static bool TryReadSvg(string text, ChiselHotspotLayout layout, out string error)
        {
            var document = new XmlDocument { XmlResolver = null };
            try { document.LoadXml(text); }
            catch (Exception exception) { error = exception.Message; return false; }
            var root = document.DocumentElement;
            if (root == null || root.LocalName != "svg") { error = "not an SVG file"; return false; }

            var size = Vector2Int.zero;
            var viewBox = Numbers(root.GetAttribute("viewBox"));
            if (viewBox.Count == 4)
                size = new Vector2Int(Mathf.RoundToInt(viewBox[2]), Mathf.RoundToInt(viewBox[3]));
            else if (TryLength(root.GetAttribute("width"), out var width) && TryLength(root.GetAttribute("height"), out var height))
                size = new Vector2Int(Mathf.RoundToInt(width), Mathf.RoundToInt(height));
            if (size.x <= 0 || size.y <= 0) { error = "the SVG has no size (viewBox, or width and height)"; return false; }
            var origin = viewBox.Count == 4 ? new Vector2(viewBox[0], viewBox[1]) : Vector2.zero;

            var tiles = new List<HotspotTile>();
            var links = new List<string>();
            foreach (XmlNode node in root.SelectNodes("//*"))
            {
                if (node is not XmlElement element || (element.LocalName != "rect" && element.LocalName != "polygon" && element.LocalName != "path"))
                    continue;
                for (var parent = element; parent != null; parent = parent.ParentNode as XmlElement)
                {
                    if (parent.HasAttribute("transform"))
                    {
                        error = $"<{element.LocalName} {NameOf(element)}> is transformed; apply the transforms first";
                        return false;
                    }
                }
                Vector2Int[] points;
                HotspotTileHole[] holes = null;
                if (element.LocalName == "path")
                {
                    if (!TryReadPath(element.GetAttribute("d"), origin, out var loops, out var pathError))
                    {
                        error = $"<path {NameOf(element)}> {pathError}";
                        return false;
                    }
                    points = loops[0];
                    if (loops.Count > 1)
                    {
                        holes = new HotspotTileHole[loops.Count - 1];
                        for (int i = 1; i < loops.Count; i++)
                            holes[i - 1] = new HotspotTileHole { points = loops[i] };
                    }
                } else if (element.LocalName == "rect")
                {
                    if (!TryLength(element.GetAttribute("width"), out var w) || !TryLength(element.GetAttribute("height"), out var h) || w <= 0 || h <= 0)
                        continue;
                    TryLength(element.GetAttribute("x"), out var x);
                    TryLength(element.GetAttribute("y"), out var y);
                    var min = Vector2Int.RoundToInt(new Vector2(x, y) - origin);
                    var max = Vector2Int.RoundToInt(new Vector2(x + w, y + h) - origin);
                    points = RectanglePoints(new RectInt(min.x, min.y, max.x - min.x, max.y - min.y));
                } else
                {
                    var numbers = Numbers(element.GetAttribute("points"));
                    if (numbers.Count >= 2 && numbers[0] == numbers[numbers.Count - 2] && numbers[1] == numbers[numbers.Count - 1])
                        numbers.RemoveRange(numbers.Count - 2, 2);
                    if (numbers.Count < 6 || numbers.Count % 2 != 0)
                    {
                        error = $"<polygon {NameOf(element)}> has fewer than three points";
                        return false;
                    }
                    points = new Vector2Int[numbers.Count / 2];
                    for (int i = 0; i < points.Length; i++)
                        points[i] = Vector2Int.RoundToInt(new Vector2(numbers[i * 2], numbers[i * 2 + 1]) - origin);
                    if (points.Length == 4)
                    {
                        // an upright rectangle as a rectangle; any other quadrilateral a shaped tile
                        var bounds = BoundsOf(points);
                        var upright = true;
                        foreach (var p in points)
                            upright &= (p.x == bounds.xMin || p.x == bounds.xMax) && (p.y == bounds.yMin || p.y == bounds.yMax);
                        if (upright)
                            points = RectanglePoints(bounds);
                    }
                }
                var tags = (element.GetAttribute("class") + " " + element.GetAttribute("data-tags")).Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                var border = 0;
                foreach (var tag in tags)
                {
                    if (tag.StartsWith("border-", StringComparison.OrdinalIgnoreCase))
                        int.TryParse(tag.Substring(7), NumberStyles.Integer, CultureInfo.InvariantCulture, out border);
                }
                var id = tiles.Count + 1;
                var name = NameOf(element);
                tiles.Add(new HotspotTile { id = id, name = string.IsNullOrEmpty(name) ? "S" + id : name, points = points, holes = holes, flags = FlagsOf(tags), border = Math.Max(0, border) });
                links.Add(element.GetAttribute("data-inside-corners"));
            }
            if (tiles.Count == 0) { error = "no rect, polygon or path elements"; return false; }
            if (!LinkInsideCorners(tiles, links, out error))
                return false;
            layout.size  = size;
            layout.tiles = tiles;
            layout.BuildCuts(out _);
            error = null;
            return true;
        }

        static string NameOf(XmlElement element)
        {
            var label = element.GetAttribute("label", "http://www.inkscape.org/namespaces/inkscape");
            return !string.IsNullOrEmpty(label) ? label : element.GetAttribute("id");
        }

        static bool TryLength(string text, out float value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            text = text.Trim();
            if (text.EndsWith("px", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(0, text.Length - 2);
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        static List<float> Numbers(string text)
        {
            var numbers = new List<float>();
            if (string.IsNullOrEmpty(text))
                return numbers;
            foreach (var part in text.Split(new[] { ' ', ',', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    numbers.Add(number);
            }
            return numbers;
        }

        // A colour mask: each colour a rectangle of the texture, as many pixels wide and high as the mask; transparent
        // and black pixels are no tile. pixels as Texture2D.GetPixels32 gives them: the bottom row first.
        public static bool TryReadColorMask(Color32[] pixels, int width, int height, ChiselHotspotLayout layout, out string error)
        {
            if (pixels == null || pixels.Length != width * height || width <= 0 || height <= 0) { error = "no pixels"; return false; }
            var regions = new Dictionary<int, (int xMin, int yMin, int xMax, int yMax, int count)>();
            for (int row = 0; row < height; row++)
            {
                for (int x = 0; x < width; x++)
                {
                    var c = pixels[row * width + x];
                    if (c.a == 0 || (c.r == 0 && c.g == 0 && c.b == 0))
                        continue;
                    var key = (c.r << 16) | (c.g << 8) | c.b;
                    var y = height - 1 - row;
                    if (regions.TryGetValue(key, out var r))
                        regions[key] = (Math.Min(r.xMin, x), Math.Min(r.yMin, y), Math.Max(r.xMax, x), Math.Max(r.yMax, y), r.count + 1);
                    else
                        regions[key] = (x, y, x, y, 1);
                }
            }
            if (regions.Count == 0) { error = "the mask has no coloured pixels"; return false; }
            var tiles = new List<HotspotTile>();
            var keys = new List<int>(regions.Keys);
            keys.Sort((a, b) =>
            {
                var ra = regions[a]; var rb = regions[b];
                return ra.yMin != rb.yMin ? ra.yMin.CompareTo(rb.yMin) : ra.xMin.CompareTo(rb.xMin);
            });
            foreach (var key in keys)
            {
                var r = regions[key];
                var w = r.xMax - r.xMin + 1;
                var h = r.yMax - r.yMin + 1;
                if (r.count != w * h)
                {
                    error = $"colour #{key:X6} is not one filled rectangle ({r.count} pixels in {w} x {h})";
                    return false;
                }
                var id = tiles.Count + 1;
                tiles.Add(new HotspotTile { id = id, name = $"#{key:X6}", points = RectanglePoints(new RectInt(r.xMin, r.yMin, w, h)) });
            }
            layout.size  = new Vector2Int(width, height);
            layout.tiles = tiles;
            layout.BuildCuts(out _);
            error = null;
            return true;
        }
    }
}
