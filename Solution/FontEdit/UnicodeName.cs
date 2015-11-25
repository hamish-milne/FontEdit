using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace FontEdit
{
	/// <summary>
	/// Gets unicode name strings for characters
	/// </summary>
	/// <remarks>
	/// The data is retrieved from the official unicode website and cached throughout the session
	/// </remarks>
	public class UnicodeName : ScriptableObject
	{
		/// <summary>
		/// Represents the current status
		/// </summary>
		public enum Status
		{
			/// <summary>
			/// The download has not yet started. Call `Init`
			/// </summary>
			NotStarted,
			/// <summary>
			/// Currently downloading
			/// </summary>
			Downloading,
			/// <summary>
			/// An error occurred
			/// </summary>
			Error,
			/// <summary>
			/// `GetName` is ready to use
			/// </summary>
			Done,
		}

		private static string parseError;

		/// <summary>
		/// The current status of the download
		/// </summary>
		public static Status CurrentStatus
		{
			get
			{
				if(map != null)
					return Status.Done;
				if(www == null)
					return Status.NotStarted;
				if (www.isDone)
					return Status.Error;
				return Status.Downloading;
			}
		}

		/// <summary>
		/// The progress of the download from 0 to 1
		/// </summary>
		public static float DownloadProgress => www?.progress ?? 0f;

		/// <summary>
		/// A more detailed status message
		/// </summary>
		public static string StatusMessage
		{
			get
			{
				switch (CurrentStatus)
				{
				case Status.Done:
					return "Done.";
				case Status.NotStarted:
					return "Not started";
				case Status.Error:
					return parseError ?? www.error;
				case Status.Downloading:
					return "Downloading: " + Mathf.RoundToInt(www.progress*100);
				}
				return "";
			}
		}

		private static WWW www;

		private static Dictionary<char, string> map;

		[Serializable]
		protected struct NamePair
		{
			public int character;
			public string name;
		}

		[SerializeField] protected NamePair[] storedNames;

		/// <summary>
		/// Call this before use, and after the download is complete (the safest is at each timestep)
		/// </summary>
		public static void Init()
		{
			if (CurrentStatus != Status.Done)
			{
				// Load from store, if it exists
				var objects = Resources.FindObjectsOfTypeAll<UnicodeName>();
				if (objects.Length > 0 && objects[0].storedNames != null && objects[0].storedNames.Length > 0)
				{
					map = new Dictionary<char, string>();
					foreach (var pair in objects[0].storedNames)
						map[(char) pair.character] = pair.name;
				}
				else if (www == null)
				{
					www = new WWW("http://www.unicode.org/Public/UNIDATA/UnicodeData.txt");
				}
				else if (www.isDone && string.IsNullOrEmpty(www.error))
				{
					try
					{
						var unicodedata = www.text.Split('\n');
						map = new Dictionary<char, string>(ushort.MaxValue);
						// Copied from https://stackoverflow.com/questions/208768
						for (var i = 0; i < unicodedata.Length; i++)
						{
							var line = unicodedata[i].Trim();
							if(string.IsNullOrEmpty(line))
								continue;
							var fields = line.Split(';');
							if (fields.Length < 2)
								throw new Exception("Parse error line " + i);
							var charCode = int.Parse(fields[0], NumberStyles.HexNumber);
							var charName = fields[1];
							if (charCode < 0 || charCode > 0xFFFF) continue;
							var isRange = charName.EndsWith(", First>");
							if (isRange) // Add all characters within a specified range
							{
								charName = charName.Replace(", First", string.Empty); // Remove range indicator from name
								fields = unicodedata[++i].Split(';');
								if (fields.Length < 2)
									throw new Exception("Parse error line " + i);
								var endCharCode = int.Parse(fields[0], NumberStyles.HexNumber);
								if (!fields[1].EndsWith(", Last>"))
									throw new Exception("Expected end-of-range indicator, line " + i);
								for (var codeInRange = charCode; codeInRange <= endCharCode; codeInRange++)
									map.Add((char)codeInRange, charName);
							}
							else
								map.Add((char)charCode, charName);
						}
						// Cache names for performance (and to save a bit of network traffic)
						var store = objects.Length > 0 ?
							objects[0] : CreateInstance<UnicodeName>();
						store.storedNames = map.Select(pair =>
							new NamePair {character = pair.Key, name = pair.Value}).ToArray();
					}
					catch (Exception e)
					{
						Debug.LogException(e);
						parseError = e.Message;
					}
				}
			}
		}

		/// <summary>
		/// Gets the full name for the given unicode character
		/// </summary>
		/// <param name="c"></param>
		/// <returns></returns>
		public static string GetName(char c)
		{
			if(map == null)
				throw new InvalidOperationException("Call Init first (or check for errors)");
			string ret;
			map.TryGetValue(c, out ret);
			return ret;
		}
	}
}
