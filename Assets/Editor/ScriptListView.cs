using UnityEngine;
using UnityEditor;
using System.Collections;

public class ScriptListView {
	
	private Vector2 scrollPos;
	
	public void GUILayout (params GUILayoutOption[] options)
	{
		scrollPos = GUILayout.BeginScrollView (scrollPos, false, false, options);
		
		// Texture2D icon = EditorGUIUtility.IconContent ("TextAsset Icon").image as Texture2D;
		// EditorGUIUtility.Load("Builtin Skins/Icons/" + name + ".png") as Texture2D
		// EditorGUIUtility.Load("Builtin Skins/Icons/d_" + name + ".png") as Texture2D
		//		GUILayout.Box (LoadIcon ("TextAsset Icon"));
		
		Texture2D prefabIcon = LoadIcon ("Prefab Icon");
		Texture2D sceneIcon = LoadIcon ("Scene Icon");
		
		GUILayout.Box (LoadIcon ("Scene Icon"));
	}
	
	
	private bool ScriptLineItem (ScriptReference scriptRef, GUIStyle style, params GUILayoutOption[] options)
	{
		Rect position = GUILayoutUtility.GetRect (content, style, options);
		int controlId = GUIUtility.GetControlID (10448, FocusType.Native, position);
		GUIContent content = new GUIContent (scriptRef.script.name, IconForScript (scriptRef.scriptType));
		
		switch (Event.current.GetTypeForControl (controlId)) {
		case EventType.MouseDown:
			// TODO ping the object in the project pane
			// Start the double click cooldown
			GUIUtility.hotControl = controlId;
			Event.current.Use ();
			return position.Contains (Event.current.mousePosition);
			break;
		case EventType.mouseUp:
			if (GUIUtility.hotControl != controlId)
				return false;
			GUIUtility.hotControl = 0;
			break;
		case EventType.Repaint:
			style.Draw (position, content, controlId);
			break;
		}
		return false;
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
