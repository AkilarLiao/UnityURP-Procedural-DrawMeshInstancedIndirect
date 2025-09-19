/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/19
/// Desc:
/// </summary>

using UnityEngine;
using UnityEditor;

namespace SB.ProceduralGrass
{
	public class ShowObjectGUI
	{
		protected void DestorySelfEditor()
		{
			if (m_selfEditor)
			{
				if (Application.isEditor)
					Object.DestroyImmediate(m_selfEditor);
				else
					Object.Destroy(m_selfEditor);
				m_selfEditor = null;
			}
		}

		public virtual void UpdateObjectGUI(Object targetObject, bool isDrawHead = true)
		{   
			if (m_targetObject != targetObject)
				DestorySelfEditor();

			if (!targetObject)
				return;

			m_targetObject = targetObject;

			if(!m_selfEditor)
				m_selfEditor = Editor.CreateEditor(targetObject);
			Debug.Assert(m_selfEditor);

			OupdateGUI(isDrawHead);
		}
		protected virtual void OupdateGUI(bool isDrawHead)
		{
			if (isDrawHead)
			{
				m_selfEditor.DrawHeader();
				EditorGUI.indentLevel++;
			}
			m_selfEditor.OnInspectorGUI();
		}

		protected Editor m_selfEditor = null;
		protected Object m_targetObject = null;
	}
}