using System;
using UnityEditor;
using UnityEngine;

namespace FontEdit
{
	[CustomEditor(typeof(Font))]
	public class FontEditor : Editor
	{
		// Not 100% sure which of these properties are used. Need to test
		private static readonly string[] properties =
		{
			"FontSize", "Ascent",
			"Kerning", "LineSpacing", "CharacterSpacing", "CharacterPadding",
			"PixelScale", "DefaultMaterial", /*"Texture",*/
			//"FontNames", "FallbackFonts" // Arrays don't directly work
		};

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
			foreach (var pname in properties)
			{
				var p = serializedObject.FindProperty("m_" + pname);
				EditorGUILayout.PropertyField(p, new GUIContent(p.displayName));
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
			else
			{
				var editor = new SerializedObject(FontEditWindow.Instance);

				// ==== Editor control ====
				FontEditWindow.Instance.WindowMode =
					(WindowMode) GUILayout.Toolbar((int) FontEditWindow.Instance.WindowMode,
					Enum.GetNames(typeof (WindowMode)));

				EditorGUILayout.LabelField("Character", EditorStyles.boldLabel);

				// ==== Selected character ====
				var selectionRect = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), new GUIContent("Selected"));
				//     Rects for character and integer index
				var charRect = new Rect(selectionRect.position,
					new Vector2((selectionRect.width / 2f) - 1f, selectionRect.height));
				var intRect = new Rect(charRect.xMax + 1f, charRect.y,
					selectionRect.width / 2f, selectionRect.height);
				var selectedChar = editor.FindProperty("selectedChar");
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
					EditorGUILayout.PropertyField(displayUnit, new GUIContent(displayUnit.displayName));
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
				EditorGUILayout.BeginHorizontal();
				if(GUILayout.Button("Apply"))
					FontEditWindow.Instance.Apply();
				if(GUILayout.Button("Revert"))
					FontEditWindow.Instance.Revert();
				EditorGUILayout.EndHorizontal();
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
}
