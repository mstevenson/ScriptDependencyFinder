using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEditor.IMGUI.Controls;

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
	
	public static AssetReference[] ReferencesFromAssets<T> (T[] assets)
		where T : Object
	{
		return assets.Select<T, AssetReference> (a => new AssetReference (a)).ToArray ();
	}
	
	public static AssetReference[] ReferencesFromPaths (string[] paths)
	{
		return paths.Select<string, AssetReference> (p => new AssetReference (p)).ToArray ();
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
	
	// FIXME inherit from SearchableEditorWindow
	
	static AssetReference[] listedAssets;
	static bool unusedOnly = false;
	static bool ignoreStandardAssets = true;
	static bool showSelected = false;
	Vector2 scrollPos;
	static GUIStyle referenceStyle;
	static GUIStyle evenStyle;
	static GUIStyle oddStyle;


	#region Window Setup

	static DependencyFinder window;

	[MenuItem("Window/Dependencies")]
	static void Init ()
	{
		window = (DependencyFinder)EditorWindow.GetWindow (typeof(DependencyFinder), false, "Dependencies");
	}
	
	static void UpdateStyles ()
	{
		referenceStyle = new GUIStyle ("label");
		referenceStyle.margin.left = 22;
		
		evenStyle = new GUIStyle ("CN EntryBackEven");
		evenStyle.margin = new RectOffset (0, 0, 0, 0);
		evenStyle.padding = new RectOffset (0, 0, 0, 0);
		
		oddStyle = new GUIStyle ("CN EntryBackOdd");
		oddStyle.margin = new RectOffset (0, 0, 0, 0);
		oddStyle.padding = new RectOffset (0, 0, 0, 0);
	}

	#endregion
	
	
	#region Menus
	
	[MenuItem("Assets/Reverse Dependencies/Scripts")]
	public static void FindScriptDependents ()
	{
		MonoScript[] scripts = FindAllMonoBehaviourScriptsInProject ();
		AssetReference[] scriptRefs = AssetReference.ReferencesFromAssets<MonoScript> (scripts);
		AssetReference[] allAssets = FindAssetDependencies (".unity", ".prefab", ".asset", ".cs", ".js", ".boo");
		Debug.Log (allAssets.Length);
		if (allAssets == null || allAssets.Length == 0) {
			return;
		}
		listedAssets = CollectReverseDependencies (scriptRefs, allAssets);
		if (window == null)
			Init ();
		else
			window.Repaint ();
	}
	
	[MenuItem("Assets/Reverse Dependencies/Prefabs")]
	public static void FindPrefabDependents ()
	{
		AssetReference[] prefabPaths = AssetReference.ReferencesFromPaths (FindAssetsByExtension (".prefab"));
		AssetReference[] allAssets = FindAssetDependencies ();
		listedAssets = CollectReverseDependencies (prefabPaths, allAssets);
	}
	
	[MenuItem("Assets/Reverse Dependencies/Textures")]
	public static void FindTextureDependents ()
	{
		Debug.LogError ("FindTextureDependents not implemented");
	}
	
	[MenuItem("Assets/Reverse Dependencies/Materials")]
	public static void FindMaterialDependents ()
	{
		Debug.LogError ("FindMaterialDependents not implemented");
	}
	
	#endregion



	/// <summary>
	/// Get MonoScripts which are used in the given scene or prefab.
	/// </summary>
//	List<MonoScript> GetScriptsInScenesOrPrefabs (UnityEngine.Object[] scenesOrPrefabs)
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
	static MonoScript[] FindAllMonoBehaviourScriptsInProject ()
	{
		List<MonoScript> scripts = new List<MonoScript> ();
		foreach (var obj in Resources.FindObjectsOfTypeAll<MonoScript> ()) {
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
		return scripts.ToArray ();
	}

	static void ClearList ()
	{
		listedAssets = null;
	}
	
	void ShowSelected ()
	{
		// TODO cache allAssets unless made dirty by project changes.
		
		AssetReference[] selected = Selection.objects.Select<Object, AssetReference> (o => new AssetReference (o)).ToArray ();
		AssetReference[] allAssets = FindAssetDependencies ();
		if (allAssets.Length == 0) {
			return;
		}
		listedAssets = CollectReverseDependencies (selected, allAssets);
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
	static AssetReference[] CollectReverseDependencies (AssetReference[] targetAsset, AssetReference[] allAssets)
	{
		bool cancelled = false;
		List<AssetReference> scriptRefs = new List<AssetReference> ();
		for (int t = 0; t < targetAsset.Length; t++) {
			
			// Display progress bar when there are many dependencies to collect
			// Progress bar is slow to calculate, so only update it occasionally
			if (t % 5 == 0) {
				cancelled = EditorUtility.DisplayCancelableProgressBar ("Collecting Dependencies", "Cross referencing assets", t / (float)targetAsset.Length);
				if (cancelled) {
					EditorUtility.ClearProgressBar ();
					return null;
				}
			}

			scriptRefs.Add (targetAsset [t]);
			for (int a = 0; a < allAssets.Length; a++) {
				var asset = allAssets [a];
				for (int d = 0; d < asset.dependencies.Count; d++) {
					if (targetAsset [t].path == asset.dependencies [d].path) {
						targetAsset [t].dependencies.Add (asset);
					}
				}
			}
			targetAsset [t].dependencies = targetAsset [t].dependencies
				.OrderBy (a => Path.GetExtension (a.path))
				.ToList ();
		}

		EditorUtility.ClearProgressBar ();

		var result = scriptRefs.OrderBy (a => Path.GetFileName (a.path)).ToArray ();

		// Write unused assets to disk
		var unused = result.Where (a => a.dependencies.Count == 0).OrderBy (a => a.path);
		var sb = new System.Text.StringBuilder ();
		foreach (var asset in unused) {
			sb.AppendLine (asset.path);
		}
		System.IO.File.WriteAllText ("UnusedScripts.txt", sb.ToString ());

		return result;
	}
	
	static AssetReference[] FindAssetDependencies (params string[] assetExtensions)
	{
		string[] assetPaths = FindAssetsByExtension (assetExtensions);
		bool cancelled = false;
		List<AssetReference> foundAssets = new List<AssetReference> ();
		for (int i = 0; i < assetPaths.Length; i++) {
			// Display progress bar when there are many dependencies to collect
			if (assetPaths.Length > 150) {
				// Progress bar is slow to calculate, so only update it occasionally
				if (i % 50 == 0) {
					cancelled = EditorUtility.DisplayCancelableProgressBar ("Collecting Dependencies", "Scanning for asset dependencies", i / (float)assetPaths.Length);
					if (cancelled) {
						EditorUtility.ClearProgressBar ();
						return null;
					}
				}
			}
			
			// Construct dependencies
			AssetReference asset = new AssetReference (assetPaths [i]);
			asset.dependencies = (
				from d in EditorUtility.CollectDependencies (new[] { asset.asset })
				where d != asset.asset
				select new AssetReference (d)
				).ToList ();
			foundAssets.Add (asset);
		}
		EditorUtility.ClearProgressBar ();
		Debug.Log (foundAssets.Count);
		return foundAssets
			.Where (a => a != null && a.asset != null)
			.OrderBy (a => a.asset.name)
			.ToArray ();
	}
	
	
	/// <summary>
	/// Returns paths for all assets matching a given set of extensions
	/// </summary>
	static string[] FindAssetsByExtension (params string[] assetExtensions)
	{
		if (assetExtensions.Length == 0) {
			return AssetDatabase.GetAllAssetPaths ();
		} else {
			// Grab asset paths for assets with the given list of extensions
			return AssetDatabase.GetAllAssetPaths ()
				.Where (p => PathIncludesExtension (p, assetExtensions)
						&& !string.IsNullOrEmpty (Path.GetExtension (p)))
				.ToArray ();
		}
	}
	
//	static List<AssetReference> PathsToAssetReferences (string[] paths)
//	{
//		List<AssetReference> refs = new List<AssetReference> ();
//		foreach (string path in paths) {
//			refs.Add (new AssetReference (path));
//		}
//		return refs;
//	}

	
	static bool PathIncludesExtension (string path, params string[] extensions)
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
		UpdateStyles ();
		
		ToolbarGUI ();
		AssetListGUI ();
	}

	void ToolbarGUI ()
	{
		GUILayout.BeginHorizontal ("Toolbar");
		{
			if (GUILayout.Button ("Clear", EditorStyles.toolbarButton, GUILayout.Width (40)))
				ClearList ();
			
			ShowAssetTypePopup ();
			
			GUILayout.Space (12);
//			if (GUILayout.Button ("Show Selected", EditorStyles.toolbarButton, GUILayout.Width (75)))
//				ShowSelected ();
			showSelected = GUILayout.Toggle (showSelected, "Show Selected", EditorStyles.toolbarButton, GUILayout.Width (90));
			unusedOnly = GUILayout.Toggle (unusedOnly, "Only unused", EditorStyles.toolbarButton, GUILayout.Width (80));
			ignoreStandardAssets = GUILayout.Toggle (ignoreStandardAssets, "No Standard Assets", EditorStyles.toolbarButton, GUILayout.Width (115));
			EditorGUILayout.Space ();
		}
		GUILayout.EndHorizontal ();
	}

	void AssetListGUI ()
	{
		if (listedAssets != null) {
			scrollPos = GUILayout.BeginScrollView (scrollPos);
			{
				int currentLine = 0;
				for (int i = 0; i < listedAssets.Length; i++) {
					AssetReference asset = listedAssets [i];
				
					if (unusedOnly && asset.dependencies.Count > 0)
						continue;
					if (ignoreStandardAssets && asset.path.StartsWith ("Assets/Standard Assets/"))
						continue;
				
						GUILayout.BeginVertical (currentLine % 2 != 0 ? evenStyle : oddStyle);
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
	}



	/// <summary>
	/// Generic button UI element for items which can be selected, clicked, and double clicked
	/// </summary>
	void ListButton (AssetReference asset, GUIContent content, GUIStyle style)
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
						if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo ()) {
							EditorSceneManager.OpenScene (asset.path);
							GUIUtility.ExitGUI ();
						}
						// Open prefab
					} else if (extension == ".prefab") {
						if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo ()) {
							EditorSceneManager.NewScene (NewSceneSetup.EmptyScene);
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
			style.Draw (position, content, false, false, false, false);
		}
	}
	
	void ShowAssetTypePopup ()
	{
		GUIContent content = new GUIContent ("Show");
		Rect rect = GUILayoutUtility.GetRect (content, EditorStyles.toolbarDropDown, GUILayout.Width (45));
		GUI.Label (rect, content, EditorStyles.toolbarDropDown);
		if (Event.current.type != EventType.MouseDown || !rect.Contains (Event.current.mousePosition))
			return;
		GUIUtility.hotControl = 0;
		EditorUtility.DisplayPopupMenu (rect, "Assets/Reverse Dependencies", null);
		Event.current.Use ();
	}
	
	
//	public static bool UsingProSkin {
//		get { return GUI.skin.name == "SceneGUISkin"; }
//	}

	#endregion

}
