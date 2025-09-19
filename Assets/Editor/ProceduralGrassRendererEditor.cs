/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/19
/// Desc:
/// </summary>

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SB.ProceduralGrass
{
    [CustomEditor(typeof(ProceduralGrassRenderer), true)]
    public class ProceduralGrassRendererEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            m_showObjectGUI.UpdateObjectGUI(((ProceduralGrassRenderer)target).m_proceduralGrassData);
        }

        private ShowObjectGUI m_showObjectGUI = new ShowObjectGUI();
    }
}
