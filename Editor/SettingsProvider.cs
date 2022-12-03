using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HueFolders
{
    public class SettingsProvider : UnityEditor.SettingsProvider
    {
        public const  string       k_PrefsFile              = nameof(HueFolders) + "_Prefs.json";
        public const  string       k_PrefsPath              = "ProjectSettings\\" + k_PrefsFile;
        public const  int          k_GradientWidth          = 16;
        
        public static  EditorOption s_InTreeViewOnly         = new EditorOption(nameof(HueFolders) + "_InTreeViewOnly");
        private const  bool         k_InTreeViewOnly_Default = true;
        
        
        public static  EditorOption s_FoldersTint           = new EditorOption(nameof(HueFolders) + "_FoldersTint");
        private static Color        k_FoldersTint_Default   = Color.white;
        
        
        public static  EditorOption s_SubFoldersTint         = new EditorOption(nameof(HueFolders) + "_SubFoldersTint");
        private static Color        k_SubFoldersTint_Default = new Color(1, 1, 1, 0.7f);
        
        public static  EditorOption s_GradientScale          = new EditorOption(nameof(HueFolders) + "_GradientScale");
        private static Vector2      k_GradientScale_Default  = new Vector2(0.536f, 1f);
        
        public static Dictionary<string, FolderData> s_FoldersDataDic;
        public static Color                          s_FoldersDefaultTint;
        private static Color                         k_FoldersDefaultTint_Default = new Color(.6f, .6f, .7f, .7f);
        
        public static List<FolderData>               s_FoldersData;
        public static Texture2D                      s_Gradient;
        
        private ReorderableList _foldersList;

        // =======================================================================
        [Serializable]
        private class JsonWrapper
        {
            public Color                              DefaultTint;
            public DictionaryData<string, FolderData> FoldersData;
            
            // =======================================================================
            [Serializable]
            public class DictionaryData<TKey, TValue>
            {
                public List<TKey>   Keys;
                public List<TValue> Values;
                
                public IEnumerable<KeyValuePair<TKey, TValue>> Enumerate()
                {
                    if (Keys == null || Values == null)
                        yield break; 
                            
                    for (var n = 0; n < Keys.Count; n++)
                        yield return new KeyValuePair<TKey, TValue>(Keys[n], Values[n]);
                }

                public DictionaryData() 
                    : this(new List<TKey>(), new List<TValue>())
                {
                }
                
                public DictionaryData(List<TKey> keys, List<TValue> values)
                {
                    Keys   = keys;
                    Values = values;
                }
                
                public DictionaryData(IEnumerable<KeyValuePair<TKey, TValue>> data)
                {
                    var pairs = data as KeyValuePair<TKey, TValue>[] ?? data.ToArray();
                    Keys   = pairs.Select(n => n.Key).ToList();
                    Values = pairs.Select(n => n.Value).ToList();
                }
            }
        }
        
        [Serializable]
        public class FolderData
        {
            public string _guid;
            public Color  _color;
            public bool   _recursive;
        }
        
        public class EditorOption
        {
            public string _key;
            public object _val;

            // =======================================================================
            public EditorOption(string key)
            {
                _key = key;
            }

            public void Setup<T>(T def)
            {
                if (HasPrefs() == false)
                    Write(def);
                
                _val = Read<T>(def);
            }
            
            public bool HasPrefs() => EditorPrefs.HasKey(_key);
            
            public T Get<T>()
            {
                return (T)_val;
            }
            
            public T Read<T>(T fallOff = default)
            {
                try
                {
                    var type = typeof(T);
                    
                    if (type == typeof(bool))
                        return (T)(object)EditorPrefs.GetBool(_key);
                    if (type == typeof(int))
                        return (T)(object)EditorPrefs.GetInt(_key);
                    if (type == typeof(float))
                        return (T)(object)EditorPrefs.GetFloat(_key);
                    if (type == typeof(string))
                        return (T)(object)EditorPrefs.GetString(_key);
                    
                    return JsonUtility.FromJson<T>(EditorPrefs.GetString(_key));
                }
                catch
                {
                    return fallOff;
                }
            }
            
            public void Write<T>(T val)
            {
                var type = typeof(T);
                _val = val;
                
                if (type == typeof(bool))
                    EditorPrefs.SetBool(_key, (bool)_val);
                else
                if (type == typeof(int))
                    EditorPrefs.SetInt(_key, (int)_val);
                else
                if (type == typeof(string))
                    EditorPrefs.SetString(_key, (string)_val);
                else
                if (type == typeof(float))
                    EditorPrefs.SetFloat(_key, (float)_val);
                else
                    EditorPrefs.SetString(_key, JsonUtility.ToJson(val));
            }
        }
        
        // =======================================================================
        public SettingsProvider(string path, SettingsScope scopes, IEnumerable<string> keywords = null) : base(path, scopes, keywords)
        {
        }
        
        [SettingsProvider]
        public static UnityEditor.SettingsProvider CreateSettingsProvider()
        {
            var provider = new SettingsProvider("Preferences/Hue Folders", SettingsScope.User);
            return provider;
        }
        
        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            // initialize data from json, read editor prefs
            _setProjectDataDefault();

            if (File.Exists(k_PrefsPath))
            {
                using var file = File.OpenText(k_PrefsPath);
                try
                {
                    var data = JsonUtility.FromJson<JsonWrapper>(file.ReadToEnd());
                    
                    s_FoldersData = data.FoldersData
                                        .Enumerate()
                                        .Select(n => n.Value)
                                        .ToList();
                    
                    s_FoldersDefaultTint = data.DefaultTint;
                }
                catch
                {
                    _setProjectDataDefault();
                }
            }
            
            s_FoldersDataDic = s_FoldersData.ToDictionary(n => n._guid, n => n);
            
            s_InTreeViewOnly.Setup(k_InTreeViewOnly_Default);
            s_SubFoldersTint.Setup(k_SubFoldersTint_Default);
            s_GradientScale.Setup(k_GradientScale_Default);
            s_FoldersTint.Setup(k_FoldersTint_Default);

            _updateGradient();
            EditorApplication.projectWindowItemOnGUI += HueFoldersBrowser.FolderColorization;

            // -----------------------------------------------------------------------
            void _setProjectDataDefault()
            {
                s_FoldersData        = new List<FolderData>();
                s_FoldersDefaultTint = k_FoldersDefaultTint_Default;
            }
        }

        public override void OnGUI(string searchContext)
        {
            // draw ui, update variables
            

            // editor prefs variables
            EditorGUI.BeginChangeCheck();

            var inTreeViewOnly = EditorGUILayout.Toggle("In Tree View Only", s_InTreeViewOnly.Get<bool>());
            // project prefs variables
            EditorGUI.BeginChangeCheck();
            
            s_FoldersDefaultTint = EditorGUILayout.ColorField("Default Tint", s_FoldersDefaultTint);
            
            if (EditorGUI.EndChangeCheck())
            {
                EditorApplication.RepaintProjectWindow();
                _saveProjectPrefs();
            }
            var foldersTint    = EditorGUILayout.ColorField("Folders Tint", s_FoldersTint.Get<Color>());
            var subFoldersTint = EditorGUILayout.ColorField("Sub Folders Tint", s_SubFoldersTint.Get<Color>());
            var gradientScale  = s_GradientScale.Get<Vector2>(); 
            EditorGUILayout.MinMaxSlider("Gradient Scale", ref gradientScale.x, ref gradientScale.y, 0f, 1f);
            
            if (EditorGUI.EndChangeCheck())
            {
                s_InTreeViewOnly.Write(inTreeViewOnly);
                s_SubFoldersTint.Write(subFoldersTint);
                s_GradientScale.Write(gradientScale);
                s_FoldersTint.Write(foldersTint);
                
                EditorApplication.RepaintProjectWindow();
                _updateGradient();
            }
            
            _getFoldersList().DoLayoutList();
            
        }
        
        public static void _updateGradient()
        {
            s_Gradient          = new Texture2D(k_GradientWidth, 1);
            s_Gradient.wrapMode = TextureWrapMode.Clamp;
            var range = s_GradientScale.Get<Vector2>();
            
            if (range == new Vector2(0, 1))
            {
                for (var x = 0; x < k_GradientWidth; x++)
                    s_Gradient.SetPixel(x, 0, new Color(1, 1, 1, 1));
            }
            else
            {
                for (var x = 0; x < k_GradientWidth; x++)
                    s_Gradient.SetPixel(x, 0, new Color(1, 1, 1, _getAlpha(x)));

                // -----------------------------------------------------------------------
                float _getAlpha(int xPixel)
                {
                    var xScale = xPixel / (k_GradientWidth - 1f);
                    
                    if (xScale >= range.x && xScale <= range.y)
                        return 1f;
                    
                    var distance = xScale < range.x ? range.x - xScale : xScale - range.y; 
                    return Mathf.Clamp01(1f - distance * 3f);
                }
            }

            s_Gradient.Apply();
        }

        private ReorderableList _getFoldersList()
        {
            if (_foldersList != null)
                return _foldersList;
            
            _foldersList = new ReorderableList(s_FoldersData, typeof(FolderData), true, true, true, true);
            _foldersList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var element = s_FoldersData[index];
                
                var refRect   = new Rect(rect.position, new Vector2(rect.size.x * .5f - EditorGUIUtility.standardVerticalSpacing, rect.size.y));
                var colorRect = new Rect(rect.position + new Vector2(rect.size.x * .5f, 0f), new Vector2(rect.size.x * .5f - 18f - EditorGUIUtility.standardVerticalSpacing, rect.size.y));
                var recRect   = new Rect(rect.position + new Vector2(rect.size.x - 18f, 0f), new Vector2(18f, rect.size.y));
                
                EditorGUI.BeginChangeCheck();
                var folder = EditorGUI.ObjectField(refRect,
                                                   GUIContent.none,
                                                   AssetDatabase.LoadAssetAtPath<DefaultAsset>(AssetDatabase.GUIDToAssetPath(element._guid)),
                                                   typeof(DefaultAsset),
                                                   false);
                
                element._color     = EditorGUI.ColorField(colorRect, GUIContent.none, element._color);
                element._recursive = EditorGUI.Toggle(recRect, GUIContent.none, element._recursive);
                
                if (EditorGUI.EndChangeCheck())
                {
                    var fodlerGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(folder));
                    
                    if (element._guid != fodlerGuid)
                    { 
                        // ignore non directory files
                        if (folder != null && File.GetAttributes(AssetDatabase.GetAssetPath(folder)).HasFlag(FileAttributes.Directory) == false)
                            folder = null;

                        // ignore if already contains
                        if (folder != null && s_FoldersData.Any(n => n._guid == fodlerGuid))
                            folder = null;
                    }
                    
                    element._guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(folder));
                    _saveProjectPrefs();
                     
                    EditorApplication.RepaintProjectWindow();
                }
                
            };
            _foldersList.elementHeight = EditorGUIUtility.singleLineHeight;
            _foldersList.onRemoveCallback = list =>
            {
                s_FoldersData.RemoveAt(list.index);
                _saveProjectPrefs();
            };
            _foldersList.onAddCallback = list =>
            {
                var color = Color.HSVToRGB(Random.value, 0.7f, 0.8f);
                color.a = 0.7f;
                
                s_FoldersData.Add(new FolderData() { _color = color, _recursive = true});
            };
            _foldersList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, new GUIContent("Folders", ""));
            };
            
            return _foldersList;
        }
        
        private void _saveProjectPrefs()
        {
            s_FoldersDataDic = s_FoldersData
                               .Where(n => n != null && string.IsNullOrEmpty(n._guid) == false && n._guid != Guid.Empty.ToString())
                               .ToDictionary(n => n._guid, n => n);
            
            var json = new JsonWrapper()
            {
                DefaultTint = s_FoldersDefaultTint,
                FoldersData = new JsonWrapper.DictionaryData<string, FolderData>(s_FoldersDataDic
                                                                            .Values
                                                                            .Select(n => new KeyValuePair<string, FolderData>(n._guid, n)))
            };
            
            File.WriteAllText(k_PrefsPath, JsonUtility.ToJson(json));
        }
    }
}
