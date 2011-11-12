using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections.Generic;
using System.IO;


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
		ScriptFinder window = (ScriptFinder)EditorWindow.GetWindow (typeof(ScriptFinder), true, "Script Finder");
	}

	#endregion

	
	private enum AssetType {
		Prefab,
		Scene,
		Script
	}
	
	private enum ScriptType {
		CS,
		JS,
		Boo
	}
	
	
	private class ScriptReference {
		public MonoScript script;
		/// <summary>
		/// Prefabs containing the script.
		/// </summary>
		public UnityEngine.Object[] prefabs;
		/// <summary>
		/// Scene files containing the script.
		/// </summary>
		public UnityEngine.Object[] scenes;
		/// <summary>
		/// Game objects in the current scene containing the script.
		/// </summary>
		public UnityEngine.Object[] gameObjects;
		/// <summary>
		/// Other scripts that reference this script.
		/// </summary>
		public UnityEngine.Object[] otherScripts;
		
		/// <summary>
		/// Is the script attached to anything within any scene or prefab?
		/// </summary>
		public bool IsAttached {
			get { return prefabs.Length != 0 || scenes.Length != 0; }
		}
		
		/// <summary>
		/// Is the script referenced by another script?
		/// </summary>
		public bool IsReferenced {
			get { return otherScripts.Length != 0; }
		}
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
	
	
	void OnGUI ()
	{
		// Show scenes and prefabs that use each script, and other scripts which reference this script
		if (GUILayout.Button ("Show All Scripts")) {
			foreach (MonoScript script in FindAllMonoBehaviourScriptsInProject ()) {
				Debug.Log (script.GetClass ());
			}
			
			// TODO option to show only unused scripts in the list
		}
		
		
		
		ConsoleGUI ();
		
		
//		foreach (var script in unusedMonoScripts) {
//			GUIStyle style = new GUIStyle ("button");
//			style.alignment = TextAnchor.MiddleLeft;
//			GUILayout.BeginHorizontal ();
//			{
//				if (GUILayout.Button (script.name, style)) {
//					EditorGUIUtility.PingObject (script);
//				}
//				if (GUILayout.Button ("Open", GUILayout.Width (50))) {
//					EditorUtility.OpenWithDefaultApp (AssetDatabase.GetAssetPath (script));
//				}
//			}
//			GUILayout.EndHorizontal ();
//		}
	}
	
	
//	private static HashSet<System.Type> FindUnusedMonoBehaviours ()
//	{
//		HashSet<System.Type> existingBehaviours = FindMonoBehaviours ();
//		HashSet<System.Type> usedBehaviours = new HashSet<System.Type> ();
//		
//		// Iterate through all scenes that exist in the project
//		foreach (string s in FindAllScenes ()) {
//			EditorApplication.OpenScene (s);
//			// Find all MonoBehaviours in each scene
//			foreach (var type in existingBehaviours) {
//				// Build a set of MonoBehaviours that are in use
//				if (CurrentSceneContainsMonoBehaviour (type)) {
//					if (!usedBehaviours.Contains (type)) {
//						usedBehaviours.Add (type);
//					}
//				}
//			}
//		}
//		
//		// Remove all used MonoBehaviours from the set of all existing MonoBehaviours
//		existingBehaviours.ExceptWith (usedBehaviours);
//		return existingBehaviours;
//	}
	
	
	
	
	
	
	void ConsoleGUI ()
	{
		// Texture2D icon = EditorGUIUtility.IconContent ("TextAsset Icon").image as Texture2D;
		// EditorGUIUtility.Load("Builtin Skins/Icons/" + name + ".png") as Texture2D
		// EditorGUIUtility.Load("Builtin Skins/Icons/d_" + name + ".png") as Texture2D
		GUILayout.Box (LoadIcon ("TextAsset Icon"));
		GUILayout.Box (LoadIcon ("Prefab Icon"));
		GUILayout.Box (LoadIcon ("Scene Icon"));
		//Builtin Skins/Inspector Images/
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
	
	
	bool UsingProSkin {
		get {
			return GUI.skin.name == "SceneGUISkin";
		}
	}
	
	
	Texture2D GetIconForScriptType (ScriptType scriptType)
	{
		switch (scriptType) {
		case ScriptType.CS:
			return GUILayout.Box (LoadIcon ("cs Script Icon"));
		case ScriptType.JS:
			return GUILayout.Box (LoadIcon ("js Script Icon"));
		case ScriptType.Boo:
			return GUILayout.Box (LoadIcon ("boo Script Icon"));
		}
	}
	
	
	#endregion
}
