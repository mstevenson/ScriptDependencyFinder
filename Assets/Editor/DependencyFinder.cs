using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Linq;


public class AssetReference
{
	public Object asset;
	public string path;

	public List<AssetReference> dependencies = new List<AssetReference> ();

	public AssetReference (Object asset)
	{
		this.asset = asset;
		this.path = AssetDatabase.GetAssetPath (asset);
	}

	public AssetReference (string path)
	{
		this.path = path;
		this.asset = AssetDatabase.LoadMainAssetAtPath (path);
	}
}




/// <summary>
/// Finds MonoBehaviour that are not attached to any objects in any scene.
/// </summary>
/// <remarks>
/// This tool does not take into account prefabs or components that are instantiated at runtime. Use with caution.
/// </remarks>
public sealed class DependencyFinder : EditorWindow
{

	#region Window Setup

	private static DependencyFinder window;

	[MenuItem("Window/Dependencies")]
	static void Init ()
	{
		DependencyFinder window = (DependencyFinder)EditorWindow.GetWindow (typeof(DependencyFinder), false, "Dependencies");
	}

	#endregion


	/// <summary>
	/// Get MonoScripts which are used in the given scene or prefab.
	/// </summary>
//	private List<MonoScript> GetScriptsInScenesOrPrefabs (UnityEngine.Object[] scenesOrPrefabs)
//	{
//		List<MonoScript> scripts = new List<MonoScript> ();
//		foreach (var s in EditorUtility.CollectDependencies (scenesOrPrefabs)) {
//			if (s as MonoScript) {
//				scripts.Add ((MonoScript)s);
//			}
//		}
//		return scripts;
//	}


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
	
	
	private List<AssetReference> listedAssets = new List<AssetReference> ();
	
	
	private static bool unusedOnly = false;
	private Vector2 scrollPos;


	private void ClearList ()
	{
		listedAssets.Clear ();
	}
		
	
	private void ShowScripts ()
	{
		ClearList ();
		List<MonoScript> scripts = FindAllMonoBehaviourScriptsInProject ();
		List<AssetReference> scriptRefs = scripts.Select<MonoScript, AssetReference> (s => new AssetReference (s)).ToList ();
		List<AssetReference> allAssets = FindAssetDependencies (".unity", ".prefab", ".asset", ".cs", ".js", ".boo");
		if (allAssets.Count == 0) {
			return;
		}
		listedAssets = FindReverseDependencies (scriptRefs, allAssets);
	}
	
	private void ShowSelected ()
	{
		// TODO cache allAssets unless made dirty by project changes.
		
		List<AssetReference> selected = Selection.objects.Select<Object, AssetReference> (o => new AssetReference (o)).ToList ();
		List<AssetReference> allAssets = FindAssetDependencies ();
		if (allAssets.Count == 0) {
			return;
		}
		listedAssets = FindReverseDependencies (selected, allAssets);
	}
	
	
	
	/// <summary>
	/// Searches all assets for a dependency matching the target asset. 
	/// </summary>
	/// <param name="targetAsset">
	/// The asset that is depended upon.
	/// </param>
	/// <param name="allAssets">
	/// The pool of assets in which to search for targetAsset dependencies.
	/// </param>
	/// <returns>
	/// Returns list of target asset references which includes a list of other assets that depend upon them.
	/// </returns>
	private List<AssetReference> FindReverseDependencies (List<AssetReference> targetAsset, List<AssetReference> allAssets)
	{
		List<AssetReference> scriptRefs = new List<AssetReference> ();
		foreach (var currentScript in targetAsset) {
			scriptRefs.Add (currentScript);
			foreach (var asset in allAssets) {
				foreach (var dependency in asset.dependencies) {
					if (currentScript.path == dependency.path) {
						currentScript.dependencies.Add (asset);
					}
				}
			}
			currentScript.dependencies = (
				from a in currentScript.dependencies
				orderby Path.GetExtension (a.path)
				select a
				).ToList ();
		}
		return scriptRefs;
	}
	
	
	private List<AssetReference> FindAssetDependencies (params string[] assetExtensions)
	{
		string[] assetPaths;
		if (assetExtensions.Length == 0) {
			assetPaths = AssetDatabase.GetAllAssetPaths ();
		}
		else {
			// Grab asset paths for assets with the given list of extensions
			assetPaths = AssetDatabase.GetAllAssetPaths ()
				.Where (p => HasExtension (p, assetExtensions)
						&& !string.IsNullOrEmpty (Path.GetExtension (p))
						&& p.StartsWith ("assets"))
				.ToArray ();
		}
		bool cancelled = false;
		List<AssetReference> foundAssets = new List<AssetReference> ();
		
		for (int i = 0; i < assetPaths.Length; i++) {
			// Progress Bar
			cancelled = EditorUtility.DisplayCancelableProgressBar (
				"Finding dependencies",
				"Finding dependencies for: " + Path.GetFileName (assetPaths[i]),
				i / (float)assetPaths.Length);
			if (cancelled) {
				EditorUtility.ClearProgressBar ();
				return new List<AssetReference> ();
			}
			
			// Construct dependencies
			AssetReference asset = new AssetReference (assetPaths[i]);
			asset.dependencies = (
				from d in EditorUtility.CollectDependencies (new[] { asset.asset })
				where d != asset.asset
				select new AssetReference (d)
				).ToList ();
			foundAssets.Add (asset);
		}
		EditorUtility.ClearProgressBar ();
		return (from a in foundAssets
				orderby a.asset.name
				select a).ToList ();
	}

	
	private bool HasExtension (string path, params string[] extensions)
	{
		string ext = Path.GetExtension (path);
		foreach (string e in extensions) {
			if (ext == e)
				return true;
		}
		return false;
	}
	
	
	
	#region GUI
	
	void OnGUI ()
	{
		ToolbarGUI ();
		ScriptListGUI ();
	}


	private void ToolbarGUI ()
	{
		GUILayout.BeginHorizontal ("Toolbar");
		{
			if (GUILayout.Button ("Clear", EditorStyles.toolbarButton, GUILayout.Width (35)))
				ClearList ();
			if (GUILayout.Button ("Show Scripts", EditorStyles.toolbarButton, GUILayout.Width (70)))
				ShowScripts ();
			if (GUILayout.Button ("Show Selected", EditorStyles.toolbarButton, GUILayout.Width (75)))
				ShowSelected ();
			GUILayout.Space (6);
			unusedOnly = GUILayout.Toggle (unusedOnly, "Only unused", EditorStyles.toolbarButton, GUILayout.Width (70));
			EditorGUILayout.Space ();
		}
		GUILayout.EndHorizontal ();
	}


	private void ScriptListGUI ()
	{
		scrollPos = GUILayout.BeginScrollView (scrollPos);
		{
			int currentLine = 0;
			for (int i = 0; i < listedAssets.Count; i++) {
				AssetReference asset = listedAssets[i];
				
				GUIStyle referenceStyle = new GUIStyle ("label");
				referenceStyle.margin.left = 22;
				
				GUIStyle rowStyle = currentLine % 2 != 0 ? new GUIStyle ("CN EntryBackEven") : new GUIStyle ("CN EntryBackOdd");
				rowStyle.margin = new RectOffset (0, 0, 0, 0);
				rowStyle.padding = new RectOffset (0, 0, 0, 0);
				
				if (unusedOnly && asset.dependencies.Count > 0)
					continue;
				
				GUILayout.BeginVertical (rowStyle);
				{
					if (asset.asset != null) {
						ListButton (asset, new GUIContent (asset.asset.name, AssetDatabase.GetCachedIcon (asset.path)), "label");
						currentLine++;
						foreach (AssetReference dependency in asset.dependencies) {
							if (dependency.asset != null)
								ListButton (dependency, new GUIContent (dependency.asset.name, AssetDatabase.GetCachedIcon (dependency.path)), referenceStyle);
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
	private void ListButton (AssetReference asset, GUIContent content, GUIStyle style)
	{
		Rect position = GUILayoutUtility.GetRect (content, style);
		//		int controlId = GUIUtility.GetControlID (FocusType.Native);
		
		// Click line element
		if (Event.current.type == EventType.MouseDown && position.Contains (Event.current.mousePosition)) {
			if (Event.current.button == 0) {
				// Show in project pane
				EditorGUIUtility.PingObject (asset.asset);
				// Open the file
				if (Event.current.clickCount == 2) {
					string extension = Path.GetExtension (asset.path);
					// Open scene
					if (extension == ".unity") {
						if (EditorApplication.SaveCurrentSceneIfUserWantsTo ()) {
							EditorApplication.OpenScene (asset.path);
							GUIUtility.ExitGUI ();
						}
						// Open prefab
					} else if (extension == ".prefab") {
						if (EditorApplication.SaveCurrentSceneIfUserWantsTo ()) {
							EditorApplication.NewScene ();
							GameObject.Instantiate (asset.asset);
							GUIUtility.ExitGUI ();
						}
						// Open script
					} else {
						AssetDatabase.OpenAsset (asset.asset);
					}
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
	
	
//	public static bool UsingProSkin {
//		get { return GUI.skin.name == "SceneGUISkin"; }
//	}

	#endregion
}
