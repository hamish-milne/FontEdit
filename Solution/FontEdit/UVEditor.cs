﻿using UnityEngine;

namespace FontEdit
{
	public partial class FontEditWindow
	{
		private const float axisLength = 20f;
		private Rect? selectedRect;

		Rect UvToUi(Rect r)
		{
			var textureRect = TextureRect;
			r.x = textureRect.x + (r.x * textureRect.width);
			r.y = textureRect.y + ((1f - r.y) * textureRect.height);
			r.width *= textureRect.width;
			r.height *= -textureRect.height;
			return r;
		}

		Rect UiToUv(Rect r)
		{
			var textureRect = TextureRect;
			r.height /= -textureRect.height;
			r.width /= textureRect.width;
			r.y = 1f - ((r.y - textureRect.y) / textureRect.height);
			r.x = (r.x - textureRect.x) / textureRect.width;
			return r;
		}

		// Draws the highlight/selection over the given area
		void DrawUvSelection(Rect r, bool rotated)
		{
			GUI.DrawTexture(r, selection);
			var width = Mathf.Sign(r.width) * Mathf.Min(Mathf.Abs(r.width), axisLength);
			var height = Mathf.Sign(r.height) * Mathf.Min(Mathf.Abs(r.height), axisLength);
			GUI.DrawTexture(new Rect(r.x, r.y,
				rotated ? width : axisWidth, rotated ? axisWidth : height),
				axisY);
			GUI.DrawTexture(new Rect(r.x, r.y,
				rotated ? axisWidth : width, rotated ? height : axisWidth),
				axisX);
		}

		void DrawUvEditor()
		{
			if (chars == null || Texture == null)
				return;
			GUI.DrawTexture(TextureRect, Texture, ScaleMode.ScaleToFit);
			for (int i = 0; i < chars.Length; i++)
			{
				var c = chars[i];
				var uiRect = UvToUi(c.uv);

				// Handle move & resize
				if (c.index == selectedChar)
				{
					selectedRect = uiRect;
					DrawHandles(ref uiRect, ref dragging);
					if (dragging != GrabCorner.None)
					{
						DrawUvSelection(uiRect, c.rotated);
						// Write back to chars
						c.uv = UiToUv(uiRect);
						chars[i] = c;
						changed = true;
					}
				}

				// Draw highlight (mouse hover)
				if (dragging == GrabCorner.None)
				{
					if (c.index == selectedChar || uiRect.Contains(Event.current.mousePosition, true))
					{
						DrawUvSelection(uiRect, c.rotated);
						if (Event.current.type == EventType.mouseDown &&
							!(selectedRect?.Contains(Event.current.mousePosition, true) ?? false))
							selectedChar = c.index;
					}
					else if (showAll)
					{
						GUI.DrawTexture(uiRect, selection);
					}
				}
			}
		}
	}
}
