using UnityEngine;

namespace FontEdit
{
	public partial class FontEditWindow
	{
		protected Vector2 VertOrigin => new Vector2(
			WindowRect.x + WindowRect.width/3f,
			WindowRect.center.y + GetFontAscent());

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

		void DrawVertEditor()
		{
			var index = GetSelectionIndex();
			if (index >= 0)
			{
				var fc = chars[index];
				var origin = VertOrigin;
				GUI.DrawTexture(new Rect(origin.x, origin.y, 2f, -GetFontAscent()), axisY);
				GUI.DrawTexture(new Rect(origin.x, origin.y, fc.advance, 2f), axisX);
				var vert = DrawFontChar(fc, VertOrigin);
				DrawHandles(ref vert, ref dragging);
				if (dragging != GrabCorner.None)
				{
					fc.vert = UiToVert(vert, VertOrigin);
					chars[index] = fc;
					changed = true;
				}
			}
			DrawTest();
		}
	}
}
