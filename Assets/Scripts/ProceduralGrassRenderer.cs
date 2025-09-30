/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/19
/// Desc:
/// </summary>

//#define VIEW_VISIBLE_INSTANCE_COUNT
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace SB.ProceduralGrass
{
    [ExecuteAlways]
    public class ProceduralGrassRenderer : MonoBehaviour
    {
        public void UpdateMainInteractorTransform(in Vector3 position, float radius)
        {
            if (!m_grassMaterial)
                return;
            m_grassMaterial.SetVector(msr_interactorCollisionSphereID,
                new float4(position, radius));
        }
        private void OnEnable()
        {
            ReInitialize();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.update -= EditUpdate;
                UnityEditor.EditorApplication.update += EditUpdate;
            }
#endif //UNITY_EDITOR
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;

            if (m_targetDisplayFPS)
            {
                m_targetDisplayFPS.AppendExtendString -= AppendExtendStringCB;
                m_targetDisplayFPS.AppendExtendString += AppendExtendStringCB;
            }
        }

        private void OnDisable()
        {
            if (m_targetDisplayFPS)
                m_targetDisplayFPS.AppendExtendString -= AppendExtendStringCB;

            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.update -= EditUpdate;
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

            m_instanceCountBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Counter);
            m_instanceCountBuffer.SetCounterValue(0);
            m_targetProceduralInstanceFilterCS.SetBuffer(0, msr_instanceCountBufferID,
                m_instanceCountBuffer);

            m_indirectArgumentsBuffer = new ComputeBuffer(1, ms_tempIndirectArguments.Length * sizeof(uint),
                ComputeBufferType.IndirectArguments);

            m_filterResultRT = new RenderTexture(m_filterResultSize.x,
                m_filterResultSize.y, 0, RenderTextureFormat.ARGBHalf);
            m_filterResultRT.enableRandomWrite = true;
            m_filterResultRT.filterMode = FilterMode.Point;
            m_filterResultRT.Create();

            m_targetProceduralInstanceFilterCS.SetVector(msr_filterResultRTSizeID,
                new Vector2(m_filterResultSize.x, m_filterResultSize.y));

            m_grassParamsSector.ReInitialize(m_targetProceduralInstanceFilterCS, m_grassMaterial,
                m_proceduralGrassData, m_filterResultRT);

            RefereshVisibleCellIndexBuffer();
            RefereshProcessWeightMapFilterKeyword();
            m_grassParamsSector.RefreshParams();
        }

        private void Awake()
        {
            m_executeTimeProcessor.Start();
        }

        private void Start()
        {
            m_executeTime = m_executeTimeProcessor.StopGetMS();
        }

        private void Release()
        {
            m_grassParamsSector.Release();

            if (m_grassMaterial)
            {
                CoreUtils.Destroy(m_grassMaterial);
                m_grassMaterial = null;
            }

            if (m_pyramidMesh)
            {
                CoreUtils.Destroy(m_pyramidMesh);
                m_pyramidMesh = null;
            }

            if (m_triangleMesh)
            {
                CoreUtils.Destroy(m_triangleMesh);
                m_triangleMesh = null;
            }

            if (m_visibleCellIndexBuffer != null)
            {
                m_visibleCellIndexBuffer.Release();
                m_visibleCellIndexBuffer.Dispose();
                m_visibleCellIndexBuffer = null;
            }

            if (m_filterResultRT)
            {
                m_filterResultRT.Release();
                CoreUtils.Destroy(m_filterResultRT);
                m_filterResultRT = null;
            }

            if (m_instanceCountBuffer != null)
            {
                m_instanceCountBuffer.Release();
                m_instanceCountBuffer.Dispose();
                m_instanceCountBuffer = null;
            }

            if (m_indirectArgumentsBuffer != null)
            {
                m_indirectArgumentsBuffer.Release();
                m_indirectArgumentsBuffer.Dispose();
                m_indirectArgumentsBuffer = null;
            }
        }

        private void LateUpdate()
        {
            m_targetProceduralInstanceFilterCS.SetFloat(msr_currentTimeID,
                Time.time);
#if UNITY_EDITOR
            RefereshVisibleCellIndexBuffer();
            RefereshProcessWeightMapFilterKeyword();
            //RefreshParams();
            m_grassParamsSector.RefreshParams();
#endif //UNITY_EDITOR
        }

        private bool IsSkipCamera(Camera camera)
        {
            var cameraType = camera.cameraType;
            if ((cameraType != CameraType.Game) &&
                (cameraType != CameraType.SceneView))
            {
                if (cameraType == CameraType.Reflection)
                    return true;
                //Hard code
                //When cameraType == CameraType.Preview, and
                //camera.clearFlags == CameraClearFlags.Depth
                //It is represented as MaterialPreviewCamera
                else if ((cameraType == CameraType.Preview) &&
                    (camera.clearFlags == CameraClearFlags.Depth))
                    return true;
            }
            return false;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (IsSkipCamera(camera))
                return;

            var cullingCamera = camera;
#if UNITY_EDITOR
            if (m_isOnlyViewMainCameraCulling)
                cullingCamera = Camera.main;
#endif //UNITY_EDITOR

            Bounds renderBounds;
            Vector2 cellSize;
            m_grassParamsSector.GetRenderBoundAndCellSize(out renderBounds, out cellSize);

            m_visableCellsCuller.ProcessCulling(cullingCamera, in cellSize, in m_proceduralGrassData.m_worldRect,
                in msr_worldMinMaxHeight);

            var visibleCellIndices = m_visableCellsCuller.GetVisibleCellIndices();
            var visibleCellIndexCount = visibleCellIndices.Count;
            if (visibleCellIndexCount <= 0)
                return;

            m_targetProceduralInstanceFilterCS.SetVector(msr_cameraPositionID,
                camera.transform.position);

            visibleCellIndices.CopyTo(m_visibleCellIndices);
            m_visibleCellIndexBuffer.SetData(m_visibleCellIndices, 0, 0, visibleCellIndexCount);

            m_targetProceduralInstanceFilterCS.SetMatrix(msr_viewProjectionMatrixID,
                camera.projectionMatrix * camera.worldToCameraMatrix);

            if (m_meshMode == MESH_MDOE.BILLBOARD)
            {
                var cameraTransform = camera.transform;
                m_targetProceduralInstanceFilterCS.SetVector(msr_cameraForwardWSID, cameraTransform.forward);
                m_targetProceduralInstanceFilterCS.SetVector(msr_cameraRightWSID, cameraTransform.right);
            }

            m_instanceCountBuffer.SetCounterValue(0);

            ref uint2 cellInstanceColumnRowCount = ref m_proceduralGrassData.m_cellInstanceColumnRowCount;
            var processInstanceCount = cellInstanceColumnRowCount.x * cellInstanceColumnRowCount.y * 
                visibleCellIndexCount;

            DispatchComputeInBatches(m_targetProceduralInstanceFilterCS, (int)processInstanceCount);

            ComputeBuffer.CopyCount(m_instanceCountBuffer, m_indirectArgumentsBuffer, sizeof(uint));

            Graphics.DrawMeshInstancedIndirect(GetGrassMesh(), 0, m_grassMaterial, renderBounds, 
                m_indirectArgumentsBuffer, 0, null, ShadowCastingMode.Off, true, 0, camera);
        }

        private void RefereshVisibleCellIndexBuffer()
        {
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
                m_visibleCellIndexBuffer = new ComputeBuffer(m_visibleCellIndices.Length, 
                    Marshal.SizeOf(typeof(uint)));
                m_currentCellCount = cellCount;
                m_targetProceduralInstanceFilterCS.SetBuffer(0, msr_visibleCellIndexBufferID,
                    m_visibleCellIndexBuffer);
            }
        }

        private void RefereshProcessWeightMapFilterKeyword()
        {
            bool isProcessWeightMapFilter = true;
#if UNITY_EDITOR
            isProcessWeightMapFilter = m_isProcessWeightMapFilter;
#endif
            if (isProcessWeightMapFilter)
            {
                m_targetProceduralInstanceFilterCS.EnableKeyword(mc_processWeightMapFilterKeyword);
                m_targetProceduralInstanceFilterCS.SetTexture(0, msr_weightMapID,
                    m_proceduralGrassData.m_weightMap);
            }
            else
                m_targetProceduralInstanceFilterCS.DisableKeyword(mc_processWeightMapFilterKeyword);
        }


private Mesh GetGrassMesh()
        {   
            var targetGrassMesh = m_meshMode == MESH_MDOE.PYRAMID ? GetPyramidMesh() : GetTriangleMesh();
            if (targetGrassMesh == m_targetGrassMesh)
                return m_targetGrassMesh;

            m_targetGrassMesh = targetGrassMesh;
            int subMeshIndex = Mathf.Clamp(0, 0, m_targetGrassMesh.subMeshCount - 1);
            ms_tempIndirectArguments[0] = m_targetGrassMesh.GetIndexCount(
                subMeshIndex);
            ms_tempIndirectArguments[1] = 0;
            ms_tempIndirectArguments[2] = m_targetGrassMesh.GetIndexStart(
                subMeshIndex);
            ms_tempIndirectArguments[3] = m_targetGrassMesh.GetBaseVertex(
                subMeshIndex);
            ms_tempIndirectArguments[4] = 0;
            m_indirectArgumentsBuffer.SetData(ms_tempIndirectArguments);

            if (m_meshMode == MESH_MDOE.BILLBOARD)
            {
                m_grassMaterial.EnableKeyword(mc_processBillboardKeyword);
                m_targetProceduralInstanceFilterCS.EnableKeyword(mc_processBillboardKeyword);
            }
            else
            {
                m_grassMaterial.DisableKeyword(mc_processBillboardKeyword);
                m_targetProceduralInstanceFilterCS.DisableKeyword(mc_processBillboardKeyword);
            }
            
            return m_targetGrassMesh;
        }

        private Mesh GetTriangleMesh()
        {
            if (m_triangleMesh)
                return m_triangleMesh;

            m_triangleMesh = new Mesh();
            Vector3[] verts = new Vector3[3];
            verts[0] = new Vector3(-mc_grassMeshWidth, 0.0f, 0.0f);
            verts[1] = new Vector3(mc_grassMeshWidth, 0.0f, 0.0f);
            verts[2] = new Vector3(0.0f, 1.0f, 0.0f);

            m_triangleMesh.SetVertices(verts);
            m_triangleMesh.SetTriangles(new int[3] { 2, 1, 0, }, 0);

            return m_triangleMesh;
        }

        private Mesh GetPyramidMesh()
        {
            if (m_pyramidMesh)
                return m_pyramidMesh;

            m_pyramidMesh = new Mesh();

            // Vertices: three at the base + one at the top
            Vector3[] verts = new Vector3[4];
            verts[0] = new Vector3(-mc_grassMeshWidth, 0, -mc_grassMeshWidth);  // Base vertex 1
            verts[1] = new Vector3(mc_grassMeshWidth, 0, -mc_grassMeshWidth);   // Base vertex 2
            verts[2] = new Vector3(0, 0, mc_grassMeshWidth);                    // Base vertex 3
            verts[3] = new Vector3(0, 1, 0);                                    // Top vertex

            // Triangles: one base + three sides
            int[] triangles = new int[]
            {
                0, 1, 2, // Base
                0, 3, 1, // Side 1
                1, 3, 2, // Side 2
                2, 3, 0  // Side 3
            };

            Vector3[] normals = new Vector3[4];

            normals[0] = Vector3.right;
            normals[1] = Vector3.right;
            normals[2] = Vector3.right;

            Vector3 n0 = Vector3.Cross(verts[3] - verts[0], verts[1] - verts[0]).normalized;
            Vector3 n1 = Vector3.Cross(verts[3] - verts[1], verts[2] - verts[1]).normalized;
            Vector3 n2 = Vector3.Cross(verts[3] - verts[2], verts[0] - verts[2]).normalized;
            normals[3] = ((n0 + n1 + n2) / 3).normalized;

            m_pyramidMesh.SetVertices(verts);
            m_pyramidMesh.SetNormals(normals);
            m_pyramidMesh.SetTriangles(triangles, 0);

            return m_pyramidMesh;
        }

        /// <summary>
        /// Dispatches a ComputeShader in batches, supporting more than 65535 thread groups.
        /// </summary>
        /// <param name="targetComputeShader">The target ComputeShader.</param>
        /// <param name="processInstanceCount">Total number of instances to process.</param>
        /// <param name="kernel">Index of the kernel to execute (default: 0).</param>
        /// <param name="threadGroupSize">Number of instances per thread group (default: 64).</param>
        /// <param name="maxDispatchCount">Maximum number of thread groups allowed per dispatch (default: 65535).</param>
        private static void DispatchComputeInBatches(ComputeShader targetComputeShader,
            int processInstanceCount, int kernel = 0, int threadGroupSize = 64, 
            int maxDispatchCount = 65535)
        {
            // Set the total number of instances for the shader to access (for bounds checking)
            targetComputeShader.SetInt(msr_maxProcessCountID, processInstanceCount);

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
                targetComputeShader.SetInt(msr_offestCountID, offset);

                // Dispatch the current batch
                targetComputeShader.Dispatch(kernel, dispatchThisBatch, 1, 1);

                // Update offset to mark processed instances
                offset += dispatchThisBatch * threadGroupSize;
            }
        }

        private void AppendExtendStringCB(ref string text)
        {
#if VIEW_VISIBLE_INSTANCE_COUNT
            m_indirectArgumentsBuffer.GetData(ms_tempIndirectArguments);
            text = string.Format("\n=============\nLoadTime:{0}S\nVisibleInstanceCount:{1}", 
                m_executeTime / 1000.0f, ms_tempIndirectArguments[1]);
#else
            text = string.Format("\n=============\nLoadTime:{0}S", m_executeTime / 1000.0f);
#endif
        }

#if UNITY_EDITOR
        private void EditUpdate()
        {
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }

        [Tooltip("Only show main camera view scope (for debug purpose).")]
        [SerializeField]
        private bool m_isOnlyViewMainCameraCulling = false;

        [Tooltip("Process Weight Map Filter flag")]
        [SerializeField]
        private bool m_isProcessWeightMapFilter = true;
#endif

        public ProceduralGrassData m_proceduralGrassData = null;
        [SerializeField]
        private MESH_MDOE m_meshMode = MESH_MDOE.PYRAMID;

        [SerializeField]
        private DisplayFPS m_targetDisplayFPS = null;

        private ComputeShader m_targetProceduralInstanceFilterCS = null;

        private Mesh m_targetGrassMesh = null;
        private Mesh m_pyramidMesh = null;
        private Mesh m_triangleMesh = null;
        private Material m_grassMaterial = null;

        private uint[] m_visibleCellIndices = null;
        private ComputeBuffer m_visibleCellIndexBuffer = null;

        private ComputeBuffer m_instanceCountBuffer = null;
        private RenderTexture m_filterResultRT = null;

        // 2 pixels record one instance, which means 2,097,152 instances.
        private Vector2Int m_filterResultSize = new Vector2Int(2048, 2048);

        private VisableCellsCuller m_visableCellsCuller = new VisableCellsCuller();

        private uint m_currentCellCount = 0;
        private ComputeBuffer m_indirectArgumentsBuffer;

        private ExecuteTimeProcessor m_executeTimeProcessor = new ExecuteTimeProcessor();
        private long m_executeTime = 0;

        private GrassParamsSector m_grassParamsSector = new GrassParamsSector();

        private static uint[] ms_tempIndirectArguments = new uint[5] { 0, 0, 0, 0, 0 };

        private static readonly Vector2 msr_worldMinMaxHeight = new Vector2(0.0f, 10.0f);

        public static readonly int msr_instanceCountBufferID = Shader.PropertyToID("_InstanceCountBuffer");
        public static readonly int msr_filterResultRTSizeID = Shader.PropertyToID("_FilterResultRTSize");
        public static readonly int msr_cameraPositionID = Shader.PropertyToID("_CameraPosition");
        public static readonly int msr_viewProjectionMatrixID = Shader.PropertyToID("_ViewProjectionMatrix");
        public static readonly int msr_visibleCellIndexBufferID = Shader.PropertyToID("_VisibleCellIndexBuffer");
        public static readonly int msr_currentTimeID = Shader.PropertyToID("_CurrentTime");
        public static readonly int msr_weightMapID = Shader.PropertyToID("_WeightMap");
        public static readonly int msr_maxProcessCountID = Shader.PropertyToID("_MaxProcessCount");
        public static readonly int msr_offestCountID = Shader.PropertyToID("_OffsetCount");
        public static readonly int msr_interactorCollisionSphereID = Shader.PropertyToID("_InteractorCollisionSphere");
        public static readonly int msr_cameraForwardWSID = Shader.PropertyToID("_CameraForwardWS");
        public static readonly int msr_cameraRightWSID = Shader.PropertyToID("_CameraRightWS");
        
        private const float mc_grassMeshWidth = 0.25f;
        private const string mc_processWeightMapFilterKeyword = "PROCESS_WEIGHT_MAP_FILTER";
        private const string mc_processBillboardKeyword = "PROCESS_BILLBOARD";

        private enum MESH_MDOE
        {
            PYRAMID,
            BILLBOARD
        };
    }
}