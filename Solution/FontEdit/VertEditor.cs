using UnityEditor;
using UnityEngine;

namespace FontEdit
{
	public partial class FontEditWindow
	{
		[SerializeField] protected string testString;
		[SerializeField] protected Vector2 testOffset = new Vector2(30f, 30f);
		[SerializeField] protected Vector2 vertOffset = new Vector2(100f, 200f);
		private bool dragTest, dragVert;

		static Rect VertToUi(Rect vert, Vector2 origin)
		{
			return new Rect(vert.x + origin.x, -vert.y + origin.y, vert.width, -vert.height);
		}

		static Rect UiToVert(Rect ui, Vector2 origin)
		{
			return new Rect(ui.x - origin.x, -(ui.y - origin.y), ui.width, -ui.height);
		}

		static Rect RotateUiRect(Rect vert)
		{
			return new Rect(vert.x + vert.width, vert.y + vert.height, vert.height, -vert.width);
		}

		// Draws a single character (with correct vert)
		Rect DrawFontChar(FontCharacter fc, Vector2 origin)
		{
			var vert = VertToUi(fc.vert, origin);
			var oldMatrix = GUI.matrix;
			if (fc.rotated)
			{
				var rv = RotateUiRect(vert);
				GUIUtility.RotateAroundPivot(-90f, rv.min);
				GUI.DrawTextureWithTexCoords(rv, Texture, fc.uv);
				GUI.matrix = oldMatrix;
			}
			else
			{
				GUI.DrawTextureWithTexCoords(vert, Texture, fc.uv);
			}
			return vert;
		}

		// Draws the handle used to position the vert editor(s)
		Vector2 DrawOriginHandle(Vector2 position, ref bool isDragging)
		{
			const float size = 8f;
			var r = Normalize(new Rect(position.x, position.y, -size, size));
			GUI.DrawTexture(r, originHandle);
			EditorGUIUtility.AddCursorRect(r, MouseCursor.MoveArrow);
			switch (Event.current.type)
			{
				case EventType.mouseDrag:
					if(isDragging)
						return Event.current.delta;
					break;
				case EventType.mouseUp:
					isDragging = false;
					break;
				case EventType.mouseDown:
					if (r.Contains(Event.current.mousePosition))
						isDragging = true;
					break;
			}
			return Vector2.zero;
		}

		void DrawVertSelection(Vector2 origin, FontCharacter fc, bool highlight)
		{
			GUI.DrawTexture(new Rect(origin.x, origin.y, axisWidth, -GetAscent()), axisY);
			GUI.DrawTexture(new Rect(origin.x, origin.y, fc.advance, axisWidth), axisX);
			if (highlight)
				GUI.DrawTexture(VertToUi(fc.vert, origin), selection);
		}

		// Draws the vert editor at the given position, returning true if there were changes
		bool DrawVertEditor(Vector2 origin, ref FontCharacter fc)
		{
			DrawVertSelection(origin, fc, false);
			var vert = DrawFontChar(fc, origin);
			DrawHandles(ref vert, ref dragging);
			if (dragging != GrabCorner.None)
			{
				fc.vert = UiToVert(vert, origin);
				changed = true;
				return true;
			}
			return false;
		}

		void DrawTest()
		{
			// User input for test string
			var strRect = new Rect(WindowRect.x, WindowRect.y, WindowRect.width, 16f);
			testString = EditorGUI.TextField(strRect, testString);
			// Don't save the test string as an undo (because it's a bit pointless)
			(new SerializedObject(this)).ApplyModifiedPropertiesWithoutUndo();
			var hasDrawnVert = false;
			var origin = WindowRect.position + (Vector2.up*GetAscent());
			int? newChar = null;
            if (!string.IsNullOrEmpty(testString))
			{
				testOffset += DrawOriginHandle(WindowRect.position + testOffset, ref dragTest);
				var tOrigin = origin + testOffset;
				foreach (var c in testString)
				{
					// Get the character data..
					var fc = default(FontCharacter);
					int i;
					for(i = 0; i < chars.Length; i++)
						if (chars[i].index == c)
						{
							fc = chars[i];
							break;
						}
					// Skip non-existant characters (maybe add something else in here)
					if (fc.index <= 0)
						continue;
					// Draw the vert editor if the character matches the selection
					if (!hasDrawnVert && fc.index == selectedChar)
					{
						if (DrawVertEditor(tOrigin, ref fc))
							chars[i] = fc;
						hasDrawnVert = true;
					} else
					{
						var r = DrawFontChar(fc, tOrigin);
						// Let the user click on test characters to edit them
						if (fc.index != selectedChar && r.Contains(Event.current.mousePosition, true))
						{
							DrawVertSelection(tOrigin, fc, true);
							if (Event.current.type == EventType.mouseDown)
								newChar = fc.index;
						}
					}
					// Advance the cursor
					tOrigin.x += fc.advance * GetKerning();
				}
			}
			// If no character matched the test string, draw the vert editor seperately
			var idx = GetSelectionIndex();
			if (!hasDrawnVert && idx >= 0)
			{
				origin += vertOffset;
				vertOffset += DrawOriginHandle(origin, ref dragVert);
				var fc = chars[idx];
				if (DrawVertEditor(origin, ref fc))
					chars[idx] = fc;
			}
			// Change the selected character if we clicked
			if (newChar.HasValue)
				selectedChar = newChar.Value;
		}
	}
}
