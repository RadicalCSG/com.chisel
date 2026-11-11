using System;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    /// <summary>A color the user can set in Chisel's preferences, kept in EditorPrefs the way Unity keeps the colors of its
    /// own Colors preferences. Until it is set it is its default, which can be one of Unity's own colors so it changes
    /// along with it.</summary>
#if UNITY_6000_7_OR_NEWER
    // s_Version only tells the settings their EditorPrefs may have changed; it is kept on entering play mode as they are
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed class ChiselColorSetting
    {
        /// <summary>Its EditorPrefs key, also the name in the stored value, as Unity's are ("Scene/Grid")</summary>
        public string     Key     { get; }
        /// <summary>The heading it is shown under in the preferences</summary>
        public string     Group   { get; }
        public GUIContent Content { get; }

        readonly Func<Color> defaultColor;

        // The color read from EditorPrefs, read again when any setting has been set or reset since; colors are read
        // every time handles are drawn
        static int s_Version;
        int   readVersion = -1;
        bool  isSet;
        Color storedColor;

        public ChiselColorSetting(string key, string group, string name, string tooltip, Color defaultColor)
            : this(key, group, name, tooltip, () => defaultColor) { }

        public ChiselColorSetting(string key, string group, string name, string tooltip, Func<Color> defaultColor)
        {
            Key               = key;
            Group             = group;
            Content           = new GUIContent(name, tooltip);
            this.defaultColor = defaultColor;
        }

        public Color Default => defaultColor();

        public bool IsSet { get { Read(); return isSet; } }

        public Color Value
        {
            get { Read(); return isSet ? storedColor : Default; }
            set
            {
                EditorPrefs.SetString(Key, ColorUtility.FormatPreferenceColor(Key, value));
                s_Version++;
            }
        }

        /// <summary>Back to its default, which follows Unity's color again where it is one</summary>
        public void Reset()
        {
            EditorPrefs.DeleteKey(Key);
            s_Version++;
        }

        /// <summary>Makes every setting read EditorPrefs again, after they were changed some other way</summary>
        public static void ReadAgain() { s_Version++; }

        void Read()
        {
            if (readVersion == s_Version)
                return;
            readVersion = s_Version;
            isSet = EditorPrefs.HasKey(Key) && ColorUtility.TryParsePreferenceColor(EditorPrefs.GetString(Key), out storedColor);
        }
    }
}
