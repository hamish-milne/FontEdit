using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FontEdit
{
	public partial class FontEditWindow
	{
		Rect VertToUi(Rect vert, Vector2 origin)
		{
			return new Rect(vert.x + origin.x, -vert.y + origin.y, vert.width, -vert.height);
		}

		Rect UiToVert(Rect ui, Vector2 origin)
		{
			return new Rect(ui.x - origin.x, -(ui.y - origin.y), ui.width, -ui.height);
		}

		Rect RotateVert(Rect vert)
		{
			return new Rect(vert.x + vert.width, vert.y + vert.height, vert.height, -vert.width);
		}

		Rect DrawFontChar(FontCharacter fc, Vector2 origin)
		{
			var vert = VertToUi(fc.vert, origin);
			var oldMatrix = GUI.matrix;
			if (fc.rotated)
			{
				var rv = RotateVert(vert);
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

		Vector2 DrawOriginHandle(Vector2 position, ref bool isDragging)
		{
			const float size = 8f;
			var r = Normalize(new Rect(position.x, position.y, -size, size));
			GUI.DrawTexture(r, originHandle);
			EditorGUIUtility.AddCursorRect(r, MouseCursor.MoveArrow);
			if (isDragging)
			{
				if (Event.current.type == EventType.MouseDrag)
					return Event.current.delta;
				else if (Event.current.type == EventType.mouseUp)
					isDragging = false;
			} else if (Event.current.type == EventType.mouseDown && r.Contains(Event.current.mousePosition))
			{
				isDragging = true;
			}
			return Vector2.zero;
		}

		void DrawVertEditor(Vector2 origin)
		{
			var index = GetSelectionIndex();
			if (index >= 0)
			{
				var fc = chars[index];
				GUI.DrawTexture(new Rect(origin.x, origin.y, 2f, -GetFontAscent()), axisY);
				GUI.DrawTexture(new Rect(origin.x, origin.y, fc.advance, 2f), axisX);
				var vert = DrawFontChar(fc, origin);
				DrawHandles(ref vert, ref dragging);
				if (dragging != GrabCorner.None)
				{
					fc.vert = UiToVert(vert, origin);
					chars[index] = fc;
					changed = true;
				}
			}
		}

		[SerializeField]
		protected string testString;

		[SerializeField] protected Vector2 testOffset = new Vector2(30f, 30f);
		[SerializeField] protected Vector2 vertOffset = new Vector2(100f, 200f);
		[SerializeField] protected bool dragTest, dragVert;

		float GetKerning()
		{
			return (new SerializedObject(currentFont)).FindProperty("m_Kerning").floatValue;
		}

		void DrawTest()
		{
			var strRect = new Rect(WindowRect.x, WindowRect.y, WindowRect.width, 16f);
			testString = EditorGUI.TextField(strRect, testString);
			(new SerializedObject(this)).ApplyModifiedPropertiesWithoutUndo();
			var hasDrawnVert = false;
			var origin = WindowRect.position + (Vector2.up*GetFontAscent());
            if (!string.IsNullOrEmpty(testString))
			{
				testOffset += DrawOriginHandle(WindowRect.position + testOffset, ref dragTest);
				var tOrigin = origin + testOffset;
				foreach (var c in testString)
				{
					var fc = chars.FirstOrDefault(cinfo => cinfo.index == c);
					if (fc.index <= 0)
						continue;
					if (!hasDrawnVert && fc.index == selectedChar)
					{
						DrawVertEditor(tOrigin);
						hasDrawnVert = true;
					} else
					{
						DrawFontChar(fc, tOrigin);
					}
					tOrigin.x += fc.advance * GetKerning();
				}
			}
			if (!hasDrawnVert && GetSelectionIndex() >= 0)
			{
				origin += vertOffset;
				vertOffset += DrawOriginHandle(origin, ref dragVert);
				DrawVertEditor(origin);
			}
		}
	}
}
