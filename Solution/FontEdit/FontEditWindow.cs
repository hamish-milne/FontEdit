using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace FontEdit
{
	/// <summary>
	/// The FontEdit window mode
	/// </summary>
	public enum WindowMode
	{
		Texture,
		Screen,
	}

	/// <summary>
	/// How to display the UV rect in the inspector
	/// </summary>
	public enum DisplayUnit
	{
		Coords,
		Pixels,
	}

	/// <summary>
	/// The main FontEdit window
	/// </summary>
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

		// Nicer storage of font characters
		[Serializable]
		protected struct FontCharacter
		{
			public int index;
			public Rect uv, vert;
			public bool rotated;
			public float advance;
		}

		// ================
		// ==== Fields ====
		// ================
		[SerializeField] protected Font currentFont;
		[SerializeField] protected FontCharacter[] chars;
		/*[SerializeField]*/ protected bool showAll = true;
		[SerializeField] protected int selectedChar = -1;
		[SerializeField] protected WindowMode windowMode;
		[SerializeField] protected DisplayUnit displayUnit;
		[SerializeField] protected bool changed;

		[NonSerialized]
		private Texture2D selection, handles, axisX, axisY, originHandle;
		private static readonly Color32 selectionColor = new Color32(45, 146, 250, 80);
		private const float margin = 10f;
		private const float axisWidth = 2f;

		// ====================
		// ==== Properties ====
		// ====================
		protected Rect WindowRect => new Rect(margin, margin,
			position.width - margin * 2f, position.height - margin * 2f);

		public bool HasChanges => changed;

		public bool CanEdit => currentFont != null;

		public WindowMode WindowMode
		{
			get { return windowMode; }
			set { windowMode = value; }
		}

		public Texture2D Texture => currentFont.material?.mainTexture as Texture2D;

		protected float Scale => Mathf.Min(WindowRect.width, WindowRect.height)
			/ Mathf.Max(Texture.width, Texture.height);

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

		public static bool IsFontAsset(Font font)
		{
			var ext = Path.GetExtension(AssetDatabase.GetAssetPath(font))?.ToLowerInvariant();
			return ext == ".ttf" || ext == ".otf";
		}

		public void Touch()
		{
			if(currentFont != null)
				changed = true;
		}

		// =========================
		// ==== Font properties ====
		// =========================
		private SerializedProperty ascent, kerning;

		float GetAscent()
		{
			if (ascent == null || ascent.serializedObject.targetObject != currentFont)
				ascent = (new SerializedObject(currentFont)).FindProperty("m_Ascent");
            return ascent.floatValue;
		}

		float GetKerning()
		{
			if (kerning == null || kerning.serializedObject.targetObject != currentFont)
			{
				var sobj = new SerializedObject(currentFont);
				kerning = sobj.FindProperty("m_Kerning") ?? sobj.FindProperty("m_Tracking");
			}
			return kerning.floatValue;
		}

		// ============================
		// ==== Store and retrieve ====
		// ============================
		void GetCharacters()
		{
			Revert();
			if(currentFont != null)
			{
				var characterInfo = currentFont.characterInfo;
				chars = new FontCharacter[characterInfo.Length];
				for (int i = 0; i < chars.Length; i++)
				{
					var c = characterInfo[i];
					chars[i] = new FontCharacter
					{
						index = c.index,
						/*uv = new Rect(
							c.uvBottomLeft.x,
							c.uvBottomLeft.y,
							c.uvTopRight.x - c.uvBottomLeft.x,
							c.uvTopRight.y - c.uvBottomLeft.y),*/
#pragma warning disable 618
						// The normal vert properties are mixed in with an inaccessible 'ascent' field
						// that is presumably set by Unity when it retrieves the data
						vert = new Rect(c.vert.x, c.vert.y + GetAscent(), c.vert.width, c.vert.height),
						uv = c.uv,
						rotated = c.flipped,
						advance = c.width
#pragma warning restore 618
						//rotated = Math.Abs(c.uvTopLeft.x - c.uvTopRight.x) < float.Epsilon,
						//advance = c.advance
					};
				}
			}
		}

		public void Apply()
		{
			if (currentFont != null)
			{
				var assetPath = AssetDatabase.GetAssetPath(currentFont);
				if (IsFontAsset(currentFont))
				{
					switch (EditorUtility.DisplayDialogComplex("FontEdit",
						"The selected font is an imported asset, so any changes you make " +
						"will be undone when you close Unity or re-import the asset. " +
						"Create an editable copy in the same folder?",
						"Yes", "Cancel", "No"))
					{
						case 0:
							var basePath = Path.GetDirectoryName(assetPath) + "/" +
								Path.GetFileNameWithoutExtension(assetPath) + "_copy";
							var i = 1;
							var path = basePath + ".fontsettings";
							while (File.Exists(path))
								path = basePath + (i++) + ".fontsettings";
							Selection.activeObject =
								((TrueTypeFontImporter) AssetImporter.GetAtPath(assetPath))
								.GenerateEditableFont(path);
							break;
						case 1:
							return;
					}
				}
				currentFont.characterInfo = chars.Select(fc => new CharacterInfo
				{
					index = fc.index,
					//uvBottomLeft = fc.uv.min,
					//uvTopRight = fc.uv.max,
#pragma warning disable 618
					uv = fc.uv,
					// There's no way to set the flipped state without this field :/
					flipped = fc.rotated,
					// The normal vert properties are mixed in with an inaccessible 'ascent' field
					// that is presumably set by Unity when it retrieves the data
					// (As well as being incredibly difficult to change in general)
					vert = new Rect(fc.vert.x, fc.vert.y - GetAscent(), fc.vert.width, fc.vert.height),
					width = fc.advance
#pragma warning restore 618
					//advance = (int)fc.advance,
			}).ToArray();
				EditorUtility.SetDirty(currentFont);
				Revert();
			}
		}

		public void Revert()
		{
			chars = null;
			selectedRect = null;
			changed = false;
		}

		// ========================
		// ==== Add and remove ====
		// ========================
		public int AddSelected()
		{
			if (GetSelectionIndex() >= 0)
				throw new InvalidOperationException("Adding a character that already exists");
			var ret = chars.Length;
			Array.Resize(ref chars, ret + 1);
			chars[ret] = new FontCharacter
			{
				index = selectedChar,
				uv = new Rect(0.375f, 0.375f, 0.25f, 0.25f),
				vert = new Rect(0f, 0f, 50f, 100f),
				advance = 50f
			};
			changed = true;
			return ret;
		}

		public void DeleteSelected()
		{
			var i = GetSelectionIndex();
			if (i < 0)
				throw new InvalidOperationException("Deleting a character that does not exist");
			var newArray = chars;
			var newLength = newArray.Length - 1;
			for (; i < newLength; i++)
				newArray[i] = newArray[i + 1];
			Array.Resize(ref newArray, newLength);
			chars = newArray;
			selectedChar = -1;
			changed = true;
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
				CreateColorPixel(selectionColor, out selection);
			if (handles == null)
			{
				var hColor = selectionColor;
				hColor.a *= 2;
				CreateColorPixel(hColor, out handles);
			}
			if (axisX == null)
				CreateColorPixel(Color.red, out axisX);
			if (axisY == null)
				CreateColorPixel(Color.green, out axisY);
			if (originHandle == null)
				CreateColorPixel(Color.blue, out originHandle);
		}

		// =========================
		// ==== Message methods ====
		// =========================
		private void SelectionChanged()
		{
			// Specifically disallow multiple fonts to avoid confusion
			// (the basic inspector will still work, however)
			var newFont = Selection.objects.Length == 1 ? Selection.activeObject as Font : null;
			if (newFont != currentFont)
			{
				if (currentFont != null && changed)
				{
					if (EditorUtility.DisplayDialog("FontEdit",
						"Apply changes to " + currentFont.name + "?",
						"Yes", "No"))
						Apply();
                }
				currentFont = newFont;
				GetCharacters();
			}
		}

		protected virtual void OnEnable()
		{
			Instance = this;
		}

		protected virtual void Update()
		{
			// This keeps the editor window responsive
			Repaint();
		}

		protected virtual void OnGUI()
		{
			// Use this rather than Selection.selectionChanged for compatibility
			SelectionChanged();
			InitTextures();
			var labelStyle = new GUIStyle(EditorStyles.boldLabel) {alignment = TextAnchor.MiddleCenter};

			if (currentFont == null)
			{
				EditorGUI.LabelField(WindowRect, "No font selected", labelStyle);
			}
			else if(Texture == null)
			{
				EditorGUI.LabelField(WindowRect, "The selected font has no main texture", labelStyle);
			} else
			{
				if(chars == null && currentFont != null)
					GetCharacters();
				switch (WindowMode)
				{
					case WindowMode.Texture:
						DrawUvEditor();
						break;
					case WindowMode.Screen:
						DrawVertPanel();
						break;
				}
			}

			// For some reason the 'ascent' and 'kerning' properties get stuck..
			ascent = null;
			kerning = null;
		}
	}
}
