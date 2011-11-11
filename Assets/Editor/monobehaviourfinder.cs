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
public class MonoBehaviourFinder : EditorWindow
{
	private List<MonoScript> unusedMonoScripts = new List<MonoScript> ();
	
	private static MonoBehaviourFinder window;

	[MenuItem ("Custom/Find Unused Scripts")]
	static void Init ()
	{
		MonoBehaviourFinder window = (MonoBehaviourFinder)EditorWindow.GetWindow (typeof(MonoBehaviourFinder), true, "Unused Scripts");
		
		window.RefreshBehaviours ();
	}
	
	
	void RefreshBehaviours ()
	{
		var unusedBehaviours = FindUnusedMonoBehaviours ();
		foreach (var script in FindAllScriptsInProject ()) {
			if (unusedBehaviours.Contains (script.GetClass ())) {
				unusedMonoScripts.Add (script);
			}
		}
	}
	
	
	void OnGUI ()
	{
		GUILayout.Space (10);
		
		foreach (var script in unusedMonoScripts) {
			GUIStyle style = new GUIStyle ("button");
			style.alignment = TextAnchor.MiddleLeft;
			GUILayout.BeginHorizontal ();
			{
				if (GUILayout.Button (script.name, style)) {
					EditorGUIUtility.PingObject (script);
				}
				if (GUILayout.Button ("Open", GUILayout.Width (50))) {
					EditorUtility.OpenWithDefaultApp (AssetDatabase.GetAssetPath (script));
				}
			}
			GUILayout.EndHorizontal ();
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
