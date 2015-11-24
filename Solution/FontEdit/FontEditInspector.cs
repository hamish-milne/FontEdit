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
			"PixelScale", "DefaultMaterial", "Texture",
			//"FontNames", "FallbackFonts" // Arrays don't directly work
		};

		protected enum UvUnit
		{
			Coords,
			Pixels,
		}

		[SerializeField] protected UvUnit uvUnit; 

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

				EditorGUILayout.LabelField("Character", EditorStyles.boldLabel);

				// ==== Selected character ====
				var selectionRect = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), new GUIContent("Selected"));
				//     Rects for character and integer index
				var charRect = new Rect(selectionRect.position,
					new Vector2((selectionRect.width / 2f) - 1f, selectionRect.height));
				var intRect = new Rect(charRect.xMax + 1f, charRect.y,
					selectionRect.width / 2f, selectionRect.height);
				var selectedIndex = editor.FindProperty("selectedChar");
				//     Draw integer index
				EditorGUI.PropertyField(intRect, selectedIndex, GUIContent.none);
				//     Draw character index (from string)
				EditorGUI.BeginChangeCheck();
				var newStr = EditorGUI.TextField(charRect, ((char)selectedIndex.intValue).ToString());
				if (EditorGUI.EndChangeCheck())
				{
					var c = newStr.Length < 1 ? '\0' : newStr[0];
					selectedIndex.intValue = c;
				}
				//     Get FontCharacter object from array
				var selectionIndex = FontEditWindow.Instance.GetSelectionIndex();
				var fontChar = selectionIndex < 0 ? null : editor.FindProperty("chars")
					.GetArrayElementAtIndex(selectionIndex);

				if (fontChar == null)
				{
					EditorGUILayout.LabelField("No character selected", centerLabel);
				}
				else
				{
					// ==== UV ====
					var tex = FontEditWindow.Instance.Texture;
					uvUnit = (UvUnit)EditorGUILayout.EnumPopup("Display unit", uvUnit);
					var uvRect = fontChar.FindPropertyRelative("uvRect");
					var rectValue = uvRect.rectValue;
					if (uvUnit == UvUnit.Pixels)
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
						if (uvUnit == UvUnit.Pixels)
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
				}

				editor.ApplyModifiedProperties();

				// ==== Control ====
				if(GUILayout.Button("Apply"))
					FontEditWindow.Instance.Apply();
			}

			serializedObject.ApplyModifiedProperties();
		}

		public override bool RequiresConstantRepaint()
		{
			return true;
		}
	}
}
