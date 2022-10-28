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
        public static GUIStyle s_labelNormal;
        public static GUIStyle s_labelSelected;

        // =======================================================================
        public static void FolderColorization(string guid, Rect rect)
        {
            if (SettingsProvider.s_InTreeViewOnly.Get<bool>() && _isTreeView() == false)
                return;
            
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(path) == false) 
                return;

            var  data = _getFolderData(out var isSubFolder);
            if (data == null)
                return;
            
            var folderColor = data._color;
            if (isSubFolder)
            {
                var tint = SettingsProvider.s_SubFoldersTint.Get<Color>(); 
                folderColor *= tint;
            } 
            
            SettingsProvider.FolderData _getFolderData(out bool isSubFolder)
            {
                isSubFolder = false;        
                if (SettingsProvider.s_FoldersDataDic.TryGetValue(guid, out var folderData))
                    return folderData;
                
                isSubFolder = true;        
                var searchPath = path;
                while (folderData == null)
                {
                    searchPath = Path.GetDirectoryName(searchPath);
                    if (string.IsNullOrEmpty(searchPath))
                        return null;
                    
                    var searchGuid = AssetDatabase.GUIDFromAssetPath(searchPath).ToString();
                    
                    SettingsProvider.s_FoldersDataDic.TryGetValue(searchGuid, out folderData);
                    if (folderData != null && folderData._recursive == false)
                        return null;
                }
                
                return folderData;
            }
            
            var isSmall = rect.width > rect.height;
            if (isSmall == false)
                return;

            if (_isTreeView() == false)
                rect.xMin += 3;
            
            // draw background, overdraw icon and text
            GUI.color = folderColor;
            GUI.DrawTexture(rect, _gradient(), ScaleMode.ScaleAndCrop);
            
            GUI.color = Color.white;
            GUI.DrawTexture(_iconRect(), _folderIcon());
            
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
                {
                    result.yMax -= 1; 
#if UNITY_2022_1_OR_NEWER
                    if (SettingsProvider.s_VisualizationFix.Get<bool>())
                        result.xMin += 1;
#endif
                }

                return result;
            }
            
			Texture _folderIcon()
			{
				if (EditorGUIUtility.isProSkin)
					return EditorGUIUtility.IconContent(_isFolderEmpty() ? "FolderEmpty Icon" : "Folder Icon").image;
				else
                {
                    if (_isSelected())
					    return EditorGUIUtility.IconContent(_isFolderEmpty() ? "FolderEmpty On Icon" : "Folder On Icon").image;
                    else
					    return EditorGUIUtility.IconContent(_isFolderEmpty() ? "FolderEmpty Icon" : "Folder Icon").image;
                }
			}
			
            bool _isFolderEmpty()
            {
                var items = Directory.EnumerateFileSystemEntries(path);
                using (var en = items.GetEnumerator())
                    return en.MoveNext() == false;
            }
            
            bool _isSelected()
            {
                return Selection.assetGUIDs.Contains(guid);
            }
            
            bool _isTreeView()
            {
                return (rect.x - 16) % 14 == 0;
            }
            
            GUIStyle _labelSkin()
            {
                if (s_labelSelected == null)
                {
                    s_labelSelected                  = new GUIStyle(GUI.skin.label);
                    s_labelSelected.normal.textColor = Color.white;
                    s_labelSelected.hover.textColor  = s_labelSelected.normal.textColor;
                }
                if (s_labelNormal == null)
                {
                    s_labelNormal                  = new GUIStyle(GUI.skin.label);
                    s_labelNormal.normal.textColor = EditorGUIUtility.isProSkin ? new Color32(175, 175, 175, 255) : new Color32(2, 2, 2, 255);
                    s_labelNormal.hover.textColor  = s_labelNormal.normal.textColor;
                }

                return _isSelected() ? s_labelSelected : s_labelNormal;
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