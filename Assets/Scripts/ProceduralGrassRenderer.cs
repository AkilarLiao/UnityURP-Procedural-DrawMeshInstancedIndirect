/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/19
/// Desc:
/// </summary>

using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace SB.ProceduralGrass
{
    [ExecuteAlways]
    public class ProceduralGrassRenderer : MonoBehaviour
    {
        private void OnEnable()
        {
            ReInitialize();
#if UNITY_EDITOR
            if (m_proceduralGrassData)
            {
                m_proceduralGrassData.OnProceduralGrassDataChange -= OnProceduralGrassDataChangeCB;
                m_proceduralGrassData.OnProceduralGrassDataChange += OnProceduralGrassDataChangeCB;
            }
#endif //UNITY_EDITOR
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
#if UNITY_EDITOR
            if (m_proceduralGrassData)
                m_proceduralGrassData.OnProceduralGrassDataChange -= OnProceduralGrassDataChangeCB;
#endif //UNITY_EDITOR
            Release();
        }

        private void ReInitialize()
        {
            Release();

            if (!m_proceduralGrassData)
                return;
            
            var internalResource = m_proceduralGrassData.m_internalResource;
            if (internalResource == null)
                return;

            if (internalResource.m_grassShader)
                m_grassMaterial = CoreUtils.CreateEngineMaterial(internalResource.m_grassShader);

            m_targetProceduralInstanceFilterCS = internalResource.m_proceduralInstanceFilterCS;

            if (m_visibleInstancesTransformBuffer != null)
            {
                m_visibleInstancesTransformBuffer.Release();
                m_visibleInstancesTransformBuffer.Dispose();
            }            

            m_visibleInstancesTransformBuffer = new ComputeBuffer((int)mc_maxVisibleInstanceCount,
                Marshal.SizeOf(typeof(Vector3)), ComputeBufferType.Append);

            m_targetProceduralInstanceFilterCS.SetBuffer(0, ProceduralInstanceFilterID.msr_visibleInstancesTransformBuffer,
                m_visibleInstancesTransformBuffer);            

            m_indirectArgumentsBuffer = new ComputeBuffer(1, ms_tempIndirectArguments.Length * sizeof(uint),
                ComputeBufferType.IndirectArguments);

            var grassMesh = GetGrassConeMeshCache();
            int subMeshIndex = Mathf.Clamp(0, 0, grassMesh.subMeshCount - 1);

            ms_tempIndirectArguments[0] = grassMesh.GetIndexCount(
                subMeshIndex);
            ms_tempIndirectArguments[1] = 0;
            ms_tempIndirectArguments[2] = grassMesh.GetIndexStart(
                subMeshIndex);
            ms_tempIndirectArguments[3] = grassMesh.GetBaseVertex(
                subMeshIndex);
            ms_tempIndirectArguments[4] = 0;
            m_indirectArgumentsBuffer.SetData(ms_tempIndirectArguments);

            RefreshParams();
        }

        private void Release()
        {
            if (m_grassMaterial)
            {
                CoreUtils.Destroy(m_grassMaterial);
                m_grassMaterial = null;
            }

            if (m_cachedGrassMesh)
            {
                CoreUtils.Destroy(m_cachedGrassMesh);
                m_cachedGrassMesh = null;
            }

            if (m_visibleCellIndexBuffer != null)
            {
                m_visibleCellIndexBuffer.Release();
                m_visibleCellIndexBuffer.Dispose();
                m_visibleCellIndexBuffer = null;
            }

            if (m_visibleInstancesTransformBuffer != null)
            {
                m_visibleInstancesTransformBuffer.Release();
                m_visibleInstancesTransformBuffer.Dispose();
                m_visibleInstancesTransformBuffer = null;
            }

            if (m_indirectArgumentsBuffer != null)
            {
                m_indirectArgumentsBuffer.Release();
                m_indirectArgumentsBuffer.Dispose();
                m_indirectArgumentsBuffer = null;
            }
        }

        //private void LateUpdate()
        //{
        //    Graphics.DrawMesh(GetGrassConeMeshCache(), Matrix4x4.identity, m_grassMaterial, 0);
        //}

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            var cameraType = camera.cameraType;
            if ((cameraType != CameraType.Game) &&
                (cameraType != CameraType.SceneView))
            {
                if (cameraType == CameraType.Reflection)
                    return;
                //Hard code
                //When cameraType == CameraType.Preview, and
                //camera.clearFlags == CameraClearFlags.Depth
                //It is represented as MaterialPreviewCamera
                else if ((cameraType == CameraType.Preview) &&
                    (camera.clearFlags == CameraClearFlags.Depth))
                    return;
            }

            m_visableCellsCuller.ProcessCulling(camera, in m_cellSize, in m_proceduralGrassData.m_worldRect,
                in msr_worldMinMaxHeight);

            var visibleCellIndices = m_visableCellsCuller.GetVisibleCellIndices();
            var visibleCellIndexCount = visibleCellIndices.Count;
            if (visibleCellIndexCount <= 0)
                return;

            visibleCellIndices.CopyTo(m_visibleCellIndices);
            m_visibleCellIndexBuffer.SetData(m_visibleCellIndices, 0, 0, visibleCellIndexCount);

            m_targetProceduralInstanceFilterCS.SetMatrix(ProceduralInstanceFilterID.msr_viewProjectionMatrix,
                camera.projectionMatrix * camera.worldToCameraMatrix);

            m_visibleInstancesTransformBuffer.SetCounterValue(0);

            ref uint2 cellInstanceColumnRowCount = ref m_proceduralGrassData.m_cellInstanceColumnRowCount;
            var processInstanceCount = cellInstanceColumnRowCount.x * cellInstanceColumnRowCount.y * visibleCellIndexCount;

            DispatchComputeInBatches(m_targetProceduralInstanceFilterCS, (int)processInstanceCount);

            ComputeBuffer.CopyCount(m_visibleInstancesTransformBuffer, m_indirectArgumentsBuffer, 4);

            Graphics.DrawMeshInstancedIndirect(GetGrassConeMeshCache(), 0, m_grassMaterial, m_renderBound, m_indirectArgumentsBuffer,
                0, null, ShadowCastingMode.Off, true, 0, camera);
        }
        private void OnProceduralGrassDataChangeCB()
        {
            RefreshParams();
        }

        private void RefreshParams()
        {
            if ((!m_proceduralGrassData) || (!m_targetProceduralInstanceFilterCS) || (!m_grassMaterial))
                return;

            ref Rect worldRect = ref m_proceduralGrassData.m_worldRect;

            var min = worldRect.min;
            var max = worldRect.max;
            m_renderBound = new Bounds();
            m_renderBound.SetMinMax(
                new Vector3(min.x, msr_worldMinMaxHeight.x, min.y),
                new Vector3(max.x, msr_worldMinMaxHeight.y, max.y));

            //referesh shared property
            Shader.SetGlobalVector(msr_worldMinMaxID, new float4(worldRect.min, worldRect.max));

            //referesh ComputeShader property
            ref uint2 cellColumnRowCount = ref m_proceduralGrassData.m_cellColumnRowCount;

            var cellCount = cellColumnRowCount.x * cellColumnRowCount.y;
            if (cellCount <= 0)
                return;

            if ((m_visibleCellIndexBuffer == null) || (m_currentCellCount != cellCount))
            {
                m_visibleCellIndices = new uint[cellCount];
                if (m_visibleCellIndexBuffer != null)
                {
                    m_visibleCellIndexBuffer.Release();
                    m_visibleCellIndexBuffer.Dispose();
                    m_visibleCellIndexBuffer = null;
                }
                m_visibleCellIndexBuffer = new ComputeBuffer(m_visibleCellIndices.Length, Marshal.SizeOf(typeof(uint)));
                m_currentCellCount = cellCount;
                m_targetProceduralInstanceFilterCS.SetBuffer(0, ProceduralInstanceFilterID.msr_visibleCellIndexBuffer,
                    m_visibleCellIndexBuffer);
            }

            m_cellSize = new Vector2(worldRect.width / cellColumnRowCount.x,
                worldRect.height / cellColumnRowCount.y);

            m_targetProceduralInstanceFilterCS.SetVector(ProceduralInstanceFilterID.msr_cellSize, m_cellSize);

            m_targetProceduralInstanceFilterCS.SetInt(ProceduralInstanceFilterID.msr_cellColumnCount,
                (int)cellColumnRowCount.x);

            ref uint2 cellInstanceColumnRowCount = ref m_proceduralGrassData.m_cellInstanceColumnRowCount;

            m_targetProceduralInstanceFilterCS.SetInt(ProceduralInstanceFilterID.msr_cellInstanceColumnCount,
                (int)cellInstanceColumnRowCount.x);

            m_targetProceduralInstanceFilterCS.SetInt(ProceduralInstanceFilterID.msr_cellInstanceCount,
                (int)(cellInstanceColumnRowCount.x * cellInstanceColumnRowCount.y));
        }

        private Mesh GetGrassConeMeshCache()
        {
            if (m_cachedGrassMesh)
                return m_cachedGrassMesh;

            m_cachedGrassMesh = new Mesh();

            // Vertices: three at the base + one at the top
            Vector3[] verts = new Vector3[4];
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


        /// <summary>
        /// Dispatches a ComputeShader in batches, supporting more than 65535 thread groups.
        /// </summary>
        /// <param name="targetComputeShader">The target ComputeShader.</param>
        /// <param name="processInstanceCount">Total number of instances to process.</param>
        /// <param name="kernel">Index of the kernel to execute (default: 0).</param>
        /// <param name="threadGroupSize">Number of instances per thread group (default: 64).</param>
        /// <param name="maxDispatchCount">Maximum number of thread groups allowed per dispatch (default: 65535).</param>
        private static void DispatchComputeInBatches(ComputeShader targetComputeShader, int processInstanceCount,
            int kernel = 0, int threadGroupSize = 64, int maxDispatchCount = 65535)
        {
            // Set the total number of instances for the shader to access (for bounds checking)
            targetComputeShader.SetInt(ProceduralInstanceFilterID.msr_maxProcessCount, processInstanceCount);

            // 'offset' tracks how many instances have already been processed
            int offset = 0;
            while (offset < processInstanceCount)
            {
                // Calculate how many instances remain to be processed in this batch
                int remainInstance = processInstanceCount - offset;

                // Dynamically calculate how many thread groups are needed for this batch
                // This is crucial: the last batch may not fill a whole thread group,
                // so we must recalculate based on the remaining instances.
                int groupCountThisBatch = Mathf.CeilToInt(remainInstance / (float)threadGroupSize);

                // Clamp the number of thread groups to the API limit (65535)
                int dispatchThisBatch = Mathf.Min(groupCountThisBatch, maxDispatchCount);

                // Set the offset for this batch so the shader knows the starting index
                targetComputeShader.SetInt(ProceduralInstanceFilterID.msr_offestCount, offset);

                // Dispatch the current batch
                targetComputeShader.Dispatch(kernel, dispatchThisBatch, 1, 1);

                // Update offset to mark processed instances
                offset += dispatchThisBatch * threadGroupSize;
            }
        }



        public ProceduralGrassData m_proceduralGrassData = null;
        
        private ComputeShader m_targetProceduralInstanceFilterCS = null;

        private Mesh m_cachedGrassMesh = null;
        private Material m_grassMaterial = null;
        private Bounds m_renderBound;

        private uint[] m_visibleCellIndices = null;
        private ComputeBuffer m_visibleCellIndexBuffer = null;
        private Vector2 m_cellSize = Vector2.one;
        
        private ComputeBuffer m_visibleInstancesTransformBuffer;

        private VisableCellsCuller m_visableCellsCuller = new VisableCellsCuller();

        private uint m_currentCellCount = 0;
        private ComputeBuffer m_indirectArgumentsBuffer;

        private static uint[] ms_tempIndirectArguments = new uint[5] { 0, 0, 0, 0, 0 };

        private static readonly Vector2 msr_worldMinMaxHeight = new Vector2(0.0f, 10.0f);

        private static readonly int msr_worldMinMaxID = Shader.PropertyToID("_WorldMinMax");
        private static class ProceduralInstanceFilterID
        {
            public static readonly int msr_offestCount = Shader.PropertyToID("_OffsetCount");
            public static readonly int msr_maxProcessCount = Shader.PropertyToID("_MaxProcessCount");
            public static readonly int msr_visibleCellIndexBuffer = Shader.PropertyToID("_VisibleCellIndexBuffer");
            public static readonly int msr_cellSize = Shader.PropertyToID("_CellSize");
            public static readonly int msr_cellColumnCount = Shader.PropertyToID("_CellColumnCount");
            public static readonly int msr_cellInstanceColumnCount = Shader.PropertyToID("_CellInstanceColumnCount");
            public static readonly int msr_cellInstanceCount = Shader.PropertyToID("_CellInstanceCount");
            public static readonly int msr_viewProjectionMatrix = Shader.PropertyToID("_ViewProjectionMatrix");
            public static readonly int msr_visibleInstancesTransformBuffer = Shader.PropertyToID("_VisibleInstancesTransformBuffer");           
        }

        private const float mc_grassMeshWidth = 0.25f;

        private const uint mc_maxVisibleInstanceCount = 500000;
    }
}
