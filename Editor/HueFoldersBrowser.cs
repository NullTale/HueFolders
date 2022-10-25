using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HueFolders
{
    public class HueFoldersBrowser
    {
        public static GUIStyle s_labelSelected;

        // =======================================================================
        public static void FolderColorization(string guid, Rect rect)
        {
            if (EditorGUIUtility.isProSkin == false)
                return;
            
            if (EditorPrefs.GetBool(SettingsProvider.k_InTreeViewOnly) && _isTreeView() == false)
                return;
            
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(path) == false) 
                return;
            
            var data = _getFolderData();
            if (data == null)
                return;
            
            SettingsProvider.FolderData _getFolderData()
            {
                var data = SettingsProvider.s_FoldersData.FirstOrDefault(n => n._guid == guid);
                if (data != null)
                    return data;
                        
                var searchPath = path;
                while (data == null)
                {
                    searchPath = Path.GetDirectoryName(searchPath);
                    if (string.IsNullOrEmpty(searchPath))
                        return null;
                    
                    var searchGuid = AssetDatabase.GUIDFromAssetPath(searchPath).ToString();
                    
                    SettingsProvider.s_FoldersDataDic.TryGetValue(searchGuid, out data);
                }
                
                return data;
            }
            
            var isSmall = rect.width > rect.height;
            if (isSmall == false)
                return;

            if (_isTreeView() == false)
                rect.xMin += 3;
            
            // GUI.color = _bgColor();
            // GUI.DrawTexture(rect, Texture2D.whiteTexture);
            
            GUI.color = data._color;
            GUI.DrawTexture(rect, _gradient(), ScaleMode.ScaleAndCrop);
            
            GUI.color = Color.white;
            GUI.DrawTexture(_iconRect(), EditorGUIUtility.IconContent(_isFolderEmpty() ? "d_Folder Icon" : "d_FolderEmpty Icon").image);
            
            GUI.Label(_textRect(), Path.GetFileName(path), _labelSkin());

            GUI.color = Color.white;
            
            // =======================================================================
            Rect _iconRect()
            {
                var result = new Rect(rect);
                result.width = result.height;

                return result;
            }
            
            Rect _textRect()
            {
                var result = new Rect(rect);
                result.xMin += _iconRect().width;
                if (_isTreeView())
                    result.yMax -= 1;

                return result;
            }
            
            bool _isFolderEmpty()
            {
                var items = Directory.EnumerateFileSystemEntries(path);
                using (var en = items.GetEnumerator())
                    return en.MoveNext();
            }
            
            bool _isSelected()
            {
                //return Selection.GetFiltered<DefaultAsset>(SelectionMode.Assets).Select(n => AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(n)).ToString()).Contains(guid);
                return Selection.assetGUIDs.Contains(guid);
            }
            
            bool _isTreeView()
            {
                return (rect.x - 16) % 14 == 0;
            }
            
            /*Color _bgColor()
            {
                //if (_isSelected())
                //    return new Color32(44, 93, 135, 255);
                
                return _isTreeView() ? new Color32(56, 56, 56, 255) : new Color32(51, 51, 51, 255);
            }*/
            
            GUIStyle _labelSkin()
            {
                if (s_labelSelected == null)
                {
                    s_labelSelected = new GUIStyle(GUI.skin.label);
                    //_labelSelected.fontStyle = FontStyle.Bold;
                    s_labelSelected.normal.textColor = Color.white;
                }

                return _isSelected() ? s_labelSelected : GUI.skin.label;
            }
            
            Texture2D _gradient()
            {
                if (SettingsProvider.s_Gradient == null)
                    SettingsProvider._updateGradient();
                
                //if (_isSelected())
                //    return SettingsProvider.s_Fill;
                
                return SettingsProvider.s_Gradient;
            }
        }
    }
}