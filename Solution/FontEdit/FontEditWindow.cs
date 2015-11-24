using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace FontEdit
{
	[Serializable]
	public struct FontCharacter
	{
		public int index;
		public Rect uvRect;
		public bool rotated;
	}

	public class FontEditWindow : EditorWindow
	{
		[MenuItem("Window/FontEdit")]
		public static void OpenWindow()
		{
			GetWindow();
		}

		public static FontEditWindow GetWindow()
		{
			return GetWindow<FontEditWindow>("FontEdit", true,
				AppDomain.CurrentDomain.GetAssemblies()
					.First(a => a.GetName().Name == "UnityEditor")
					.GetType("UnityEditor.SceneView"));
		}

		public static FontEditWindow Instance { get; private set; }

		public static Font SelectedFont
		{
			get { return Selection.activeObject as Font; }
			set { Selection.activeObject = value; }
		}

		[NonSerialized]
		private Font storedFont;

		[SerializeField, HideInInspector]
		protected FontCharacter[] chars;

		[SerializeField, HideInInspector]
		protected Texture2D selection, handles, axisX, axisY;

		[SerializeField]
		protected bool showAll;

		[SerializeField]
		protected int selectedChar = -1;

		public int GetSelectionIndex()
		{
			for(int i = 0; i < chars.Length; i++)
				if (chars[i].index == selectedChar)
					return i;
			return -1;
		}

		const float margin = 10f;
		protected Rect WindowRect => new Rect(margin, margin, position.width - margin*2f, position.height - margin*2f);

		void GetCharacters()
		{
			if (SelectedFont == null)
				chars = null;
			else if (storedFont != SelectedFont || chars == null || storedFont.characterInfo.Length != chars.Length)
			{
				var characterInfo = SelectedFont.characterInfo;
				chars = new FontCharacter[characterInfo.Length];
				for (int i = 0; i < chars.Length; i++)
				{
					var c = characterInfo[i];
					chars[i] = new FontCharacter
					{
						index = c.index,
						uvRect = new Rect(
							c.uvBottomLeft.x,
							c.uvBottomLeft.y,
							c.uvTopRight.x - c.uvBottomLeft.x,
							c.uvTopRight.y - c.uvBottomLeft.y),
						rotated = Math.Abs(c.uvTopLeft.x - c.uvTopRight.x) < float.Epsilon
					};
				}
				storedFont = SelectedFont;
				selectedRect = null;
			}
		}

		public void Apply()
		{
			GetCharacters();
			if (SelectedFont != null)
			{
				var assetPath = AssetDatabase.GetAssetPath(SelectedFont);
                var ext = Path.GetExtension(assetPath)?.ToLower();
				if (ext == ".ttf" || ext == ".otf")
				{
					switch (EditorUtility.DisplayDialogComplex("FontEdit",
						"The selected font is an imported asset, so any changes you make " +
						"will be undone when you close Unity or re-import the asset. " +
						"Create an editable copy in the same folder?",
						"Yes", "Cancel", "No"))
					{
						case 0:
							var basePath = Path.GetDirectoryName(assetPath) + "/" + Path.GetFileNameWithoutExtension(assetPath) + "_copy";
							var i = 1;
							var path = basePath + ".fontsettings";
							while (File.Exists(path))
								path = basePath + (i++) + ".fontsettings";
							SelectedFont = ((TrueTypeFontImporter) AssetImporter.GetAtPath(assetPath)).GenerateEditableFont(path);
							break;
						case 1:
							return;
					}
				}
				SelectedFont.characterInfo = SelectedFont.characterInfo.Join(chars, ci => ci.index, fc => fc.index, (ci, fc) =>
				{
					ci.uvBottomLeft = fc.uvRect.min;
					ci.uvTopRight = fc.uvRect.max;
#pragma warning disable 618 // There's no way to set the flipped state without this field :/
					ci.flipped = fc.rotated;
#pragma warning restore 618
					return ci;
				}).ToArray();
				chars = null;
			}
		}

		Rect GetCenterRect(float width, float height)
		{
			return new Rect(
				(WindowRect.xMin + WindowRect.xMax - width) / 2f,
				(WindowRect.yMin + WindowRect.yMax - height) / 2f,
				width, height);
		}

		static void CreateColorPixel(Color c, out Texture2D tex)
		{
			tex = new Texture2D(1, 1);
			tex.SetPixel(0, 0, c);
			tex.wrapMode = TextureWrapMode.Repeat;
			tex.Apply();
		}

		protected virtual void OnEnable()
		{
			Instance = this;
		}

		[Flags]
		enum GrabCorner
		{
			None = 0,
			XMin = 1,
			XMax = 2,
			YMin = 4,
			YMax = 8,
		}

		static bool HasCorner(GrabCorner c, GrabCorner f)
		{
			return (c & f) != GrabCorner.None;
		}

		private GrabCorner dragging;
		private Rect? selectedRect;

		static Rect Normalize(Rect r)
		{
			return new Rect(
				r.width >= 0f ? r.xMin : r.xMax,
				r.height >= 0f ? r.yMin : r.yMax,
				Mathf.Abs(r.width),
				Mathf.Abs(r.height));
		}

		struct GrabHandle
		{
			public Func<Rect, Rect> rect;
			public MouseCursor cursor;
			public GrabCorner corners;
		}

		private const float grabBorder = 5f;
		static readonly GrabHandle[] grabHandles = 
		{
			new GrabHandle // Top left
			{
				rect = r => new Rect(r.position, new Vector2(grabBorder, grabBorder)),
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
				rect = r => new Rect(r.x + grabBorder, r.yMax - grabBorder, r.width - (grabBorder*2f), grabBorder),
				cursor = MouseCursor.ResizeVertical,
				corners = GrabCorner.YMax
			},
			new GrabHandle // Bottom right
			{
				rect = r => new Rect(r.xMax - grabBorder, r.yMax - grabBorder, grabBorder, grabBorder),
				cursor = MouseCursor.ResizeUpLeft,
				corners = GrabCorner.XMax | GrabCorner.YMax
			},
			new GrabHandle // Right
			{
				rect = r => new Rect(r.xMax - grabBorder, r.y + grabBorder, grabBorder, r.height - (grabBorder*2f)),
				cursor = MouseCursor.ResizeHorizontal,
				corners = GrabCorner.XMax
			},
			new GrabHandle // Center
			{
				rect = r => new Rect(r.x + grabBorder, r.y + grabBorder, r.width - (grabBorder*2f), r.height - (grabBorder*2f)),
				cursor = MouseCursor.MoveArrow,
				corners = GrabCorner.XMin | GrabCorner.XMax | GrabCorner.YMin | GrabCorner.YMax
			},
		};

		void DrawSelection(Rect r, bool rotated)
		{
			GUI.DrawTexture(r, selection);

			const float axisWidth = 2f;
			const float axisLength = 20f;
			var width = Mathf.Sign(r.width) * Mathf.Min(Mathf.Abs(r.width), axisLength);
			var height = Mathf.Sign(r.height) * Mathf.Min(Mathf.Abs(r.height), axisLength);
			GUI.DrawTexture(new Rect(r.position,
				new Vector2(rotated ? width : axisWidth, rotated ? axisWidth : height)),
				axisY);
			GUI.DrawTexture(new Rect(r.position,
				new Vector2(rotated ? axisWidth : width, rotated ? height : axisWidth)),
				axisX);
		}

		public Texture Texture => SelectedFont.material?.mainTexture;

		protected float Scale => Mathf.Min(WindowRect.width, WindowRect.height)/Mathf.Max(Texture.width, Texture.height);

		protected Rect TextureRect => GetCenterRect(Texture.width*Scale, Texture.height*Scale);

		Rect GetUiRect(Rect r)
		{
			var textureRect = TextureRect;
			r.x = textureRect.x + (r.x * textureRect.width);
			r.y = textureRect.y + ((1f - r.y) * textureRect.height);
			r.width *= textureRect.width;
			r.height *= -textureRect.height;
			return r;
		}

		Rect GetUvRect(Rect r)
		{
			var textureRect = TextureRect;
			r.height /= -textureRect.height;
			r.width /= textureRect.width;
			r.y = 1f - ((r.y - textureRect.y)/textureRect.height);
			r.x = (r.x - textureRect.x)/textureRect.width;
			return r;
		}

		void InitTextures()
		{
			if (selection == null)
				CreateColorPixel(new Color32(45, 146, 250, 80), out selection);
			if (handles == null)
				CreateColorPixel(new Color32(45, 146, 250, 160), out handles);
			if (axisX == null)
				CreateColorPixel(Color.red, out axisX);
			if (axisY == null)
				CreateColorPixel(Color.green, out axisY);
		}

		protected virtual void OnGUI()
		{
			InitTextures();

			var labelStyle = new GUIStyle(EditorStyles.boldLabel) {alignment = TextAnchor.MiddleCenter};

			/*if (Event.current.type == EventType.mouseDown && Selection.activeObject != this)
			{
				Selection.activeObject = this;
				return;
			}*/
			if (SelectedFont == null)
			{
				EditorGUI.LabelField(WindowRect, "No font selected", labelStyle);
			}
			else if(Texture == null)
			{
				EditorGUI.LabelField(WindowRect, "The selected font has no main texture", labelStyle);
			} else
			{
				GUI.DrawTexture(TextureRect, Texture, ScaleMode.ScaleToFit);
				GetCharacters();

				for (int i = 0; i < chars.Length; i++)
				{
					var c = chars[i];
					var uiRect = GetUiRect(c.uvRect);
					

					// Handle move & resize
					if (c.index == selectedChar)
					{
						selectedRect = uiRect;
						var nr = Normalize(uiRect);

						foreach (var handle in grabHandles)
						{
							var r = handle.rect(nr);
							EditorGUIUtility.AddCursorRect(r, handle.cursor);
							if (handle.cursor != MouseCursor.MoveArrow)
								GUI.DrawTexture(r, handles);
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
							}
						}

						if (dragging != GrabCorner.None)
						{
							DrawSelection(uiRect, c.rotated);
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
								// Write back to chars
								c.uvRect = GetUvRect(uiRect);
								chars[i] = c;
							}
							else if (Event.current.type == EventType.mouseUp)
							{
								dragging = GrabCorner.None;
							}
						}
					}

					// Draw highlight (mouse hover)
					if (dragging == GrabCorner.None)
					{
						if (c.index == selectedChar || uiRect.Contains(Event.current.mousePosition, true))
						{
							DrawSelection(uiRect, c.rotated);
							if (Event.current.type == EventType.mouseDown && !(selectedRect?.Contains(Event.current.mousePosition, true) ?? false))
								selectedChar = c.index;
						}
						else if (showAll)
						{
							DrawSelection(uiRect, c.rotated);
						}
					}
				}
			}
		}

		protected virtual void Update()
		{
			Repaint();
		}
	}
}
