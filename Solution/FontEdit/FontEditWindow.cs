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
		public Rect uv, vert;
		public bool rotated;
		public float advance;
	}

	public enum WindowMode
	{
		UV,
		Vert,
		Test,
	}

	public enum DisplayUnit
	{
		Coords,
		Pixels,
	}

	public partial class FontEditWindow : EditorWindow
	{
		// =========================
		// ==== Window handling ====
		// =========================
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

		// ================
		// ==== Fields ====
		// ================
		[NonSerialized] private Font storedFont;
		[SerializeField, HideInInspector] protected FontCharacter[] chars;
		[SerializeField, HideInInspector] protected Texture2D selection, handles, axisX, axisY;
		[SerializeField] protected bool showAll;
		[SerializeField] protected int selectedChar = -1;
		[SerializeField] protected WindowMode windowMode;
		[SerializeField] protected DisplayUnit displayUnit;

		// ====================
		// ==== Properties ====
		// ====================
		const float margin = 10f;
		protected Rect WindowRect => new Rect(margin, margin, position.width - margin * 2f, position.height - margin * 2f);

		public static Font SelectedFont
		{
			get { return Selection.activeObject as Font; }
			set { Selection.activeObject = value; }
		}

		public WindowMode WindowMode
		{
			get { return windowMode; }
			set { windowMode = value; }
		}

		public Texture2D Texture => SelectedFont.material?.mainTexture as Texture2D;

		protected float Scale => Mathf.Min(WindowRect.width, WindowRect.height) / Mathf.Max(Texture.width, Texture.height);

		protected Rect TextureRect => GetCenterRect(Texture.width * Scale, Texture.height * Scale);

		// ========================
		// ==== Common methods ====
		// ========================
		Rect GetCenterRect(float width, float height)
		{
			return new Rect(
				(WindowRect.xMin + WindowRect.xMax - width) / 2f,
				(WindowRect.yMin + WindowRect.yMax - height) / 2f,
				width, height);
		}

		static Rect Normalize(Rect r)
		{
			return new Rect(
				r.width >= 0f ? r.xMin : r.xMax,
				r.height >= 0f ? r.yMin : r.yMax,
				Mathf.Abs(r.width),
				Mathf.Abs(r.height));
		}

		public int GetSelectionIndex()
		{
			if (chars != null)
				for(int i = 0; i < chars.Length; i++)
					if (chars[i].index == selectedChar)
						return i;
			return -1;
		}

		static float GetFontAscent()
		{
			return (new SerializedObject(SelectedFont)).FindProperty("m_Ascent").floatValue;
		}

		// ============================
		// ==== Store and retrieve ====
		// ============================
		void GetCharacters()
		{
			if (SelectedFont == null)
				chars = null;
			else if (storedFont != SelectedFont || chars == null || chars.Length == 0)
			{
				var characterInfo = SelectedFont.characterInfo;
				chars = new FontCharacter[characterInfo.Length];
				var ascent = GetFontAscent();
				for (int i = 0; i < chars.Length; i++)
				{
					var c = characterInfo[i];
					chars[i] = new FontCharacter
					{
						index = c.index,
						uv = new Rect(
							c.uvBottomLeft.x,
							c.uvBottomLeft.y,
							c.uvTopRight.x - c.uvBottomLeft.x,
							c.uvTopRight.y - c.uvBottomLeft.y),
#pragma warning disable 618
						// The normal vert properties are mixed in with an inaccessible 'ascent' field
						// that is presumably set by Unity when it retrieves the data
						vert = new Rect(c.vert.x, c.vert.y + ascent, c.vert.width, c.vert.height),
#pragma warning restore 618
						rotated = Math.Abs(c.uvTopLeft.x - c.uvTopRight.x) < float.Epsilon,
						advance = c.advance
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
				var ascent = GetFontAscent();
				SelectedFont.characterInfo = chars.Select(fc => new CharacterInfo
				{
					index = fc.index,
					uvBottomLeft = fc.uv.min,
					uvTopRight = fc.uv.max,
#pragma warning disable 618
					// There's no way to set the flipped state without this field :/
					flipped = fc.rotated,
					// The normal vert properties are mixed in with an inaccessible 'ascent' field
					// that is presumably set by Unity when it retrieves the data
					// (As well as being incredibly difficult to change in general)
					vert = new Rect(fc.vert.x, fc.vert.y - ascent, fc.vert.width, fc.vert.height),
#pragma warning restore 618
					advance = (int)fc.advance,
			}).ToArray();
				chars = null;
				EditorUtility.SetDirty(SelectedFont);
			}
		}

		public void Revert()
		{
			chars = null;
		}

		// ========================
		// ==== Add and remove ====
		// ========================
		public int AddSelected()
		{
			if (GetSelectionIndex() >= 0)
				throw new InvalidOperationException();
			var ret = chars.Length;
			Array.Resize(ref chars, ret + 1);
			chars[ret].index = selectedChar;
			chars[ret].uv = new Rect(0.375f, 0.375f, 0.25f, 0.25f);
			return ret;
		}

		public void DeleteSelected()
		{
			var i = GetSelectionIndex();
			if (i < 0)
				throw new InvalidOperationException();
			var newArray = chars;
			var newLength = newArray.Length - 1;
			for (; i < newLength; i++)
				newArray[i] = newArray[i + 1];
			Array.Resize(ref newArray, newLength);
			chars = newArray;
			selectedChar = -1;
		}

		// ==========================
		// ==== Texture creation ====
		// ==========================
		static void CreateColorPixel(Color c, out Texture2D tex)
		{
			tex = new Texture2D(1, 1);
			tex.SetPixel(0, 0, c);
			tex.wrapMode = TextureWrapMode.Repeat;
			tex.Apply();
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

		// =========================
		// ==== Message methods ====
		// =========================
		protected virtual void OnEnable()
		{
			Instance = this;
		}

		protected virtual void Update()
		{
			Repaint();
		}

		protected virtual void OnGUI()
		{
			InitTextures();

			var labelStyle = new GUIStyle(EditorStyles.boldLabel) {alignment = TextAnchor.MiddleCenter};

			if (SelectedFont == null)
			{
				EditorGUI.LabelField(WindowRect, "No font selected", labelStyle);
			}
			else if(Texture == null)
			{
				EditorGUI.LabelField(WindowRect, "The selected font has no main texture", labelStyle);
			} else
			{
				GetCharacters();
				switch (WindowMode)
				{
					case WindowMode.UV:
						DrawUvEditor();
						break;
					case WindowMode.Vert:
						DrawVertEditor();
						break;
					case WindowMode.Test:
						DrawTest();
						break;
				}
			}
		}
	}
}
