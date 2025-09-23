/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/19
/// Desc:
/// </summary>

//#############初步測試，網格數還是有差的，改成billboard性能會比較好…但是有顯示上的問題，先放棄…
//#############1.單三角：116 fps（家裡電腦）
//#############2.coneMesh: 86 fps（家裡電腦）
//#############1.考慮要不要改成billboard...
//2.要怎麼算specular, out line rim，還是中心點…
//3.整合WeightMap...
//4.VertexShader處理Collision...

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

            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.update -= EditUpdate;
                UnityEditor.EditorApplication.update += EditUpdate;
            }
#endif //UNITY_EDITOR
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.update -= EditUpdate;

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

            if (m_visibleInstanceBuffer != null)
            {
                m_visibleInstanceBuffer.Release();
                m_visibleInstanceBuffer.Dispose();
            }

            m_visibleInstanceBuffer = new ComputeBuffer((int)mc_maxVisibleInstanceCount,
                Marshal.SizeOf(typeof(GrassInstanceData)), ComputeBufferType.Append);

            m_targetProceduralInstanceFilterCS.SetBuffer(0, msr_visibleInstanceBufferID,
                m_visibleInstanceBuffer);            

            m_indirectArgumentsBuffer = new ComputeBuffer(1, ms_tempIndirectArguments.Length * sizeof(uint),
                ComputeBufferType.IndirectArguments);
            
            var grassMesh = GetGrassMesh();
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

            if (m_visibleInstanceBuffer != null)
            {
                m_visibleInstanceBuffer.Release();
                m_visibleInstanceBuffer.Dispose();
                m_visibleInstanceBuffer = null;
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
            m_targetProceduralInstanceFilterCS.SetFloat(ProceduralInstanceFilterID.msr_currentTime,
                Time.time);
#if UNITY_EDITOR
            RefreshParams();
#endif //UNITY_EDITOR
        }

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

            var cullingCamera = camera;
#if UNITY_EDITOR
            if (m_isOnlyViewMainCameraCulling)
                cullingCamera = Camera.main;
#endif //UNITY_EDITOR

            m_visableCellsCuller.ProcessCulling(cullingCamera, in m_cellSize, in m_proceduralGrassData.m_worldRect,
                in msr_worldMinMaxHeight);

            var visibleCellIndices = m_visableCellsCuller.GetVisibleCellIndices();
            var visibleCellIndexCount = visibleCellIndices.Count;
            if (visibleCellIndexCount <= 0)
                return;

            m_targetProceduralInstanceFilterCS.SetVector(ProceduralInstanceFilterID.msr_cameraPosition,
                camera.transform.position);

            visibleCellIndices.CopyTo(m_visibleCellIndices);
            m_visibleCellIndexBuffer.SetData(m_visibleCellIndices, 0, 0, visibleCellIndexCount);

            m_targetProceduralInstanceFilterCS.SetMatrix(ProceduralInstanceFilterID.msr_viewProjectionMatrix,
                camera.projectionMatrix * camera.worldToCameraMatrix);

            m_visibleInstanceBuffer.SetCounterValue(0);

            ref uint2 cellInstanceColumnRowCount = ref m_proceduralGrassData.m_cellInstanceColumnRowCount;
            var processInstanceCount = cellInstanceColumnRowCount.x * cellInstanceColumnRowCount.y * visibleCellIndexCount;

            DispatchComputeInBatches(m_targetProceduralInstanceFilterCS, (int)processInstanceCount);

            ComputeBuffer.CopyCount(m_visibleInstanceBuffer, m_indirectArgumentsBuffer, 4);

            Graphics.DrawMeshInstancedIndirect(GetGrassMesh(), 0, m_grassMaterial, m_renderBound, m_indirectArgumentsBuffer,
                0, null, ShadowCastingMode.Off, true, 0, camera);
        }
        private void OnProceduralGrassDataChangeCB()
        {
            //RefreshParams();
        }

        private void RefreshParams()
        {
            if ((!m_proceduralGrassData) || (!m_targetProceduralInstanceFilterCS) || (!m_grassMaterial))
                return;

            // Clamp fade range to avoid invalid interval, then pass squared distances to shader
            float fadeStartDistance = Mathf.Min(m_proceduralGrassData.m_fadeStartDistance,
                m_proceduralGrassData.m_fadeEndDistance - 1.0f);

            float maxViewDistance = Mathf.Max(fadeStartDistance + 1.0f, 
                m_proceduralGrassData.m_fadeEndDistance);

            //referesh shared property
            Shader.SetGlobalFloat(msr_maxViewSquareDistanceID, maxViewDistance * maxViewDistance);

            m_grassMaterial.SetFloat(GrassShaderID.msr_fadeStartSquareDistance, fadeStartDistance * fadeStartDistance);

            var windParameters = m_proceduralGrassData.m_windParameters;

            //public Vector2 GetMovementParams(float directionAngle)
            //{
            //    var rotation = Quaternion.Euler(0.0f, directionAngle, 0.0f);
            //    var direction = rotation * Vector3.forward;
            //    return new Vector2(direction.x, direction.z);
            //}
            var rotation = Quaternion.Euler(0.0f, windParameters.m_windYawAngle, 0.0f);
            var direction = rotation * Vector3.forward;
            m_grassMaterial.SetVector(GrassShaderID.msr_windDirection, new Vector2(direction.x, direction.z));

            var specularColor = m_proceduralGrassData.m_specularColor;

            m_grassMaterial.SetVector(GrassShaderID.msr_specularColor, new Vector4(
                specularColor.r, specularColor.g, specularColor.b, m_proceduralGrassData.m_specularWeightPow));

            m_grassMaterial.SetFloat(GrassShaderID.msr_windNormalWeight, windParameters.m_windNormalWeight);

            m_grassMaterial.SetTexture(GrassShaderID.msr_ColorTexture, m_proceduralGrassData.m_grassColorTexture);

            m_grassMaterial.SetBuffer(msr_visibleInstanceBufferID, m_visibleInstanceBuffer);            

            ref Rect worldRect = ref m_proceduralGrassData.m_worldRect;

            var min = worldRect.min;
            var max = worldRect.max;
            m_renderBound = new Bounds();
            m_renderBound.SetMinMax(
                new Vector3(min.x, msr_worldMinMaxHeight.x, min.y),
                new Vector3(max.x, msr_worldMinMaxHeight.y, max.y));

            
            m_targetProceduralInstanceFilterCS.SetVector(ProceduralInstanceFilterID.msr_worldMinMax, new float4(worldRect.min, worldRect.max));

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

            var widthSizeInfo = m_proceduralGrassData.m_widthSizeInfo;
            m_targetProceduralInstanceFilterCS.SetVector(ProceduralInstanceFilterID.msr_widthSizeInfo,
                new Vector3(widthSizeInfo.m_size, widthSizeInfo.m_minSizeOffest,
                widthSizeInfo.m_maxSizeOffest));

            var heightSizeInfo = m_proceduralGrassData.m_heightSizeInfo;
            m_targetProceduralInstanceFilterCS.SetVector(ProceduralInstanceFilterID.msr_heightSizeInfo,
                new Vector3(heightSizeInfo.m_size, heightSizeInfo.m_minSizeOffest,
                heightSizeInfo.m_maxSizeOffest));


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

            m_targetProceduralInstanceFilterCS.SetFloat(ProceduralInstanceFilterID.msr_jitterStrength,
                m_proceduralGrassData.m_jitterStrength);

            m_targetProceduralInstanceFilterCS.SetVector(ProceduralInstanceFilterID.msr_InstanceSpacing,
                new Vector2(m_cellSize.x / cellInstanceColumnRowCount.x, m_cellSize.y / cellInstanceColumnRowCount.y));
           
            var maxSize = Mathf.Max(
                widthSizeInfo.m_size + widthSizeInfo.m_maxSizeOffest,
                heightSizeInfo.m_size + heightSizeInfo.m_maxSizeOffest);

            m_targetProceduralInstanceFilterCS.SetFloat(ProceduralInstanceFilterID.msr_maxInstanceSize,
                maxSize);            

            m_targetProceduralInstanceFilterCS.SetFloat(ProceduralInstanceFilterID.msr_windWeight, windParameters.m_windIntensityRatio);

            var windInfo = windParameters.m_windInfoA;
            m_targetProceduralInstanceFilterCS.SetVector(ProceduralInstanceFilterID.msr_WindAParams, new Vector2(
                windInfo.m_windIntensity, windInfo.m_windFrequency));

            var tiling = windInfo.m_windTiling;
            var wrap = windInfo.m_windWrap;
            m_targetProceduralInstanceFilterCS.SetVector(ProceduralInstanceFilterID.msr_windATilingWrap, new Vector4(
                tiling.x, tiling.y, wrap.x, wrap.y));

            windInfo = windParameters.m_windInfoB;
            m_targetProceduralInstanceFilterCS.SetVector(ProceduralInstanceFilterID.msr_WindBParams, new Vector2(
                windInfo.m_windIntensity, windInfo.m_windFrequency));

            tiling = windInfo.m_windTiling;
            wrap = windInfo.m_windWrap;
            m_targetProceduralInstanceFilterCS.SetVector(ProceduralInstanceFilterID.msr_windBTilingWrap, new Vector4(
                tiling.x, tiling.y, wrap.x, wrap.y));

            windInfo = windParameters.m_windInfoC;
            m_targetProceduralInstanceFilterCS.SetVector(ProceduralInstanceFilterID.msr_WindCParams, new Vector2(
                windInfo.m_windIntensity, windInfo.m_windFrequency));

            tiling = windInfo.m_windTiling;
            wrap = windInfo.m_windWrap;
            m_targetProceduralInstanceFilterCS.SetVector(ProceduralInstanceFilterID.msr_windCTilingWrap, new Vector4(
                tiling.x, tiling.y, wrap.x, wrap.y));
        }

        //private Mesh GetGrassMesh()
        //{
        //    if (m_cachedGrassMesh)
        //        return m_cachedGrassMesh;

        //    m_cachedGrassMesh = new Mesh();
        //    Vector3[] verts = new Vector3[3];
        //    verts[0] = new Vector3(-mc_grassMeshWidth, 0.0f, 0.0f);
        //    verts[1] = new Vector3(mc_grassMeshWidth, 0.0f, 0.0f);
        //    verts[2] = new Vector3(0.0f, 1.0f, 0.0f);

        //    m_cachedGrassMesh.SetVertices(verts);
        //    m_cachedGrassMesh.SetTriangles(new int[3] { 2, 1, 0, }, 0);

        //    return m_cachedGrassMesh;
        //}

        private Mesh GetGrassMesh()
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

            // Normals
            Vector3[] normals = new Vector3[4];

            // 底部三個點法線都指向下方
            //normals[0] = Vector3.down;
            //normals[1] = Vector3.down;
            //normals[2] = Vector3.down;
            normals[0] = Vector3.right;
            normals[1] = Vector3.right;
            normals[2] = Vector3.right;

            // 頂點的法線：取三個側面法線平均
            Vector3 n0 = Vector3.Cross(verts[3] - verts[0], verts[1] - verts[0]).normalized;
            Vector3 n1 = Vector3.Cross(verts[3] - verts[1], verts[2] - verts[1]).normalized;
            Vector3 n2 = Vector3.Cross(verts[3] - verts[2], verts[0] - verts[2]).normalized;
            normals[3] = ((n0 + n1 + n2) / 3).normalized;

            m_cachedGrassMesh.SetVertices(verts);
            m_cachedGrassMesh.SetNormals(normals);
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

#if UNITY_EDITOR
        private void EditUpdate()
        {
            //if (!Application.isPlaying)
            //UpdateCS();
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }

        [Tooltip("Only show main camera view scope (for debug purpose).")]
        [SerializeField]
        private bool m_isOnlyViewMainCameraCulling = false;
#endif

        public ProceduralGrassData m_proceduralGrassData = null;
        
        private ComputeShader m_targetProceduralInstanceFilterCS = null;

        private Mesh m_cachedGrassMesh = null;
        private Material m_grassMaterial = null;
        private Bounds m_renderBound;

        private uint[] m_visibleCellIndices = null;
        private ComputeBuffer m_visibleCellIndexBuffer = null;
        private Vector2 m_cellSize = Vector2.one;
        
        private ComputeBuffer m_visibleInstanceBuffer;

        private VisableCellsCuller m_visableCellsCuller = new VisableCellsCuller();

        private uint m_currentCellCount = 0;
        private ComputeBuffer m_indirectArgumentsBuffer;

        private static uint[] ms_tempIndirectArguments = new uint[5] { 0, 0, 0, 0, 0 };

        private static readonly Vector2 msr_worldMinMaxHeight = new Vector2(0.0f, 10.0f);        

        private static readonly int msr_visibleInstanceBufferID = Shader.PropertyToID("_VisibleInstanceBuffer");
        private static readonly int msr_maxViewSquareDistanceID = Shader.PropertyToID("_MaxViewSquareDistance");
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
            public static readonly int msr_jitterStrength = Shader.PropertyToID("_JitterStrength");
            public static readonly int msr_InstanceSpacing = Shader.PropertyToID("_InstanceSpacing");
            public static readonly int msr_maxInstanceSize = Shader.PropertyToID("_MaxInstanceSize");
            public static readonly int msr_cameraPosition = Shader.PropertyToID("_CameraPosition");

            public static readonly int msr_widthSizeInfo = Shader.PropertyToID("_WidthSizeInfo");
            public static readonly int msr_heightSizeInfo = Shader.PropertyToID("_HeightSizeInfo");
            public static readonly int msr_worldMinMax = Shader.PropertyToID("_WorldMinMax");

            public static readonly int msr_windWeight = Shader.PropertyToID("_WindWeight");
            public static readonly int msr_WindAParams = Shader.PropertyToID("_WindAParams");
            public static readonly int msr_windATilingWrap = Shader.PropertyToID("_WindATilingWrap");
            public static readonly int msr_WindBParams = Shader.PropertyToID("_WindBParams");
            public static readonly int msr_windBTilingWrap = Shader.PropertyToID("_WindBTilingWrap");
            public static readonly int msr_WindCParams = Shader.PropertyToID("_WindCParams");
            public static readonly int msr_windCTilingWrap = Shader.PropertyToID("_WindCTilingWrap");
            public static readonly int msr_currentTime = Shader.PropertyToID("_CurrentTime");
        }

        private static class GrassShaderID
        {
            public static readonly int msr_fadeStartSquareDistance = Shader.PropertyToID("_FadeStartSquareDistance");            
            public static readonly int msr_ColorTexture = Shader.PropertyToID("_ColorTexture");
            public static readonly int msr_windDirection = Shader.PropertyToID("_WindDirection");
            public static readonly int msr_specularColor = Shader.PropertyToID("_SpecularColor");
            public static readonly int msr_windNormalWeight = Shader.PropertyToID("_WindNormalWeight");            
        }

        private const float mc_grassMeshWidth = 0.25f;

        private const uint mc_maxVisibleInstanceCount = 1000000;
        

        private struct GrassInstanceData
        {
            public Vector2 position2D;
            public Vector2 sizeFactor;            
            public float yawSin;
            public float yawCos;
            public float wind;
        }
    }
}
