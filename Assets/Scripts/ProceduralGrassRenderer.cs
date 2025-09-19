/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/19
/// Desc:
/// </summary>
/// 
using UnityEngine;
using UnityEngine.Rendering;

namespace SB.ProceduralGrass
{
    [ExecuteAlways]
    public class ProceduralGrassRenderer : MonoBehaviour
    {
        private void OnEnable()
        {
            m_grassMaterial = CoreUtils.CreateEngineMaterial(m_grassShader);
        }

        private void OnDisable()
        {
            CoreUtils.Destroy(m_grassMaterial);            
            m_grassMaterial = null;
            CoreUtils.Destroy(m_cachedGrassMesh);
            m_cachedGrassMesh = null;
        }

        private void LateUpdate()
        {
            Graphics.DrawMesh(GetGrassConeMeshCache(), Matrix4x4.identity, m_grassMaterial, 0);
        }

        private Mesh GetGrassConeMeshCache()
        {
//            bool isForceRecreate = false;
//#if UNITY_EDITOR
//            isForceRecreate = m_currentGrassMeshWidth != m_grassMeshWidth;
//            m_currentGrassMeshWidth = m_grassMeshWidth;
//#endif//UNITY_EDITOR

            if (m_cachedGrassMesh/* && (!isForceRecreate)*/)
                return m_cachedGrassMesh;

            //if (m_cachedGrassMesh)
                //CoreUtils.Destroy(m_cachedGrassMesh);

            m_cachedGrassMesh = new Mesh();
            //m_cachedGrassMesh.name = $"GrassPrismMesh_{m_grassMeshWidth:F2}";

            // Vertices: three at the base + one at the top
            Vector3[] verts = new Vector3[4];
            //verts[0] = new Vector3(-m_grassMeshWidth, 0, -m_grassMeshWidth);    // Base vertex 1
            //verts[1] = new Vector3(m_grassMeshWidth, 0, -m_grassMeshWidth);     // Base vertex 2
            //verts[2] = new Vector3(0, 0, m_grassMeshWidth);                     // Base vertex 3
            verts[0] = new Vector3(-mc_grassMeshWidth, 0, -mc_grassMeshWidth);    // Base vertex 1
            verts[1] = new Vector3(mc_grassMeshWidth, 0, -mc_grassMeshWidth);     // Base vertex 2
            verts[2] = new Vector3(0, 0, mc_grassMeshWidth);                     // Base vertex 3

            verts[3] = new Vector3(0, 1, 0);                                    // Top vertex

            // Triangles: one base + three sides
            int[] triangles = new int[]
            {
                0, 1, 2, // Base
                0, 3, 1, // Side 1
                1, 3, 2, // Side 2
                2, 3, 0  // Side 3
            };

            m_cachedGrassMesh.SetVertices(verts);
            m_cachedGrassMesh.SetTriangles(triangles, 0);
            
            return m_cachedGrassMesh;
        }
        [SerializeField]
        private Shader m_grassShader = null;

        //[SerializeField]
        //[Range(0.05f, 1.0f)]
        //private float m_grassMeshWidth = 0.25f;

        private Mesh m_cachedGrassMesh = null;
        private Material m_grassMaterial = null;

//#if UNITY_EDITOR
        //private float m_currentGrassMeshWidth = 0.0f;
//#endif//UNITY_EDITOR
        private const float mc_grassMeshWidth = 0.25f;
    }
}
