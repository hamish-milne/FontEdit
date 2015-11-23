using UnityEditor;
using UnityEngine;

namespace FontEdit
{
	[CustomEditor(typeof(FontEditWindow))]
	public class FontEditInspector : Editor
	{
		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();
			var property = serializedObject.FindProperty("selectedIndex");
			var newStr = EditorGUILayout.TextField("Selected character", ((char)property.intValue).ToString());
			var c = newStr.Length < 1 ? '\0' : newStr[0];
			property.intValue = c;
			serializedObject.ApplyModifiedProperties();

			if (GUILayout.Button("Apply"))
			{
				((FontEditWindow)target).Apply();
			}
		}
	}
}
