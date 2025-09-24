/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/24
/// Desc:
/// </summary>

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SB.ProceduralGrass
{
    [ExecuteAlways]
    public class GrassInteractor : MonoBehaviour
    {
        private void OnEnable()
        {
            m_selfTransform = transform;
        }
        private void LateUpdate()
        {
            if (!m_targetGrassRenderer)
                return;
            m_targetGrassRenderer.UpdateMainInteractorTransform(m_selfTransform.position,
                m_selfTransform.lossyScale.x);
        }
        [SerializeField]
        private ProceduralGrassRenderer m_targetGrassRenderer = null;

        private Transform m_selfTransform = null;
    }
}
