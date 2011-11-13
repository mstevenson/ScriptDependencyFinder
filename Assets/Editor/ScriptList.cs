using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;

public class ScriptListElement
{
	public int row = 0;
	public ScriptReference scriptRef;
	
	public void Draw ()
	{
		Event current = Event.current;
		GUIContent content = new GUIContent (scriptRef.script.name, IconForScript (scriptRef.scriptType));
		Rect position = GUILayoutUtility.GetRect (100, 50);
		int controlId = GUIUtility.GetControlID (FocusType.Native);
		
		if (current.type == EventType.MouseDown && position.Contains (current.mousePosition)) {
			if (current.button == 0) {
				// TODO ping the object in the project pane
				Debug.Log ("click");
				if (current.clickCount == 2) {
					// TODO Open the script, scene, or prefab and highlight dependencies
					Debug.Log ("double click");
				}
			}
		}
		if (current.type == EventType.Repaint) {
			// FIXME optimized by caching a reference to these GUIStyles
			//			GUIStyle style = row % 2 != 0 ? new GUIStyle ("CN EntryBackEven") : new GUIStyle ("CN EntryBackOdd");
			GUIStyle style = new GUIStyle ("box");
			style.Draw (position, content, false, false, false, false);
		}
	}
	
	
	Texture2D LoadIcon (string name)
	{
		/*
		 * console.infoicon.png
		 * console.warnicon.png
		 * console.erroricon.png
		 * console.infoicon.sml.png
		 * console.warnicon.sml.png
		 * console.erroricon.sml.png
		 * 
		 * TextAsset Icon.png
		 * js Script Icon.png
		 * cs Script Icon.png
		 * boo Script Icon.png
		 * Prefab Icon.png
		 * Scene Icon.png
		 */		
		
		//Based on EditorGUIUtility.LoadIconForSkin
		if (!UsingProSkin)
			return EditorGUIUtility.LoadRequired ("Builtin Skins/Icons/" + name + ".png") as Texture2D;
		Texture2D tex = EditorGUIUtility.LoadRequired ("Builtin Skins/Icons/_d" + name + ".png") as Texture2D;
		if (tex == null)
			tex = EditorGUIUtility.LoadRequired ("Builtin Skins/Icons/" + name + ".png") as Texture2D;
		return tex;
	}

	
	Texture2D IconForScript (ScriptType scriptType)
	{
		switch (scriptType) {
		case ScriptType.CS:
			return LoadIcon ("cs Script Icon");
		case ScriptType.JS:
			return LoadIcon ("js Script Icon");
		case ScriptType.Boo:
			return LoadIcon ("boo Script Icon");
		}
		return null;
	}


	bool UsingProSkin {
		get { return GUI.skin.name == "SceneGUISkin"; }
	}
}


public class ScriptList {
	/// <summary>
	/// Show only unused scripts.
	/// </summary>
	public static bool unusedOnly = false;
	public Vector2 scrollPos;
	
	public List<ScriptListElement> elements = new List<ScriptListElement> ();
}
