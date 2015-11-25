using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FontEdit
{
	public partial class FontEditWindow
	{
		[SerializeField] protected string testString;

		float GetKerning()
		{
			return (new SerializedObject(SelectedFont)).FindProperty("m_Kerning").floatValue;
		}

		void DrawTest()
		{
			var strRect = new Rect(WindowRect.x, WindowRect.y, WindowRect.width, 16f);
			testString = EditorGUI.TextField(strRect, testString);
			(new SerializedObject(this)).ApplyModifiedPropertiesWithoutUndo();
			if (!string.IsNullOrEmpty(testString))
			{
				var origin = new Vector2(WindowRect.x, WindowRect.center.y + GetFontAscent()/2f);
				foreach (var c in testString)
				{
					var fc = chars.FirstOrDefault(cinfo => cinfo.index == c);
					if(fc.index <= 0)
						continue;
					DrawFontChar(fc, origin);
					origin.x += fc.advance * GetKerning();
				}
			}
		}
	}
}
