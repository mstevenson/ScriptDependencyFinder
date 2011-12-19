using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Linq;


[System.Serializable]
public class ListViewState
{
	public int id;
	public Vector2 scrollPos = Vector2.zero;
	public int row = -1;
	public int totalRows = 0;
	public bool selectionChanged = false;
	public AssetReference[] elements;
}


public class ListElement
{
	public int row = 0;
	public AssetReference scriptRef;
}


public class AssetReference
{
	public Object asset;
	public string path;
	public Texture icon;

	public AssetReference[] dependencies;

	public AssetReference (Object asset)
	{
		this.asset = asset;
		this.path = AssetDatabase.GetAssetPath (asset);
		this.icon = AssetDatabase.GetCachedIcon (path);
	}

	public AssetReference (string path)
	{
		this.path = path;
		this.asset = AssetDatabase.LoadMainAssetAtPath (path);
		this.icon = AssetDatabase.GetCachedIcon (path);
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

	[MenuItem("Window/Script Dependencies")]
	static void Init ()
	{
		ScriptFinder window = (ScriptFinder)EditorWindow.GetWindow (typeof(ScriptFinder), false, "Dependencies");
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
//		scripts.Sort ();
		return scripts;
	}


	#region GUI

	private List<AssetReference> allAssets = new List<AssetReference> ();

	private static bool unusedOnly = false;
	private static bool showSelected = false;
	private Vector2 scrollPos;

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
				Clear ();
			if (GUILayout.Button ("Show All", EditorStyles.toolbarButton, GUILayout.Width (50)))
				ShowAll ();
			GUILayout.Space (6);
			unusedOnly = GUILayout.Toggle (unusedOnly, "Only unused", EditorStyles.toolbarButton, GUILayout.Width (70));
			showSelected = GUILayout.Toggle (showSelected, "Show selected", EditorStyles.toolbarButton, GUILayout.Width (75));
			EditorGUILayout.Space ();
		}
		GUILayout.EndHorizontal ();
	}
	
	
	private void ScriptListGUI ()
	{
		scrollPos = GUILayout.BeginScrollView (scrollPos);
		{
			int currentLine = 0;
			for (int i = 0; i < allAssets.Count; i++) {
				AssetReference asset = allAssets[i];
				
				GUIStyle referenceStyle = new GUIStyle ("label");
				referenceStyle.margin.left = 22;
				
				GUIStyle rowStyle = currentLine % 2 != 0 ? new GUIStyle ("CN EntryBackEven") : new GUIStyle ("CN EntryBackOdd");
				rowStyle.margin = new RectOffset (0, 0, 0, 0);
				rowStyle.padding = new RectOffset (0, 0, 0, 0);
				
				if (unusedOnly && asset.dependencies.Length > 0)
					continue;
				
				GUILayout.BeginVertical (rowStyle);
				{
					if (asset.asset != null) {
						ListButton (asset, new GUIContent (asset.asset.name, asset.icon), "label");
						currentLine++;
						foreach (AssetReference dependency in asset.dependencies) {
							if (dependency.asset != null)
								ListButton (dependency, new GUIContent (dependency.asset.name, dependency.icon), referenceStyle);
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


	private void Clear ()
	{
		allAssets.Clear ();
	}
	
	
	
	private void ShowAll ()
	{
		LoadAssets ();
	}
	
	
	private void LoadAssets ()
	{
		Clear ();
		
		// Grab all asset paths except for folders and items outside of the Assets folder
		string[] assetPaths = (
			from p in AssetDatabase.GetAllAssetPaths ()
			where string.IsNullOrEmpty (Path.GetExtension (p)) == false
			where p.StartsWith ("assets") == true
			select p
			).ToArray ();
		
		bool cancelled = false;
		
		for (int i = 0; i < assetPaths.Length; i++) {
			cancelled = EditorUtility.DisplayCancelableProgressBar (
				"Finding dependencies",
				"Finding dependencies for: " + Path.GetFileName(assetPaths[i]),
				i / (float)assetPaths.Length);
			if (cancelled) {
				EditorUtility.ClearProgressBar ();
				Clear ();
				return;
			}
			
			AssetReference asset = new AssetReference (assetPaths[i]);
			asset.dependencies = (
				from d in EditorUtility.CollectDependencies (new[] { asset.asset })
				where d != asset.asset
				select new AssetReference (d)
				).ToArray ();
			allAssets.Add (asset);
		}
		
		EditorUtility.ClearProgressBar ();
	}

	#endregion


//	public static bool UsingProSkin {
//		get { return GUI.skin.name == "SceneGUISkin"; }
//	}
}
