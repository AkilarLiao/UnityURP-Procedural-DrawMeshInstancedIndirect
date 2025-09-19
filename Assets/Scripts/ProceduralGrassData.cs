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
            // 建立 ScriptableObject 實例
            ProceduralGrassData asset = ScriptableObject.CreateInstance<ProceduralGrassData>();

            // 設定儲存路徑
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

            // 建立 asset
            UnityEditor.AssetDatabase.CreateAsset(asset, assetPathAndName);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();

            // 選取新建立的 asset
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

        [System.Serializable, ReloadGroup]
        public sealed class InternalResource
        {
            [Reload("Shaders/Grass.shader")]
            public Shader m_grassShader = null;
            [Reload("Shaders/ProceduralInstanceFilter.compute")]
            public ComputeShader m_proceduralInstanceFilterCS = null;
        }
        //[HideInInspector]
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
    }
}
