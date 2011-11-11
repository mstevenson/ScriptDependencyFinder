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
	
	// Find MonoBehaviours which are not attached, but are still being called by other code.
	// (they don't need to be monobehaviours in this case)
	
	
	private enum ScriptContainerType {
		Scene,
		Prefab,
		None
	}
	
	private class ScriptReference {
		public MonoScript script;
		public UnityEngine.Object attachedTo;
		public ScriptContainerType containerType;
	}
	
	
	#region Window Setup
	
	private static ScriptFinder window;

	[MenuItem ("Custom/Find Unused Scripts")]
	static void Init ()
	{
		ScriptFinder window = (ScriptFinder)EditorWindow.GetWindow (typeof(ScriptFinder), true, "Script Finder");
	}
	
	#endregion
	
	
	
	
	
	private List<MonoScript> GetScriptDependenciesForAsset (UnityEngine.Object obj)
	{
		return GetScriptDependenciesForAsset (AssetDatabase.GetAssetPath (obj));
	}
	
	
	/// <summary>
	/// Get MonoScripts which are used by the given scene or prefab.
	/// </summary>
	private List<MonoScript> GetScriptDependenciesForAsset (string path)
	{
		List<MonoScript> scripts = new List<MonoScript> ();
		// Note: AssetDatabase.GetDependencies returns path names in all lowercase
		string[] dependencies = AssetDatabase.GetDependencies (new string[] { path });
		foreach (string d in dependencies) {
			if (d == path.ToLower ())
				continue;
			MonoScript s = AssetDatabase.LoadAssetAtPath (d, typeof(MonoScript)) as MonoScript;
			if (s == null)
				continue;
			scripts.Add (s);
		}
		return scripts;
	}
	
	
	private List<UnityEngine.Object> GetAllScenes ()
	{
		return GetAllAssetsOfTypeWithExtension<UnityEngine.Object> (".unity");
	}
	
	private List<UnityEngine.Object> GetAllPrefabs ()
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
	
	
	
	
	
	private bool findInScenes = true;
	private bool findInPrefabs = true;
	void OnGUI ()
	{
		if (GUILayout.Button ("All asset paths")) {
			foreach (var s in AssetDatabase.GetAllAssetPaths ()) {
				Debug.Log (s);
			}
		}
		
		
		if (GUILayout.Button ("Find scene dependencies")) {
			foreach (MonoScript script in GetScriptDependenciesForAsset (Selection.activeObject)) {
				Debug.Log (script.GetClass ());
			}
			
//			foreach (string s in AssetDatabase.GetDependencies (new string[] { p })) {
//				Debug.Log (s);
//			}
		}
		
		
		GUILayout.Space (10);
		
		GUILayout.Label ("Look for scripts in:");
		findInScenes = EditorGUILayout.Toggle ("Scenes", findInScenes);
		findInPrefabs = EditorGUILayout.Toggle ("Prefabs", findInPrefabs);
		
		// FIXME need to ask the user to save current scene before progressing
		
		if (GUILayout.Button ("Find Selected Script")) {
			
		}
		if (GUILayout.Button ("Find All Unused Scripts")) {
			if (findInScenes)
				FindMonoBehavioursInScenes ();
//			if (findInPrefabs)
//				FindMonoBehavioursInPrefabs ();
		}
		
		
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
	
	
	
	private List<MonoScript> unusedMonoScripts = new List<MonoScript> ();
	
	void FindMonoBehavioursInScenes ()
	{
		HashSet<System.Type> unusedBehaviours = FindUnusedMonoBehaviours ();
		foreach (var script in FindAllScriptsInProject ()) {
			if (unusedBehaviours.Contains (script.GetClass ())) {
				unusedMonoScripts.Add (script);
			}
		}
	}
	
	
	private static List<MonoScript> FindAllScriptsInProject ()
	{
		List<MonoScript> scripts = new List<MonoScript> ();
		foreach (var obj in FindObjectsOfTypeIncludingAssets (typeof(MonoScript))) {
			if (obj as MonoScript)
				scripts.Add ((MonoScript)obj);
		}
		return scripts;
	}
	
	
	private static HashSet<System.Type> FindUnusedMonoBehaviours ()
	{
		HashSet<System.Type> existingBehaviours = FindMonoBehaviours ();
		HashSet<System.Type> usedBehaviours = new HashSet<System.Type> ();
		
		// Iterate through all scenes that exist in the project
		foreach (string s in FindAllScenes ()) {
			EditorApplication.OpenScene (s);
			// Find all MonoBehaviours in each scene
			foreach (var type in existingBehaviours) {
				// Build a set of MonoBehaviours that are in use
				if (CurrentSceneContainsMonoBehaviour (type)) {
					if (!usedBehaviours.Contains (type)) {
						usedBehaviours.Add (type);
					}
				}
			}
		}
		
		// Remove all used MonoBehaviours from the set of all existing MonoBehaviours
		existingBehaviours.ExceptWith (usedBehaviours);
		return existingBehaviours;
	}
	
	
	private static string[] FindAllScenes ()
	{
		return Directory.GetFiles (Application.dataPath, "*.unity", SearchOption.AllDirectories);
	}
	
	
	private static bool CurrentSceneContainsMonoBehaviour (System.Type type)
	{
		Object[] objs = FindSceneObjectsOfType (type);
		return objs.Length > 0;
	}
	
	
	private static HashSet<System.Type> FindMonoBehaviours ()
	{
		HashSet<System.Type> subclasses = new HashSet<System.Type> ();
		// Assembly assembly = Assembly.GetExecutingAssembly ();
		Assembly assembly = Assembly.GetAssembly (typeof(AssemblyAnchor));
		foreach (var t in assembly.GetTypes ()) {
			if (t.IsSubclassOf (typeof(MonoBehaviour))) {
				subclasses.Add (t);
			}
		}
		return subclasses;
	}
}
