using System;
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

		public override bool RequiresConstantRepaint()
		{
			return true;
		}

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

				// ==== Editor control ====
				FontEditWindow.Instance.WindowMode =
					(WindowMode) GUILayout.Toolbar((int) FontEditWindow.Instance.WindowMode,
					Enum.GetNames(typeof (WindowMode)));

				EditorGUILayout.LabelField("Character", EditorStyles.boldLabel);

				// ==== Selected character ====
				var selectedChar = editor.FindProperty("selectedChar");
				var selectionRect = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(),
					new GUIContent("Selected", selectedChar.tooltip));
				//     Rects for character and integer index
				var charRect = new Rect(selectionRect.position,
					new Vector2((selectionRect.width / 2f) - 1f, selectionRect.height));
				var intRect = new Rect(charRect.xMax + 1f, charRect.y,
					selectionRect.width / 2f, selectionRect.height);
				//     Draw integer index
				EditorGUI.PropertyField(intRect, selectedChar, GUIContent.none);
				//     Draw character index (from string)
				EditorGUI.BeginChangeCheck();
				var newStr = EditorGUI.TextField(charRect, ((char)selectedChar.intValue).ToString());
				if (EditorGUI.EndChangeCheck())
				{
					var c = newStr.Length < 1 ? '\0' : newStr[0];
					selectedChar.intValue = c;
				}
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
					if(selectedChar.intValue <= 0)
						EditorGUILayout.LabelField("No character selected", centerLabel);
					else if (GUILayout.Button("Create character"))
						FontEditWindow.Instance.AddSelected();
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
					if (GUILayout.Button("Delete character"))
					{
						FontEditWindow.Instance.DeleteSelected();
						editor.Update();
					}
				}

				editor.ApplyModifiedProperties();
				editor.Update();

				// ==== Control ====
				EditorGUI.BeginDisabledGroup(!FontEditWindow.Instance.HasChanges);
				EditorGUILayout.BeginHorizontal();
				if(GUILayout.Button("Apply"))
					FontEditWindow.Instance.Apply();
				if(GUILayout.Button("Revert"))
					FontEditWindow.Instance.Revert();
				EditorGUILayout.EndHorizontal();
				EditorGUI.EndDisabledGroup();
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
}
