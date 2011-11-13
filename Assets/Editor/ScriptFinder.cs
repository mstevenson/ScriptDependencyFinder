using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Linq;


// TODO Auto refresh when deleting or changing files. Is there a callback for this? May have to use my Watcher


public enum AssetType
{
	Scene,
	Prefab,
	CS,
	JS,
	Boo,
	Unknown
}


/// <summary>
/// Reference to a MonoBehaviour script and the objects which depend upon it.
/// </summary>
public sealed class ScriptReference
{
	public readonly MonoScript script;
	public readonly AssetType scriptType;
	
	public Texture2D Icon {
		get { return ScriptFinder.LoadIconForAsset (scriptType); }
	}
	
	/// <summary>
	/// Prefabs containing the script.
	/// </summary>
	public List<Dependent> prefabDependents = new List<Dependent> ();
	/// <summary>
	/// Scene files containing the script.
	/// </summary>
	public List<Dependent> sceneDependents = new List<Dependent> ();
	
	
	// Constructor
	public ScriptReference (MonoScript script)
	{
		this.script = script;
		scriptType = ScriptFinder.AssetTypeFromObject (script);
	}
	
	
	/// <summary>
	/// Is this script referenced by any object or other script?
	/// </summary>
	public bool IsUsed {
		get {
			if (prefabDependents.Count == 0 && sceneDependents.Count == 0)
				return false;
			else
				return true;
		}
	}
	
	
	/// <summary>
	/// Finds and stores all objects which depend upon this MonoBehaviour script.
	/// </summary>
	public void LoadDependents ()
	{
		// Build scene refs
		foreach (var scene in GetAllSceneAssets ()) {
			if (SceneOrPrefabContainsScript (scene, script))
				sceneDependents.Add (new Dependent(scene));
		}
		// Build prefab refs
		foreach (var prefab in GetAllPrefabAssets ()) {
			if (SceneOrPrefabContainsScript (prefab, script))
				prefabDependents.Add (new Dependent(prefab));
		}
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
	private List<T> GetAllAssetsOfTypeWithExtension<T> (string extension) where T : UnityEngine.Object
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
}


/// <summary>
/// An object which depends upon a particular MonoBehaviour script.
/// </summary>
public sealed class Dependent
{
	public readonly UnityEngine.Object obj;
	public readonly AssetType type;
	
	public Texture2D Icon {
		get {
			return ScriptFinder.LoadIconForAsset (type);
		}
	}
	
	// Constructor
	public Dependent (UnityEngine.Object obj)
	{
		this.obj = obj;
		type = ScriptFinder.AssetTypeFromObject (obj);
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
	/// Find all MonoBehaviour files contained in the project.
	/// </summary>
	private static List<MonoScript> FindAllMonoBehaviourScriptsInProject ()
	{
		List<MonoScript> scripts = new List<MonoScript> ();
		foreach (var obj in FindObjectsOfTypeIncludingAssets (typeof(MonoScript))) {
			if (obj as MonoScript) {
				MonoScript script = (MonoScript)obj;
				System.Type type = script.GetClass ();
				if (type != null) {
					if (script.GetClass ().IsSubclassOf (typeof(MonoBehaviour))) {
						scripts.Add (script);
					}
				}
			}
		}
		return scripts;
	}
	
	
	#region GUI
	
	public List<ScriptListElement> elements = new List<ScriptListElement> ();
	
	private static bool unusedOnly = false;
	private static bool showSelected = false;
	private Vector2 scrollPos;
	
	void OnGUI ()
	{
		// Toolbar
		GUILayout.BeginHorizontal ("Toolbar");
		{
			// Clear
			if (GUILayout.Button ("Clear", EditorStyles.toolbarButton, GUILayout.Width (45)))
				Clear ();
			// Refresh
			if (GUILayout.Button ("Refresh", EditorStyles.toolbarButton, GUILayout.Width (45)))
				Refresh ();
			GUILayout.Space (6);
			// Only unused
			unusedOnly = GUILayout.Toggle (unusedOnly, "Only unused", EditorStyles.toolbarButton, GUILayout.Width (70));
			// Show selected
			showSelected = GUILayout.Toggle (showSelected, "Show selected", EditorStyles.toolbarButton, GUILayout.Width (75));
			EditorGUILayout.Space ();
		}
		GUILayout.EndHorizontal ();
		
		// Script list
		scrollPos = GUILayout.BeginScrollView (scrollPos);
		{
			for (int i = 0; i < elements.Count; i++) {
				ScriptListElement item = elements[i];
				item.row = i;
				
				// Ignore used scripts
				if (unusedOnly) {
					if (item.scriptRef.IsUsed)
						continue;
				}
				
				if (item.scriptRef == null)
					continue;
				
				
				// FIXME this can be optimized by using Rect position = GUILayoutUtility.GetRect ()
				// Also, cache my style modifications
				GUIStyle rowStyle = item.row % 2 != 0 ? new GUIStyle ("CN EntryBackEven") : new GUIStyle ("CN EntryBackOdd");
				rowStyle.margin = new RectOffset (0, 0, 0, 0);
				rowStyle.padding = new RectOffset (0, 0, 0, 0);
				
				GUIStyle referenceStyle = new GUIStyle ("label");
				referenceStyle.margin.left = 22;
				
				GUILayout.BeginVertical (rowStyle);
				{
					// Master script
					ListButton (item.scriptRef.script, new GUIContent (item.scriptRef.script.name, item.scriptRef.Icon), "label");
					// Scenes
					if (item.scriptRef.sceneDependents.Count > 0) {
						foreach (Dependent scene in item.scriptRef.sceneDependents) {
							ListButton (scene.obj, new GUIContent (scene.obj.name, scene.Icon), referenceStyle);
						}
					}
					// Prefabs
					if (item.scriptRef.prefabDependents.Count > 0) {
						foreach (Dependent prefab in item.scriptRef.prefabDependents) {
							ListButton (prefab.obj, new GUIContent (prefab.obj.name, prefab.Icon), referenceStyle);
						}
					}
				}
				GUILayout.EndVertical ();
			}
		}
		GUILayout.EndScrollView ();
	}
	
	
	/// <summary>
	/// Generic button UI element for items which can be selected, clicked, and double clicked
	/// </summary>
	private void ListButton (UnityEngine.Object obj, GUIContent content, GUIStyle style)
	{
		Rect position = GUILayoutUtility.GetRect (content, style);
		//		int controlId = GUIUtility.GetControlID (FocusType.Native);
		
		// Click line element
		if (Event.current.type == EventType.MouseDown && position.Contains (Event.current.mousePosition)) {
			if (Event.current.button == 0) {
				// Show in project pane
				EditorGUIUtility.PingObject (obj);
				// Open the file
				if (Event.current.clickCount == 2) {
					// FIXME if a scene, need to ask to open scene.
					// If code, open in default editor and show first reference.
					// If prefab, ask to load prefab into a blank scene.
					Debug.Log ("double clicked");
//					EditorUtility.OpenWithDefaultApp (AssetDatabase.GetAssetPath (obj));
				}
			}
		}
		// Draw line element
		if (Event.current.type == EventType.Repaint) {
			// FIXME optimized by caching a reference to these GUIStyles
//			GUIStyle style = item.row % 2 != 0 ? new GUIStyle ("CN EntryBackEven") : new GUIStyle ("CN EntryBackOdd");
			style.Draw (position, content, false, false, false, false);
		}
	}
	
	
	private void Clear ()
	{
		elements.Clear ();
	}
	
	
	private void Refresh ()
	{
		var allScripts = FindAllMonoBehaviourScriptsInProject ();
		
		List<ScriptReference> references = new List<ScriptReference> ();
		foreach (MonoScript script in allScripts) {
			ScriptReference s = new ScriptReference (script);
			
			s.LoadDependents ();
			
			references.Add (s);
		}
		
		elements.Clear ();
		foreach (var r in references) {
			ScriptListElement element = new ScriptListElement ();
			element.scriptRef = r;
			elements.Add (element);
		}
	}
	
	
	#endregion
	
	
	public static Texture2D LoadIcon (string name)
	{
		//Based on EditorGUIUtility.LoadIconForSkin
		if (!UsingProSkin)
			return EditorGUIUtility.LoadRequired ("Builtin Skins/Icons/" + name + ".png") as Texture2D;
		Texture2D tex = EditorGUIUtility.LoadRequired ("Builtin Skins/Icons/_d" + name + ".png") as Texture2D;
		if (tex == null)
			tex = EditorGUIUtility.LoadRequired ("Builtin Skins/Icons/" + name + ".png") as Texture2D;
		return tex;
	}


	public static Texture2D LoadIconForAsset (AssetType assetType)
	{
		switch (assetType) {
		case AssetType.Scene:
			return LoadIcon ("Scene Icon");
		case AssetType.Prefab:
			return LoadIcon ("Prefab Icon");
		case AssetType.CS:
			return LoadIcon ("cs Script Icon");
		case AssetType.JS:
			return LoadIcon ("js Script Icon");
		case AssetType.Boo:
			return LoadIcon ("boo Script Icon");
		case AssetType.Unknown:
			// TODO make this a blank document rather than a text document
			return LoadIcon ("TextAsset Icon");
		}
		return null;
	}


	public static bool UsingProSkin {
		get { return GUI.skin.name == "SceneGUISkin"; }
	}
	
	
	public static AssetType AssetTypeFromObject (UnityEngine.Object obj)
	{
		string ext = Path.GetExtension (AssetDatabase.GetAssetPath (obj)).ToLower ();
		switch (ext) {
		case ".unity":
			return AssetType.Scene;
		case ".prefab":
			return AssetType.Prefab;
		case ".cs":
			return AssetType.CS;
		case ".js":
			return AssetType.JS;
		case ".boo":
			return AssetType.Boo;
		default:
			return AssetType.Unknown;
		}
	}
}
