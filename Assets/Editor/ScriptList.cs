using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;

public class ScriptListElement
{
	public int row = 0;
	public ScriptReference scriptRef;
}


public class ScriptList {
	/// <summary>
	/// Show only unused scripts.
	/// </summary>
	public static bool unusedOnly = false;
	public Vector2 scrollPos;
	
	public List<ScriptListElement> elements = new List<ScriptListElement> ();
}
