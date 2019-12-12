#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

public class IgnoreSpriteAtlasHashChanges : UnityEditor.AssetModificationProcessor
{
	// keeps each sprite atlas content that was before saving assets
	private static Dictionary<string, string[]> _dictFileContent;

	static IgnoreSpriteAtlasHashChanges()
	{
		_dictFileContent = new Dictionary<string, string[]>(256);

		EditorApplication.update += CheckAtlases;
	}

	// catches paths of the atlases being serialized and saved to files
	private static string[] OnWillSaveAssets(string[] paths)
	{
		_dictFileContent.Clear();

		foreach (string path in paths)
		{
			if (path.EndsWith(".spriteatlas"))
			{
				if (TryReadAllLines(path, out string[] lines))
				{
					// store content of the file to restore later if needed
					_dictFileContent.Add(path, lines);
				}
			}
		}

		return paths;
	}

	private static bool TryReadAllLines(string path, out string[] lines)
	{
		lines = null;

		try
		{
			lines = File.ReadAllLines(path);
		}
		catch
		{
			Debug.Log("Failed to read sprite atlas " + path);
		}

		return lines != null;
	}

	// vali
	private static void CheckAtlases()
	{
		if (_dictFileContent.Count == 0)
			return;

		//Debug.Log("Will check files: " + _dictFileLines.Count);

		int count = 0;
		var names = "";

		foreach (var pair in _dictFileContent)
		{
			string path = pair.Key;

			if (!TryReadAllLines(path, out string[] newLines))
				continue;

			string[] oldLines = pair.Value;

			if (TryRestoreAtlas(path, oldLines, newLines))
			{
				count++;
				names += path + "\n";
			}
		}

		if (count > 0)
			Debug.Log("Restored " + count + " out of " + _dictFileContent.Count
					+ " atlas(es) because only Hash changed: \n" + names + "\n");

		_dictFileContent.Clear();
	}


	// restores file content to previous version (that was prior to saving asset)
	// if the only difference is the line containing Hash
	private static bool TryRestoreAtlas(string path, string[] oldLines, string[] newLines)
	{
		bool mustRestore = (oldLines.Length == newLines.Length);

		if (mustRestore)
		{
			for (int i=0; i<oldLines.Length; i++)
			{
				string _old = oldLines[i];
				string _new = newLines[i];

				string hash = "      Hash: ";

				if (_old.StartsWith(hash) && _new.StartsWith(hash))
					continue;

				if (_old.Equals(_new, StringComparison.Ordinal))
					continue;

				mustRestore = false;
				break;
			}
		}

		if (mustRestore)
		{
			try
			{
				var timestamp = File.GetLastWriteTime(path);

				using (var writer = new StreamWriter(path))
				{
					switch (EditorSettings.lineEndingsForNewScripts)
					{
						case LineEndingsMode.Unix:
							writer.NewLine = "\n";
							break;

						case LineEndingsMode.Windows:
							writer.NewLine = "\r\n";
							break;

						default:
							writer.NewLine = Environment.NewLine;
							break;
					}


					foreach (var line in oldLines)
						writer.WriteLine(line);
				}

				// set the same timestamp for sprite atlas to make unity believe file is not changed
				// otherwise Unity hides packed texture and spends time to restore it from cache
				File.SetLastWriteTime(path, timestamp);

				return true;
			}
			catch
			{
				Debug.LogError("Failed to restore sprite atlas " + path);
			}
		}

		return false;
	}
}

#endif