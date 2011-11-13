using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Linq;


public enum ScriptType
{
	CS,
	JS,
	Boo
}


public class ScriptReference
{
	public readonly MonoScript script;
	public readonly ScriptType scriptType;
	
	/// <summary>
	/// Prefabs containing the script.
	/// </summary>
	public List<UnityEngine.Object> prefabs = new List<UnityEngine.Object> ();
	/// <summary>
	/// Scene files containing the script.
	/// </summary>
	public List<UnityEngine.Object> scenes = new List<UnityEngine.Object> ();
	/// <summary>
	/// Game objects in the current scene containing the script.
	/// </summary>
	public List<UnityEngine.Object> gameObjects = new List<UnityEngine.Object> ();
	/// <summary>
	/// Other scripts that reference this script.
	/// </summary>
	public List<UnityEngine.Object> otherScripts = new List<UnityEngine.Object> ();
	
	
	public ScriptReference (MonoScript script)
	{
		this.script = script;
		string ext = Path.GetExtension (AssetDatabase.GetAssetPath (script)).ToLower ();
		switch (ext) {
		case ".cs":
			scriptType = ScriptType.CS;
			break;
		case ".js":
			scriptType = ScriptType.JS;
			break;
		case ".boo":
			scriptType = ScriptType.Boo;
			break;
		}
	}
	
	
	/// <summary>
	/// Is this script referenced by any object or other script?
	/// </summary>
	public bool IsUsed {
		get {
			if (prefabs.Count == 0 && scenes.Count == 0 && otherScripts.Count == 0)
				return false;
			else
				return true;
		}
	}
}


/// <summary>
/// Finds MonoBehaviour that are not attached to any objects in any scene.
/// </summary>
/// <remarks>
/// This tool does not take into account prefabs or components that are instantiated at runtime. Use with caution.
/// </remarks>
public sealed class ScriptFinder : EditorWindow
{
	#region Window Setup

	private static ScriptFinder window;

	[MenuItem("Custom/Find Unused Scripts")]
	static void Init ()
	{
		ScriptFinder window = (ScriptFinder)EditorWindow.GetWindow (typeof(ScriptFinder), false, "Script Finder");
	}

	#endregion

	
	public enum AssetType {
		Prefab,
		Scene,
		Script
	}
	
	
	/// <summary>
	/// A Scene, Prefab, or MonoScript which relies a particular MonoBehaviour.
	/// </summary>
	private class ScriptDepender {
		public UnityEngine.Object obj;
		public string path;
		AssetType assetType;
	}
	
	
	
	/// <summary>
	/// Get MonoScripts which are used in the given scene or prefab.
	/// </summary>
	private List<MonoScript> GetScriptsInScenesOrPrefabs (UnityEngine.Object[] scenesOrPrefabs)
	{
		List<MonoScript> scripts = new List<MonoScript> ();
		foreach (var s in EditorUtility.CollectDependencies (scenesOrPrefabs)) {
			if (s as MonoScript) {
				scripts.Add ((MonoScript)s);
			}
		}
		return scripts;
	}
	
	
	
	/// <summary>
	/// Get all scene files contained in the project.
	/// </summary>
	private List<UnityEngine.Object> GetAllSceneAssets ()
	{
		return GetAllAssetsOfTypeWithExtension<UnityEngine.Object> (".unity");
	}
	
	
	/// <summary>
	/// Get all prefabs contained in the project.
	/// </summary>
	private List<UnityEngine.Object> GetAllPrefabAssets ()
	{
		return GetAllAssetsOfTypeWithExtension<UnityEngine.Object> (".prefab");
	}
	
	
	private bool SceneOrPrefabContainsScript (UnityEngine.Object sceneOrPrefab, MonoScript script)
	{
		foreach (var d in EditorUtility.CollectDependencies (new UnityEngine.Object[] { sceneOrPrefab })) {
			if (d == script)
				return true;
		}
		return false;
	}
	
	
	/// <summary>
	/// Get all asset files in the project which match the given extension (including the dot)
	/// </summary>
	private List<T> GetAllAssetsOfTypeWithExtension<T> (string extension)
		where T : UnityEngine.Object
	{
		List<T> objs = new List<T> ();
		foreach (string path in AssetDatabase.GetAllAssetPaths ()) {
			if (Path.GetExtension (path) != extension)
				continue;
			T asset = AssetDatabase.LoadAssetAtPath (path, typeof(T)) as T;
			if (asset != null)
				objs.Add (asset);
		}
		return objs;
	}
	
	
	/// <summary>
	/// Find all MonoBehaviour files contained in the project.
	/// </summary>
	private static List<MonoScript> FindAllMonoBehaviourScriptsInProject ()
	{
		List<MonoScript> scripts = new List<MonoScript> ();
		foreach (var obj in FindObjectsOfTypeIncludingAssets (typeof(MonoScript))) {
			if (obj as MonoScript) {
				MonoScript script = (MonoScript)obj;
				if (script.GetClass ().IsSubclassOf (typeof(MonoBehaviour))) {
					scripts.Add (script);
				}
			}
		}
		return scripts;
	}
	
	
	
	
	
	
	// When displaying scenes that use a particular script, click to highlight the scene file.
	// Double click to open and highlight the objects that use the script. Also expands
	// The view 
	
	// Auto refresh when deleting or changing files. Is there a callback for this? May have to use my Watcher
	
	
	#region GUI
	
	private ScriptList list = new ScriptList ();
	
	void OnGUI ()
	{
		// Toolbar
		GUILayout.BeginHorizontal ("Toolbar");
		{
			if (GUILayout.Button ("Clear", EditorStyles.toolbarButton, GUILayout.Width (45)))
				Clear ();
			if (GUILayout.Button ("Refresh", EditorStyles.toolbarButton, GUILayout.Width (45)))
				Refresh ();
			GUILayout.Space (6);
			ScriptList.unusedOnly = GUILayout.Toggle (ScriptList.unusedOnly, "Only unused", EditorStyles.toolbarButton, GUILayout.Width (70));
			EditorGUILayout.Space ();
		}
		GUILayout.EndHorizontal ();
		
		// Script list
		list.scrollPos = GUILayout.BeginScrollView (list.scrollPos);
		{
			for (int i = 0; i < list.elements.Count; i++) {
				ScriptListElement item = list.elements[i];
				item.row = i;
				
				// Ignore used scripts
				if (ScriptList.unusedOnly) {
					if (item.scriptRef.IsUsed)
						continue;
				}
				
				
				// FIXME this can be optimized by using Rect position = GUILayoutUtility.GetRect ()
				// Also, cache my style modifications
				GUIStyle rowStyle = item.row % 2 != 0 ? new GUIStyle ("CN EntryBackEven") : new GUIStyle ("CN EntryBackOdd");
				rowStyle.margin = new RectOffset (0, 0, 0, 0);
				rowStyle.padding = new RectOffset (0, 0, 0, 0);
				
				GUIStyle scriptStyle = new GUIStyle ("label");
				
				GUIStyle referenceStyle = new GUIStyle ("label");
				referenceStyle.margin.left = 22;
				
				GUILayout.BeginVertical (rowStyle);
				{
					// Master script
					GUIContent scriptContent = new GUIContent (item.scriptRef.script.name, IconForScript (item.scriptRef.scriptType));
					ScriptButton (scriptContent, scriptStyle);
					// Scenes
					if (item.scriptRef.scenes.Count > 0) {
						foreach (var scene in item.scriptRef.scenes) {
							GUILayout.Label (new GUIContent (scene.name, LoadIcon ("Scene Icon")), referenceStyle);
						}
					}
					// Prefabs
					if (item.scriptRef.prefabs.Count > 0) {
						foreach (var prefab in item.scriptRef.prefabs) {
							GUILayout.Label (new GUIContent (prefab.name, LoadIcon ("Prefab Icon")), referenceStyle);
						}
					}
				}
				GUILayout.EndVertical ();

				
//				GUIStyle endStyle = (item.row + 1) % 2 != 0 ? new GUIStyle ("CN EntryBackEven") : new GUIStyle ("CN EntryBackOdd");
//				GUILayout.FlexibleSpace ();
				
				
				
//				Event current = Event.current;
//				
//				// Script line content
//				GUIContent scriptContent = new GUIContent (item.scriptRef.script.name, IconForScript (item.scriptRef.scriptType));
//				
//				Rect position = GUILayoutUtility.GetRect (100, 50);
//				int controlId = GUIUtility.GetControlID (FocusType.Native);
//				
//				// Click line element
//				if (current.type == EventType.MouseDown && position.Contains (current.mousePosition)) {
//					if (current.button == 0) {
//						// TODO ping the object in the project pane
//						Debug.Log ("click");
//						if (current.clickCount == 2) {
//							// TODO Open the script, scene, or prefab and highlight dependencies
//							Debug.Log ("double click");
//						}
//					}
//				}
//				// Draw line element
//				if (current.type == EventType.Repaint) {
//					// FIXME optimized by caching a reference to these GUIStyles
//					GUIStyle style = item.row % 2 != 0 ? new GUIStyle ("CN EntryBackEven") : new GUIStyle ("CN EntryBackOdd");
//					style.Draw (position, scriptContent, false, false, false, false);
//				}
			}
		}
		GUILayout.EndScrollView ();
	}
	
	
	private void ScriptButton (GUIContent content, GUIStyle style)
	{
		GUILayout.Button (content, style);
	}
	
	
	
	
	private void Clear ()
	{
		list.elements.Clear ();
		
	}
	
	
	private void Refresh ()
	{
		Debug.Log ("Refreshing");
		
		var allScripts = FindAllMonoBehaviourScriptsInProject ();
		
		List<ScriptReference> references = new List<ScriptReference> ();
		foreach (MonoScript script in allScripts) {
			ScriptReference s = new ScriptReference (script);
			
			// Build scene refs
			foreach (var scene in GetAllSceneAssets ()) {
				if (SceneOrPrefabContainsScript (scene, script))
					s.scenes.Add (scene);
			}
			// Build prefab refs
			foreach (var prefab in GetAllPrefabAssets ()) {
				if (SceneOrPrefabContainsScript (prefab, script))
					s.prefabs.Add (prefab);
			}
			
			references.Add (s);
		}
		
		list.elements.Clear ();
		foreach (var r in references) {
			ScriptListElement element = new ScriptListElement ();
			element.scriptRef = r;
			list.elements.Add (element);
		}
	}
	
	
	#endregion
	
	
	
	
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
