using System;
using UnityEditor;
using UnityEngine;

namespace FontEdit
{
	public partial class FontEditWindow
	{
		private const float grabBorder = 4f;
		private GrabCorner dragging;

		[Flags]
		enum GrabCorner
		{
			None = 0,
			XMin = 1,
			XMax = 2,
			YMin = 4,
			YMax = 8,
		}

		struct GrabHandle
		{
			public Func<Rect, Rect> rect;
			public MouseCursor cursor;
			public GrabCorner corners;
		}

		static bool HasCorner(GrabCorner c, GrabCorner f)
		{
			return (c & f) != GrabCorner.None;
		}

		static readonly GrabHandle[] grabHandles =
		{
			new GrabHandle // Top left
			{
				rect = r => new Rect(r.x, r.y, grabBorder, grabBorder),
				cursor = MouseCursor.ResizeUpLeft,
				corners = GrabCorner.XMin | GrabCorner.YMin
			},
			new GrabHandle // Top
			{
				rect = r => new Rect(r.x + grabBorder, r.y, r.width - (grabBorder*2f), grabBorder),
				cursor = MouseCursor.ResizeVertical,
				corners = GrabCorner.YMin
			},
			new GrabHandle // Top right
			{
				rect = r => new Rect(r.xMax - grabBorder, r.y, grabBorder, grabBorder),
				cursor = MouseCursor.ResizeUpRight,
				corners = GrabCorner.XMax | GrabCorner.YMin
			},
			new GrabHandle // Left
			{
				rect = r => new Rect(r.x, r.y + grabBorder, grabBorder, r.height - (grabBorder*2f)),
				cursor = MouseCursor.ResizeHorizontal,
				corners = GrabCorner.XMin
			},
			new GrabHandle // Bottom left
			{
				rect = r => new Rect(r.x, r.yMax - grabBorder, grabBorder, grabBorder),
				cursor = MouseCursor.ResizeUpRight,
				corners = GrabCorner.XMin | GrabCorner.YMax
			},
			new GrabHandle // Bottom
			{
				rect = r => new Rect(r.x + grabBorder, r.yMax - grabBorder,
					r.width - (grabBorder*2f), grabBorder),
				cursor = MouseCursor.ResizeVertical,
				corners = GrabCorner.YMax
			},
			new GrabHandle // Bottom right
			{
				rect = r => new Rect(r.xMax - grabBorder, r.yMax - grabBorder,
					grabBorder, grabBorder),
				cursor = MouseCursor.ResizeUpLeft,
				corners = GrabCorner.XMax | GrabCorner.YMax
			},
			new GrabHandle // Right
			{
				rect = r => new Rect(r.xMax - grabBorder, r.y + grabBorder,
					grabBorder, r.height - (grabBorder*2f)),
				cursor = MouseCursor.ResizeHorizontal,
				corners = GrabCorner.XMax
			},
			new GrabHandle // Center
			{
				rect = r => new Rect(r.x + grabBorder, r.y + grabBorder,
					r.width - (grabBorder*2f), r.height - (grabBorder*2f)),
				cursor = MouseCursor.MoveArrow,
				corners = GrabCorner.XMin | GrabCorner.XMax | GrabCorner.YMin | GrabCorner.YMax
			},
		};

		void DrawHandles(ref Rect uiRect, ref GrabCorner dragging)
		{
			var nr = Normalize(uiRect);

			foreach (var handle in grabHandles)
			{
				var r = handle.rect(nr);
				// AddCursorRect requires positive height and width (from 'Normalize')
				EditorGUIUtility.AddCursorRect(r, handle.cursor);
				if (handle.cursor != MouseCursor.MoveArrow)
					GUI.DrawTexture(r, handles);
				// Start drag
				if (Event.current.type == EventType.mouseDown && r.Contains(Event.current.mousePosition))
				{
					var d = handle.corners;
					if (uiRect.width < 0f) // Invert X
					{
						if (HasCorner(d, GrabCorner.XMin) && !HasCorner(d, GrabCorner.XMax))
							d = (d | GrabCorner.XMax) & ~GrabCorner.XMin;
						else if (HasCorner(d, GrabCorner.XMax) && !HasCorner(d, GrabCorner.XMin))
							d = (d | GrabCorner.XMin) & ~GrabCorner.XMax;
					}
					if (uiRect.height < 0f) // Invert Y
					{
						if (HasCorner(d, GrabCorner.YMin) && !HasCorner(d, GrabCorner.YMax))
							d = (d | GrabCorner.YMax) & ~GrabCorner.YMin;
						else if (HasCorner(d, GrabCorner.YMax) && !HasCorner(d, GrabCorner.YMin))
							d = (d | GrabCorner.YMin) & ~GrabCorner.YMax;
					}
					dragging = d;
					// Using RecordObject doesn't work fully here, for some reason
					Undo.RegisterCompleteObjectUndo(this, "Drag font character");
				}
			}

			if (dragging != GrabCorner.None)
			{
				// Move drag
				if (Event.current.type == EventType.mouseDrag)
				{
					var d = dragging;
					if (HasCorner(d, GrabCorner.XMin))
						uiRect.xMin += Event.current.delta.x;
					if (HasCorner(d, GrabCorner.XMax))
						uiRect.xMax += Event.current.delta.x;
					if (HasCorner(d, GrabCorner.YMin))
						uiRect.yMin += Event.current.delta.y;
					if (HasCorner(d, GrabCorner.YMax))
						uiRect.yMax += Event.current.delta.y;
				}
				else if (Event.current.type == EventType.mouseUp)
				{
					// Stop drag
					dragging = GrabCorner.None;
				}
			}
		}
	}
}
