using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FontEdit
{
	/// <summary>
	/// A cleaner font inspector that integrates FontEdit functionality
	/// </summary>
	[CustomEditor(typeof(Font)), CanEditMultipleObjects]
	public class FontEditInspector : Editor
	{
		private static readonly string[] fontProperties =
		{
			"FontSize", "Ascent",
			"Kerning", "LineSpacing",
			"DefaultMaterial"
		};

		private static readonly string[] arrayProperties =
		{
			"FontNames", "FallbackFonts"
		};

		// Automatic drawing of these arrays doesn't work for some reason
		private static void DrawArray(SerializedProperty property)
		{
			property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, property.displayName);
			if (property.isExpanded)
			{
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(property.FindPropertyRelative("Array.size"));
				for (int i = 0; i < property.arraySize; i++)
					EditorGUILayout.PropertyField(property.GetArrayElementAtIndex(i));
				EditorGUI.indentLevel--;
			}
		}

		// This keeps the inspector updating when you use the editor window
		public override bool RequiresConstantRepaint()
		{
			return true;
		}

		private const float buttonWidth = 80f;

		public override void OnInspectorGUI()
		{
			// ==== Styles ====
			var centerLabel = new GUIStyle(EditorStyles.boldLabel)
			{
				alignment = TextAnchor.MiddleCenter
			};

			// ==== Font settings ====
			EditorGUILayout.LabelField("Font settings", EditorStyles.boldLabel);
			foreach (var pname in fontProperties)
			{
				var p = serializedObject.FindProperty("m_" + pname);
				EditorGUILayout.PropertyField(p);
			}
			foreach (var pname in arrayProperties)
			{
				var p = serializedObject.FindProperty("m_" + pname);
				DrawArray(p);
			}
			EditorGUILayout.Space();

			// ==== Open window ====
			if (FontEditWindow.Instance == null)
			{
				var br = EditorGUILayout.GetControlRect(false, 40f);
				if(br.width > 200f)
					br = new Rect(br.center - new Vector2(100f, 20f), new Vector2(200f, 40f));
				if(GUI.Button(br, "Open FontEdit window"))
					FontEditWindow.OpenWindow();
			}
			else if (!FontEditWindow.Instance.CanEdit)
			{
				EditorGUILayout.LabelField("Unable to edit current selection", centerLabel);
			}
			else
			{
				var editor = new SerializedObject(FontEditWindow.Instance);

				// Re-enable controls if this is an asset
				var isAsset = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(serializedObject.targetObject)) != null;
				if(isAsset)
					EditorGUI.EndDisabledGroup();

				// ==== Editor control ====
				EditorGUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				FontEditWindow.Instance.WindowMode =
					(WindowMode) GUILayout.Toolbar((int) FontEditWindow.Instance.WindowMode,
					Enum.GetNames(typeof (WindowMode)), GUILayout.MaxWidth(buttonWidth*2));
				GUILayout.FlexibleSpace();
				EditorGUILayout.EndHorizontal();

				EditorGUILayout.LabelField("Character", EditorStyles.boldLabel);

				// ==== Selected character ====
				var selectedChar = editor.FindProperty("selectedChar");
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField(new GUIContent("Selected", selectedChar.tooltip),
					GUILayout.Width(EditorGUIUtility.labelWidth));
				var width = EditorGUIUtility.currentViewWidth - EditorGUIUtility.labelWidth;
				//    Character index
				EditorGUILayout.PropertyField(selectedChar, GUIContent.none, GUILayout.Width(width / 2f));
				EditorGUI.BeginChangeCheck();
				//    Character from string
				var newStr = EditorGUILayout.TextField(((char)selectedChar.intValue).ToString());
				if (EditorGUI.EndChangeCheck())
				{
					var c = newStr.Length < 1 ? '\0' : newStr[0];
					selectedChar.intValue = c;
				}
				EditorGUILayout.EndHorizontal();
				//     Show character name
				UnicodeName.Init();
				string unicodeName;
				if (UnicodeName.CurrentStatus != UnicodeName.Status.Done)
					unicodeName = UnicodeName.StatusMessage;
				else
					unicodeName = UnicodeName.GetName((char) selectedChar.intValue) ?? "Unknown character";
				EditorGUILayout.LabelField(" ", unicodeName);
				//     Get FontCharacter object from array
				var selectionIndex = FontEditWindow.Instance.GetSelectionIndex();
				var chars = editor.FindProperty("chars");
                var fontChar = selectionIndex < 0 ? null : chars.GetArrayElementAtIndex(selectionIndex);

				if (fontChar == null)
				{
					// ==== Create ====
					if (selectedChar.intValue <= 0)
						EditorGUILayout.LabelField("No character selected", centerLabel);
					else
					{
						EditorGUILayout.BeginHorizontal();
						GUILayout.FlexibleSpace();
						if (GUILayout.Button("Add", GUILayout.MaxWidth(buttonWidth)))
							FontEditWindow.Instance.AddSelected();
						EditorGUILayout.EndHorizontal();
					}
				}
				else
				{
					// ==== UV ====
					var tex = FontEditWindow.Instance.Texture;
					var displayUnit = editor.FindProperty("displayUnit");
					EditorGUILayout.PropertyField(displayUnit);
					var uvRect = fontChar.FindPropertyRelative("uv");
					var rectValue = uvRect.rectValue;
					if ((DisplayUnit)displayUnit.intValue == DisplayUnit.Pixels)
					{
                        rectValue.x *= tex.width;
						rectValue.y *= tex.height;
						rectValue.width *= tex.width;
						rectValue.height *= tex.height;
					}
					EditorGUI.BeginChangeCheck();
					rectValue = EditorGUILayout.RectField("UV rect", rectValue);
					if (EditorGUI.EndChangeCheck())
					{
						if ((DisplayUnit)displayUnit.intValue == DisplayUnit.Pixels)
						{
							rectValue.x /= tex.width;
							rectValue.y /= tex.height;
							rectValue.width /= tex.width;
							rectValue.height /= tex.height;
						}
						uvRect.rectValue = rectValue;
					}

					// ==== Rotation ====
					EditorGUILayout.PropertyField(fontChar.FindPropertyRelative("rotated"));
					EditorGUILayout.Space();

					// ==== Vert ====
					var vert = fontChar.FindPropertyRelative("vert");
					vert.rectValue = EditorGUILayout.RectField("Vert", vert.rectValue);

					// ==== Advance ====
					EditorGUILayout.PropertyField(fontChar.FindPropertyRelative("advance"));

					// ==== Delete ====
					EditorGUILayout.BeginHorizontal();
					GUILayout.FlexibleSpace();
					if (GUILayout.Button("Delete", GUILayout.MaxWidth(buttonWidth)))
					{
						FontEditWindow.Instance.DeleteSelected();
						editor.Update();
					}
					EditorGUILayout.EndHorizontal();
				}

				editor.ApplyModifiedProperties();
				editor.Update();

				// ==== Control ====
				EditorGUI.BeginDisabledGroup(!FontEditWindow.Instance.HasChanges);
				EditorGUILayout.BeginHorizontal();
				if(GUILayout.Button("Apply", GUILayout.MaxWidth(buttonWidth)))
					FontEditWindow.Instance.Apply();
				GUILayout.FlexibleSpace();
				if(GUILayout.Button("Revert", GUILayout.MaxWidth(buttonWidth)))
					FontEditWindow.Instance.Revert();
				EditorGUILayout.EndHorizontal();
				EditorGUI.EndDisabledGroup();

				if(isAsset)
					EditorGUI.BeginDisabledGroup(true);
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
}
