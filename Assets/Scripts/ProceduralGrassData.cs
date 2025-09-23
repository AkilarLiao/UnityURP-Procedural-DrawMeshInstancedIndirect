/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/19
/// Desc:
/// </summary>

using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace SB.ProceduralGrass
{   
    public class ProceduralGrassData : ScriptableObject
    {
        public delegate void OnProceduralGrassDataChangeCB();
        public OnProceduralGrassDataChangeCB OnProceduralGrassDataChange { get; set; } =
            null;
        private void Awake()
        {
#if UNITY_EDITOR
            ResourceReloader.ReloadAllNullIn(this, "Assets");
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (OnProceduralGrassDataChange != null)
                OnProceduralGrassDataChange();
        }

        [UnityEditor.MenuItem("Assets/Create/Procedural Grass Data")]
        public static void CreateProceduralGrassData()
        {   
            ProceduralGrassData asset = ScriptableObject.CreateInstance<ProceduralGrassData>();

            asset.m_grassColorTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Textures/GrassGround.png");

            string path = UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.Selection.activeObject);
            if (string.IsNullOrEmpty(path))
            {
                path = "Assets";
            }
            else if (!System.IO.Directory.Exists(path))
            {
                path = System.IO.Path.GetDirectoryName(path);
            }

            string assetPathAndName = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(path + "/ProceduralGrassData.asset");
            
            UnityEditor.AssetDatabase.CreateAsset(asset, assetPathAndName);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            
            UnityEditor.EditorUtility.FocusProjectWindow();
            UnityEditor.Selection.activeObject = asset;
        }

#endif
        public Rect m_worldRect = new Rect(Vector2.zero, 2000.0f * Vector2.one);
        public uint2 m_cellColumnRowCount = new uint2(20, 20);
        public uint2 m_cellInstanceColumnRowCount = new uint2(100, 100);
        [Range(0.0f, 10.0f)]
        public float m_jitterStrength = 5.0f;

        [Tooltip("The width sizeInfo.")]
        public SizeInfo m_widthSizeInfo;

        [Tooltip("The height sizeInfo.")]
        public SizeInfo m_heightSizeInfo;

        [Tooltip("Distance at which the object begins to fade out.")]
        [Range(10.0f, 1000.0f)]
        public float m_fadeStartDistance = 200.0f;

        [Tooltip("Distance at which the object is fully invisible (culled).")]
        [Range(20.0f, 1000.0f)]
        public float m_fadeEndDistance = 300.0f;
        
        [Tooltip("The wind parameters.")]
        public WindParameters m_windParameters = new WindParameters();        

        [Tooltip("The specular color.")]
        public Color m_specularColor = new Color(1.0f, 1.0f, 0.5f, 1.0f);

        [Tooltip("The specular weight pow.")]
        [Range(1.0f, 32.0f)]
        public float m_specularWeightPow = 4.0f;

        [Tooltip("grass color texture")]
        public Texture2D m_grassColorTexture = null;

        [System.Serializable, ReloadGroup]
        public sealed class InternalResource
        {
            [Reload("Shaders/Grass.shader")]
            public Shader m_grassShader = null;
            [Reload("Shaders/ProceduralInstanceFilter.compute")]
            public ComputeShader m_proceduralInstanceFilterCS = null;
        }
        [HideInInspector]
        public InternalResource m_internalResource = null;


        [System.Serializable]
        public class SizeInfo
        {
            [Tooltip("The size.")]
            [Range(0.1f, 10.0f)]
            public float m_size = 2.0f;

            [Tooltip("The min offset size.")]
            [Range(0.0f, 5.0f)]
            public float m_minSizeOffest = 0.2f;

            [Tooltip("The max offset size.")]
            [Range(0.0f, 10.0f)]
            public float m_maxSizeOffest = 0.5f;
        }

        [System.Serializable]
        public class WindParameters
        {
            [Tooltip("The wind intensity ratio.")]
            [Range(0.0f, 1.0f)]
            public float m_windIntensityRatio = 0.3f;

            [Tooltip("The wind intensity ratio.")]
            [Range(0.0f, 360.0f)]
            public float m_windYawAngle = 0.0f;

            [Tooltip("The wind normal weight.")]
            [Range(0.0f, 1.0f)]
            public float m_windNormalWeight = 0.25f;

            [Tooltip("The first wind info.")]
            public WindInfo m_windInfoA = new WindInfo(2.0f, 2.0f,
                new Vector2(0.1f, 0.1f), new Vector2(0.5f, 0.5f));

            [Tooltip("The second wind info.")]
            public WindInfo m_windInfoB = new WindInfo(0.25f, 4.0f,
                new Vector2(0.37f, 3.0f), new Vector2(0.5f, 0.5f));

            [Tooltip("The third wind info.")]
            public WindInfo m_windInfoC = new WindInfo(0.125f, 4.0f,
                new Vector2(0.77f, 3.0f), new Vector2(0.5f, 0.5f));
        }


        [System.Serializable]
        public class TextureWindParameters
        {
            [Range(0.001f, 1.0f)]
            public float m_windWorldScale = 0.01f;
            [ColorUsage(false, false)]
            public Color m_windWaveColor = new Color(1, 0.9739528f, 0.8915094f, 1);
            [Range(0.0f, 2.0f)]
            public float m_windWaveBrightness = 0.5f;
            [Range(0.0f, 360.0f)]
            public float m_windYawAngle = 270.0f;
            [Range(0.01f, 1.0f)]
            public float m_windSpeed = 0.04f;

            [Reload("Textures/WindNoise.png")]
            public Texture2D m_windWaveMap = null;
            public Vector2 m_windWaveMapTile = new Vector2(1.5f, 5.0f);
            [Reload("Textures/WindNormal.png")]
            public Texture2D m_waveNormalMap = null;
            public Vector2 m_waveNormalMapTile = new Vector2(3.0f, 2.0f);
            [Range(0.001f, 2.0f)]
            public float m_windWaveDetailBumpScale = 0.001f;
        }



        [System.Serializable]
        public class WindInfo
        {
            [Tooltip("The wind intensity.")]
            [Range(0.0f, 100.0f)]
            public float m_windIntensity;

            [Tooltip("The wind frequency.")]
            [Range(0.0f, 10.0f)]
            public float m_windFrequency;

            [Tooltip("The wind tiling.")]
            public Vector2 m_windTiling;

            [Tooltip("The wind wrap.")]
            public Vector2 m_windWrap;

            public WindInfo(float windIntensity, float windFrequency,
                Vector2 windTiling, Vector2 windWrap)
            {
                m_windIntensity = windIntensity;
                m_windFrequency = windFrequency;
                m_windTiling = windTiling;
                m_windWrap = windWrap;
            }
        }
    }
}
