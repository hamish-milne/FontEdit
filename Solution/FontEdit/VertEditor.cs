using UnityEngine;

namespace FontEdit
{
	public partial class FontEditWindow
	{
		protected Vector2 VertOrigin => new Vector2(WindowRect.x + WindowRect.width/3f, WindowRect.center.y + GetFontAscent()/2f);

		Rect VertToUi(Rect vert)
		{
			return new Rect(vert.x + VertOrigin.x, -vert.y + VertOrigin.y, vert.width, -vert.height);
		}

		Rect UiToVert(Rect ui)
		{
			return new Rect(ui.x - VertOrigin.x, -(ui.y - VertOrigin.y), ui.width, -ui.height);
		}

		Rect RotateVert(Rect vert)
		{
			return new Rect(vert.x + vert.width, vert.y + vert.height, vert.height, -vert.width);
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

				var vert = VertToUi(fc.vert);
				var oldMatrix = GUI.matrix;
				if (fc.rotated)
				{
					var rv = RotateVert(vert);
					GUIUtility.RotateAroundPivot(-90f, rv.min);
					GUI.DrawTextureWithTexCoords(rv, Texture, fc.uv);
					GUI.matrix = oldMatrix;
				} else
				{
					GUI.DrawTextureWithTexCoords(vert, Texture, fc.uv);
				}

				DrawHandles(ref vert, ref dragging);
				if (dragging != GrabCorner.None)
				{
					fc.vert = UiToVert(vert);
					chars[index] = fc;
				}
			}
		}
	}
}
