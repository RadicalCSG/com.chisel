using System.Collections.Generic;
using System.Buffers;
using UnityEngine;
using UnityEditor;
using Chisel.Core;
using System.IO;
using System;

namespace Chisel.Editors
{
    public static class ChiselEditorResources
    {
        internal const string kLargeIconID      = "@2x";
        internal const string kIconPath         = "Icons/";
        internal const string kActiveIconID     = " On";
        internal const string kDarkIconID       = "d_";

        internal static string[] s_ResourceFilePaths;
		internal static string[] s_ResourceUnityPaths;

		static ChiselEditorResources()
        {
            s_EditorPixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
			s_IsProSkin = EditorGUIUtility.isProSkin;

            FindResourcePaths(); 
        }

        // Should be safe since when these parameters change, Unity will do a domain reload, 
        // which will call the constructor in which these are set.
        static readonly float s_EditorPixelsPerPoint;
        static readonly bool s_IsProSkin;
        public static bool IsProSkin => s_IsProSkin;

		public static float ImageScale { get { return (s_EditorPixelsPerPoint > 1.0f) ? 2.0f : 1.0f; } }

        static Texture2D LoadImageFromResourcePaths(string name)
        {
            name += kImageExtension;

            var currentPath = Environment.CurrentDirectory; 
			for (int i = 0; i < s_ResourceUnityPaths.Length; i++)
            {
                if (!System.IO.File.Exists(s_ResourceFilePaths[i] + name))
                    continue;
				var path = s_ResourceUnityPaths[i] + name; 
				var image = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (image)
                    return image;
            }
            return null;
        }

        static Texture2D LoadScaledTextureInternal(string name)
        {
            Texture2D image = null;
            var imagePixelsPerPoint = 1.0f;
            if (s_EditorPixelsPerPoint > 1.0f)
            {
                image = LoadImageFromResourcePaths(name + kLargeIconID);
                if (image != null)
                    imagePixelsPerPoint = 2.0f;
            }

            if (image == null) 
                image = LoadImageFromResourcePaths(name);

            if (image == null)
                return null;

            if (!Mathf.Approximately(imagePixelsPerPoint, s_EditorPixelsPerPoint) &&    // scaling are different
                !Mathf.Approximately(s_EditorPixelsPerPoint % 1, 0))                    // screen scaling is non-integer
                image.filterMode = FilterMode.Bilinear;
            return image;
        }

        public static Texture2D[] LoadIconImages(string name)
        {
            var nameID = HashLowerInvariant(name);
            if (!s_IconImagesLookup.TryGetValue(nameID, out var iconImages))
            {
                iconImages = LoadIconImagesInternal(name);
                s_IconImagesLookup[nameID] = iconImages;
            }
            return iconImages;
        }

        public static GUIContent[] GetIconContent(string name, string tooltip = null)
        {
            var nameID = HashLowerInvariant(name);
            var id = (nameID * 33) + (tooltip?.GetHashCode() ?? 0);
            if (!s_IconContentLookup.TryGetValue(id, out var contents))
            {
                contents = GetIconContentInternal(name, tooltip ?? string.Empty);
                s_IconContentLookup[id] = contents;
            }
            return contents;
        }

        public static GUIContent[] GetIconContentWithName(string name, string tooltip = null)
        {
            var nameID = HashLowerInvariant(name); 
            var id = (nameID * 33) + (tooltip?.GetHashCode() ?? 0);
            if (!s_IconContentWithNameLookup.TryGetValue(id, out var contents))
            {
                contents = GetIconContentWithNameInternal(name, tooltip ?? string.Empty);
                s_IconContentWithNameLookup[id] = contents;
            }
            return contents;
        }

        // TODO: add a AssetPostProcessor to detect if images changed/added/removed and remove those from the lookup
        readonly static Dictionary<string, Texture2D> s_ImagesLookup              = new();
        readonly static Dictionary<int, Texture2D[]>  s_IconImagesLookup          = new();
        readonly static Dictionary<int, GUIContent[]> s_IconContentLookup         = new();
        readonly static Dictionary<int, GUIContent[]> s_IconContentWithNameLookup = new();

        static Texture2D LoadImageInternal(string name)
        {
            name = FixSlashes(name);
            if (s_ImagesLookup.TryGetValue(name, out Texture2D image))
                return image;
            image = LoadScaledTextureInternal(name);
            if (!image)
                return image;
            s_ImagesLookup[name] = image;
            return image;
        }

        static int HashLowerInvariant(string name)
        {
            var length = name.Length;
            if (length == 0)
                return 0;

            var nameBuffer = ArrayPool<char>.Shared.Rent(length);

            for (int i = 0; i < length; i++)
            {
                var lowerChar = char.ToLowerInvariant(name[i + 0]);
                nameBuffer[i] = lowerChar;
            }

            int result;
            unchecked { result = (int)nameBuffer.Hash(); }
            ArrayPool<char>.Shared.Return(nameBuffer);
            return result; 
        }


        static Texture2D LoadIconImageInternal(string name, bool active)
        {
            Texture2D result = null;
            var nameID = name.Replace(' ', '_').ToLowerInvariant();
            if (s_IsProSkin)
            {
                if (active        ) result = LoadImageInternal($@"{kIconPath}{kDarkIconID}{nameID}{kActiveIconID}");
                if (result == null) result = LoadImageInternal($@"{kIconPath}{kDarkIconID}{nameID}");
            }
            if (result == null)
            {
                if (active        ) result = LoadImageInternal($@"{kIconPath}{nameID}{kActiveIconID}");
                if (result == null) result = LoadImageInternal($@"{kIconPath}{nameID}");
            }
            return result;
        } 

        static Texture2D[] LoadIconImagesInternal(string name)
        {
            var iconImages = new[] { LoadIconImageInternal(name, false), LoadIconImageInternal(name, true) };
            if (iconImages[0] == null || iconImages[1] == null)
                iconImages = null;
            return iconImages;
        }

        static GUIContent[] GetIconContentInternal(string name, string tooltip)
        {
			tooltip ??= string.Empty;

            GUIContent[] contents;
            var images = LoadIconImagesInternal(name);
            if (images == null)
                contents = new GUIContent[] { new GUIContent(L10n.Tr(name), L10n.Tr(tooltip)), new GUIContent(L10n.Tr(name), L10n.Tr(tooltip)) };
            else
                contents = new GUIContent[] { new GUIContent(images[0], L10n.Tr(tooltip)), new GUIContent(images[1], L10n.Tr(tooltip)) };
            return contents;
        }


        static GUIContent[] GetIconContentWithNameInternal(string name, string tooltip = "")
        {
            tooltip ??= string.Empty;

            GUIContent[] contents;
            var images = LoadIconImagesInternal(name);
            if (images == null)
                contents = new GUIContent[] { new GUIContent(name, tooltip), new GUIContent(name, tooltip) };
            else
                contents = new GUIContent[] { new GUIContent(name, images[0], tooltip), new GUIContent(name, images[1], tooltip) };
            return contents;
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        public static void ClearCache() 
        { 
            s_ImagesLookup.Clear(); 
            s_IconImagesLookup.Clear(); 
            s_IconContentLookup.Clear(); 
            s_IconContentWithNameLookup.Clear(); 
        }

        
        #region Editor Resource Paths
        const string kEditorResourcesPath   = @"Editor Resources";
        const string kImageExtension        = @".png";

		static (string, string)[] GetSearchPaths()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly(); 
			var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);
			if (packageInfo != null)
			{
				return new (string,string)[]
                       {
					        (
								FixSlashes(System.IO.Path.Combine(packageInfo.assetPath, kEditorResourcesPath)),
								FixSlashes(System.IO.Path.GetFullPath(System.IO.Path.Combine(packageInfo.resolvedPath, kEditorResourcesPath)))
                            ),
							(
								FixSlashes(System.IO.Path.Combine(@"Assets/", kEditorResourcesPath)),
								FixSlashes(System.IO.Path.Combine(Application.dataPath, kEditorResourcesPath))
                            )
					   };
			} else
			{
				return new (string, string)[]
					   {
							(
								System.IO.Path.Combine(packageInfo.assetPath, kEditorResourcesPath),
								System.IO.Path.Combine(@"Assets/", kEditorResourcesPath)
                            )
					   };
			}
		}

        readonly static (string, string)[] searchPaths = GetSearchPaths();

        static void FindResourcePaths()
        {
            var unityPaths = new List<string>();
            var filePaths = new List<string>();
            var foundPaths = new HashSet<string>(); 

            foreach((string localPath, string filePath) in searchPaths) 
            {
                if (System.IO.Directory.Exists(filePath))
				{
                    if (foundPaths.Add(localPath))
                    {
                        unityPaths.Add(localPath + "/");
                        filePaths.Add(filePath + "/");
                    }
                }
            }
            s_ResourceUnityPaths = unityPaths.ToArray();
			s_ResourceFilePaths = filePaths.ToArray();
		}
         
        static string FixSlashes(string path)
        {
            return path.Replace('\\', '/');
        }
        
        static string ToLocalPath(string path)
        {
            path = FixSlashes(path);
            var assetsPathIndex = path.IndexOf(@"/Assets/");
            if (assetsPathIndex != -1)
            {
                path = path.Substring(assetsPathIndex + 1);
            } else
            {
                var packagePathIndex = path.IndexOf(@"/Packages/");
                if (packagePathIndex != -1)
                {
                    path = path.Substring(packagePathIndex + 1);
				}
				var packageCacheIndex = path.IndexOf(@"/PackageCache/");
				if (packageCacheIndex != -1)
				{
					path = "Packages/" + path.Substring(packageCacheIndex + @"/PackageCache/".Length);
				}
			}
            if (!path.EndsWith("/"))
                path = path + "/";
            return path;
        }
        #endregion
    }
}
