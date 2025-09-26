/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/26
/// Desc:
/// </summary>

using Unity.Mathematics;
using UnityEngine;

namespace SB.ProceduralGrass
{
    public class GrassParamsSector
    {
        public void GetRenderBoundAndCellSize(out Bounds bounds, out Vector2 cellSize)
        {
            bounds = m_renderBound;
            cellSize = m_cellSize;
        }

        public void ReInitialize(ComputeShader targetProceduralInstanceFilterCS, Material targetGrassMaterial,
            ProceduralGrassData targetProceduralGrassData, RenderTexture targetFilterResultRT)
        {
            Release();
            m_targetProceduralInstanceFilterCS = targetProceduralInstanceFilterCS;
            m_targetGrassMaterial = targetGrassMaterial;
            m_targetProceduralGrassData = targetProceduralGrassData;
            m_targetFilterResultRT = targetFilterResultRT;
        }
        public void Release()
        {
        }

        public void RefreshParams()
        {
            if ((!m_targetProceduralGrassData) || (!m_targetProceduralInstanceFilterCS) 
                || (!m_targetGrassMaterial) || (!m_targetFilterResultRT))
                return;

            ref uint2 cellColumnRowCount = ref m_targetProceduralGrassData.m_cellColumnRowCount;
            var cellCount = cellColumnRowCount.x * cellColumnRowCount.y;
            if (cellCount <= 0)
                return;

            m_targetProceduralInstanceFilterCS.SetTexture(0, msr_filterResultRTID, m_targetFilterResultRT);

            m_targetGrassMaterial.SetTexture(msr_filterResultRTID, m_targetFilterResultRT);

            // Clamp fade range to avoid invalid interval, then pass squared distances to shader
            float fadeStartDistance = Mathf.Min(m_targetProceduralGrassData.m_fadeStartDistance,
                m_targetProceduralGrassData.m_fadeEndDistance - 1.0f);

            float maxViewDistance = Mathf.Max(fadeStartDistance + 1.0f,
                m_targetProceduralGrassData.m_fadeEndDistance);

            //referesh shared property
            Shader.SetGlobalFloat(msr_maxViewSquareDistanceID, maxViewDistance * maxViewDistance);

            m_targetGrassMaterial.SetFloat(GrassShaderID.msr_fadeStartSquareDistance, 
                fadeStartDistance * fadeStartDistance);

            var windParameters = m_targetProceduralGrassData.m_windParameters;

            var rotation = Quaternion.Euler(0.0f, windParameters.m_windYawAngle, 0.0f);
            var direction = rotation * Vector3.forward;
            m_targetGrassMaterial.SetVector(GrassShaderID.msr_windDirection, new Vector2(direction.x, direction.z));

            m_targetGrassMaterial.SetVector(GrassShaderID.msr_shadingParams, new Vector4(
                windParameters.m_windNormalWeight, m_targetProceduralGrassData.m_colorTextureTileScale,
                m_targetProceduralGrassData.m_fadePow, m_targetProceduralGrassData.m_interactorAffectWeight));

            m_targetGrassMaterial.SetTexture(GrassShaderID.msr_ColorTexture, 
                m_targetProceduralGrassData.m_grassColorTexture);

            ref Rect worldRect = ref m_targetProceduralGrassData.m_worldRect;

            var min = worldRect.min;
            var max = worldRect.max;
            m_renderBound = new Bounds();
            m_renderBound.SetMinMax(
                new Vector3(min.x, msr_worldMinMaxHeight.x, min.y),
                new Vector3(max.x, msr_worldMinMaxHeight.y, max.y));

            Shader.SetGlobalVector(msr_worldMinMaxID, new float4(worldRect.min, worldRect.max));
            
            var widthSizeInfo = m_targetProceduralGrassData.m_widthSizeInfo;
            m_targetProceduralInstanceFilterCS.SetVector(ProceduralInstanceFilterID.msr_widthSizeInfo,
                new Vector3(widthSizeInfo.m_size, widthSizeInfo.m_minSizeOffest,
                widthSizeInfo.m_maxSizeOffest));

            var heightSizeInfo = m_targetProceduralGrassData.m_heightSizeInfo;
            m_targetProceduralInstanceFilterCS.SetVector(ProceduralInstanceFilterID.msr_heightSizeInfo,
                new Vector3(heightSizeInfo.m_size, heightSizeInfo.m_minSizeOffest,
                heightSizeInfo.m_maxSizeOffest));

            m_cellSize = new Vector2(worldRect.width / cellColumnRowCount.x,
                worldRect.height / cellColumnRowCount.y);

            m_targetProceduralInstanceFilterCS.SetVector(ProceduralInstanceFilterID.msr_cellSize, m_cellSize);

            m_targetProceduralInstanceFilterCS.SetInt(ProceduralInstanceFilterID.msr_cellColumnCount,
                (int)cellColumnRowCount.x);

            ref uint2 cellInstanceColumnRowCount = ref m_targetProceduralGrassData.m_cellInstanceColumnRowCount;

            m_targetProceduralInstanceFilterCS.SetInt(ProceduralInstanceFilterID.msr_cellInstanceColumnCount,
                (int)cellInstanceColumnRowCount.x);

            m_targetProceduralInstanceFilterCS.SetInt(ProceduralInstanceFilterID.msr_cellInstanceCount,
                (int)(cellInstanceColumnRowCount.x * cellInstanceColumnRowCount.y));

            m_targetProceduralInstanceFilterCS.SetFloat(ProceduralInstanceFilterID.msr_jitterStrength,
                m_targetProceduralGrassData.m_jitterStrength);

            m_targetProceduralInstanceFilterCS.SetVector(ProceduralInstanceFilterID.msr_InstanceSpacing,
                new Vector2(m_cellSize.x / cellInstanceColumnRowCount.x, m_cellSize.y / cellInstanceColumnRowCount.y));

            var maxSize = Mathf.Max(
                widthSizeInfo.m_size + widthSizeInfo.m_maxSizeOffest,
                heightSizeInfo.m_size + heightSizeInfo.m_maxSizeOffest);

            Shader.SetGlobalFloat(msr_maxInstanceSizeID, maxSize);

            m_targetProceduralInstanceFilterCS.SetFloat(ProceduralInstanceFilterID.msr_windWeight,
                windParameters.m_windIntensityRatio);

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

        private ComputeShader m_targetProceduralInstanceFilterCS = null;
        private Material m_targetGrassMaterial = null;
        private ProceduralGrassData m_targetProceduralGrassData = null;
        private RenderTexture m_targetFilterResultRT = null;        
        private Bounds m_renderBound;
        private Vector2 m_cellSize = Vector2.one;

        private static readonly Vector2 msr_worldMinMaxHeight = new Vector2(0.0f, 10.0f);

        private static readonly int msr_filterResultRTID = Shader.PropertyToID("_FilterResultRT");
        private static readonly int msr_maxViewSquareDistanceID = Shader.PropertyToID("_MaxViewSquareDistance");
        private static readonly int msr_maxInstanceSizeID = Shader.PropertyToID("_MaxInstanceSize");
        private static readonly int msr_worldMinMaxID = Shader.PropertyToID("_WorldMinMax");
        
        private static class ProceduralInstanceFilterID
        {
            //public static readonly int msr_offestCount = Shader.PropertyToID("_OffsetCount");
            //public static readonly int msr_maxProcessCount = Shader.PropertyToID("_MaxProcessCount");

            public static readonly int msr_cellSize = Shader.PropertyToID("_CellSize");
            public static readonly int msr_cellColumnCount = Shader.PropertyToID("_CellColumnCount");
            public static readonly int msr_cellInstanceColumnCount = Shader.PropertyToID("_CellInstanceColumnCount");
            public static readonly int msr_cellInstanceCount = Shader.PropertyToID("_CellInstanceCount");
            public static readonly int msr_jitterStrength = Shader.PropertyToID("_JitterStrength");
            public static readonly int msr_InstanceSpacing = Shader.PropertyToID("_InstanceSpacing");


            public static readonly int msr_widthSizeInfo = Shader.PropertyToID("_WidthSizeInfo");
            public static readonly int msr_heightSizeInfo = Shader.PropertyToID("_HeightSizeInfo");

            public static readonly int msr_windWeight = Shader.PropertyToID("_WindWeight");
            public static readonly int msr_WindAParams = Shader.PropertyToID("_WindAParams");
            public static readonly int msr_windATilingWrap = Shader.PropertyToID("_WindATilingWrap");
            public static readonly int msr_WindBParams = Shader.PropertyToID("_WindBParams");
            public static readonly int msr_windBTilingWrap = Shader.PropertyToID("_WindBTilingWrap");
            public static readonly int msr_WindCParams = Shader.PropertyToID("_WindCParams");
            public static readonly int msr_windCTilingWrap = Shader.PropertyToID("_WindCTilingWrap");
            
            //public static readonly int msr_currentTime = Shader.PropertyToID("_CurrentTime");
            //public static readonly int msr_weightMap = Shader.PropertyToID("_WeightMap");
        }

        private static class GrassShaderID
        {
            public static readonly int msr_fadeStartSquareDistance = Shader.PropertyToID("_FadeStartSquareDistance");
            public static readonly int msr_ColorTexture = Shader.PropertyToID("_ColorTexture");
            public static readonly int msr_windDirection = Shader.PropertyToID("_WindDirection");
            //public static readonly int msr_interactorCollisionSphere = Shader.PropertyToID("_InteractorCollisionSphere");
            public static readonly int msr_shadingParams = Shader.PropertyToID("_ShadingParams");
        }

        //private const string mc_processWeightMapFilterKeyword = "PROCESS_WEIGHT_MAP_FILTER";
    }
}
