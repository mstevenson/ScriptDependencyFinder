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
	
	private static AssetReference[] listedAssets;
	private static bool unusedOnly = false;
	private static bool showSelected = false;
	private Vector2 scrollPos;
	private static GUIStyle referenceStyle;
	private static GUIStyle evenStyle;
	private static GUIStyle oddStyle;
	
	
	#region Window Setup

	private static DependencyFinder window;

	[MenuItem("Window/Dependencies")]
	static void Init ()
	{
		window = (DependencyFinder)EditorWindow.GetWindow (typeof(DependencyFinder), false, "Dependencies");
	}
	
	private static void UpdateStyles ()
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
		if (allAssets.Length == 0) {
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
		
	}
	
	[MenuItem("Assets/Reverse Dependencies/Materials")]
	public static void FindMaterialDependents ()
	{
		
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
	private static MonoScript[] FindAllMonoBehaviourScriptsInProject ()
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
		return scripts.ToArray ();
	}

	private static void ClearList ()
	{
		listedAssets = null;
	}
	
	private void ShowSelected ()
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
	private static AssetReference[] CollectReverseDependencies (AssetReference[] targetAsset, AssetReference[] allAssets)
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
		return scriptRefs.OrderBy (a => Path.GetFileName (a.path)).ToArray ();
	}
	
	private static AssetReference[] FindAssetDependencies (params string[] assetExtensions)
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
		return (from a in foundAssets
				orderby a.asset.name
				select a).ToArray ();
	}
	
	
	/// <summary>
	/// Returns paths for all assets matching a given set of extensions
	/// </summary>
	private static string[] FindAssetsByExtension (params string[] assetExtensions)
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
	
//	private static List<AssetReference> PathsToAssetReferences (string[] paths)
//	{
//		List<AssetReference> refs = new List<AssetReference> ();
//		foreach (string path in paths) {
//			refs.Add (new AssetReference (path));
//		}
//		return refs;
//	}

	
	private static bool PathIncludesExtension (string path, params string[] extensions)
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

	private void ToolbarGUI ()
	{
		GUILayout.BeginHorizontal ("Toolbar");
		{
			if (GUILayout.Button ("Clear", EditorStyles.toolbarButton, GUILayout.Width (35)))
				ClearList ();
			
			ShowAssetTypePopup ();
			
			GUILayout.Space (6);
//			if (GUILayout.Button ("Show Selected", EditorStyles.toolbarButton, GUILayout.Width (75)))
//				ShowSelected ();
			showSelected = GUILayout.Toggle (showSelected, "Show Selected", EditorStyles.toolbarButton, GUILayout.Width (75));
			unusedOnly = GUILayout.Toggle (unusedOnly, "Only unused", EditorStyles.toolbarButton, GUILayout.Width (70));
			EditorGUILayout.Space ();
		}
		GUILayout.EndHorizontal ();
	}

	private void AssetListGUI ()
	{
		if (listedAssets != null) {
			scrollPos = GUILayout.BeginScrollView (scrollPos);
			{
				int currentLine = 0;
				for (int i = 0; i < listedAssets.Length; i++) {
					AssetReference asset = listedAssets [i];
				
					if (unusedOnly && asset.dependencies.Count > 0)
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
			style.Draw (position, content, false, false, false, false);
		}
	}
	
	private void ShowAssetTypePopup ()
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
