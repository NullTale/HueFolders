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
        public const  string       k_InTreeViewOnly         = nameof(HueFolders) + "_InTreeViewOnly";
        public const  string       k_Gradient               = nameof(HueFolders) + "_Gradient";
        public const  int          k_GradientWidth          = 16;
        
        public static EditorOption s_SubFoldersTint         = new EditorOption(nameof(HueFolders) + "_SubFoldersTint");
        public static Color        s_SubFoldersTint_Default = Color.white;
        public static Color        s_SubFoldersTint_Value   = Color.white;
        
        public const  bool                           k_InTreeViewOnly_Default = true;
        public const  bool                           k_Gradient_Default       = true;
        
        public static Dictionary<string, FolderData> s_FoldersDataDic;
        public static List<FolderData>               s_FoldersData;
        public static Texture2D                      s_Gradient;
        public static Texture2D                      s_Fill;
        
        private ReorderableList _foldersList;

        // =======================================================================
        [Serializable]
        private class JsonWrapper
        {
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

            // =======================================================================
            public EditorOption(string key)
            {
                _key = key;
            }

            public bool HasPrefs() => EditorPrefs.HasKey(_key);
            
            public T Get<T>()
            {
                var type = typeof(T);
                
                if (type == typeof(bool))
                    return (T)(object)EditorPrefs.GetBool(_key);
                if (type == typeof(int))
                    return (T)(object)EditorPrefs.GetInt(_key);
                if (type == typeof(string))
                    return (T)(object)EditorPrefs.GetString(_key);
                
                return JsonUtility.FromJson<T>(EditorPrefs.GetString(_key));
            }
            
            public void Write<T>(out T val)
            {
                val = Get<T>();
            }
            
            public void Set<T>(T val)
            {
                var type = typeof(T);
                
                if (type == typeof(bool))
                    EditorPrefs.SetBool(_key, (bool)(object)val);
                else
                if (type == typeof(int))
                    EditorPrefs.SetInt(_key, (int)(object)val);
                else
                if (type == typeof(string))
                    EditorPrefs.SetString(_key, (string)(object)val);
                else
                    EditorPrefs.SetString(_key, JsonUtility.ToJson(val));
            }
        }
        
        // =======================================================================
        public SettingsProvider(string path, SettingsScope scopes, IEnumerable<string> keywords = null) : base(path, scopes, keywords)
        {
        }
        
        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            s_FoldersData = new List<FolderData>();
            
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
                }
                catch
                {
                    // ignored
                }
            }
            
            s_FoldersDataDic = s_FoldersData.ToDictionary(n => n._guid, n => n);
            
            if (EditorPrefs.HasKey(k_InTreeViewOnly) == false)
                EditorPrefs.SetBool(k_InTreeViewOnly, k_InTreeViewOnly_Default);
            
            //if (EditorPrefs.HasKey(k_Gradient) == false)
                EditorPrefs.SetBool(k_Gradient, k_Gradient_Default);
            
            if (s_SubFoldersTint.HasPrefs() == false)
                s_SubFoldersTint.Set(s_SubFoldersTint_Default);
            
            s_SubFoldersTint.Write(out s_SubFoldersTint_Value);
            
            _updateGradient();
            EditorApplication.projectWindowItemOnGUI += HueFoldersBrowser.FolderColorization;
        }

        public override void OnGUI(string searchContext)
        {
            EditorGUI.BeginChangeCheck();

            var inTreeViewOnly = EditorGUILayout.Toggle("In Tree View Only", EditorPrefs.GetBool(k_InTreeViewOnly));
            //var gradient = EditorGUILayout.Toggle("Gradient", EditorPrefs.GetBool(k_Gradient));
            var subFoldersTint = EditorGUILayout.ColorField("Sub Folders Tint", s_SubFoldersTint.Get<Color>());
            
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(k_InTreeViewOnly, inTreeViewOnly);
                //EditorPrefs.SetBool(k_Gradient, gradient);
                s_SubFoldersTint.Set(subFoldersTint);
                s_SubFoldersTint.Write(out s_SubFoldersTint_Value);
                
                EditorApplication.RepaintProjectWindow();
                _updateGradient();
            }
            
            _getFoldersList().DoLayoutList();
            
        }
        
        public static void _updateGradient()
        {
            s_Gradient          = new Texture2D(k_GradientWidth, 1);
            s_Gradient.wrapMode = TextureWrapMode.Clamp;
            
            if (EditorPrefs.GetBool(k_Gradient))
            {
                for (var x = 0; x < k_GradientWidth; x++)
                    s_Gradient.SetPixel(x, 0, new Color(1, 1, 1, x / (float)(k_GradientWidth - 1)));
            }
            else
            {
                for (var x = 0; x < k_GradientWidth; x++)
                    s_Gradient.SetPixel(x, 0, new Color(1, 1, 1, 1));
            }

            s_Gradient.Apply();
            
            s_Fill = new Texture2D(1, 1);
            s_Fill.SetPixel(0, 0, new Color(1, 1, 1, 0.7f));
            s_Fill.Apply();
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
                FoldersData = new JsonWrapper.DictionaryData<string, FolderData>(s_FoldersDataDic
                                                                            .Values
                                                                            .Select(n => new KeyValuePair<string, FolderData>(n._guid, n)))
            };
            
            File.WriteAllText(k_PrefsPath, JsonUtility.ToJson(json));
        }

        [SettingsProvider]
        public static UnityEditor.SettingsProvider CreateMyCustomSettingsProvider()
        {
            var provider = new SettingsProvider("Preferences/Hue Folders", SettingsScope.User);

            return provider;
        }
    }
}
