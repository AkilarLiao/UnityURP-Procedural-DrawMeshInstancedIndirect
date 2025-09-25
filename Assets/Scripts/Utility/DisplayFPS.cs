/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/19
/// Desc:
/// </summary>

using System.Collections;
using UnityEngine;

namespace SB
{
    public class DisplayFPS : MonoBehaviour
    {
        public delegate void AppendExtendStringCB(ref string text);
        public AppendExtendStringCB AppendExtendString { get; set; }

        private void Start()
        {
            var targetFrameRate = m_targetFrameRate;
#if UNITY_EDITOR
            Application.targetFrameRate = -1;
#else
            Application.targetFrameRate = m_targetFrameRate;
#endif
            m_style = new GUIStyle();
            m_style.alignment = m_leftSide ? TextAnchor.UpperLeft :
                TextAnchor.UpperRight;
            m_style.normal.textColor = m_textColor;
            m_restPrintFPSTime = m_printFPSTime;
            //Disabling this lets you skip the GUI layout phase.
            //it' can avoid gc...
            useGUILayout = false;
            StartCoroutine(CollectFPS());
        }
        
        private IEnumerator CollectFPS()
        {
            while (true)
            {
                // Capture frame-per-second
                int lastFrameCount = Time.frameCount;
                float lastTime = Time.realtimeSinceStartup;
                yield return m_waitForSeconds;
                m_timeSpan = Time.realtimeSinceStartup - lastTime;
                int frameCount = Time.frameCount - lastFrameCount;
                m_fps = Mathf.RoundToInt(frameCount / m_timeSpan);
            }
        }

        private void OnGUI()
        {
            if (!m_showDebugInfo)
                return;
            m_restPrintFPSTime -= Time.deltaTime;
            if (m_restPrintFPSTime <= 0.0)
            {
                m_restPrintFPSTime = m_printFPSTime;
                int width = Screen.width, height = Screen.height;
                float offestX = m_leftSide ? m_leftSiedeOffest : 0;
                ms_tempPrintRect = new Rect(offestX, 0,
                    width, height * 2 / 100);
                m_style.fontSize = height * 5 / 100;

                if (m_displayFPS)
                {
                    float msec = (1.0f / (float)m_fps) * 1000.0f;
                    ms_tempFPSText = string.Format("  {0:0.0} ms, ({1} fps)", 
                        msec, m_fps);
                }
                else
                    ms_tempFPSText = "";

                if (!m_onlyShowFPS)
                {
                    //ms_tempFPSText = string.Format("{0}\ngraphicsDeviceType:{1}",
                        //ms_tempFPSText, SystemInfo.graphicsDeviceType);
                    ms_tempFPSText = string.Format("{0}\ngraphicsDeviceName:{1}",
                        ms_tempFPSText, SystemInfo.graphicsDeviceName);
                    //ms_tempFPSText = string.Format("{0}\ngraphicsMultiThreaded:{1}",
                    //    ms_tempFPSText, SystemInfo.graphicsMultiThreaded);
                    //ms_tempFPSText = string.Format("{0}\nsupportsInstancing:{1}",
                    //    ms_tempFPSText, SystemInfo.supportsInstancing);
                    //ms_tempFPSText = string.Format("{0}\nsupportsComputeShaders:{1}",
                    //    ms_tempFPSText, SystemInfo.supportsComputeShaders);
                    //ms_tempFPSText = string.Format("{0}\nmaxComputeBufferInputsCompute:{1}",
                    //    ms_tempFPSText, SystemInfo.maxComputeBufferInputsCompute);
                    //ms_tempFPSText = string.Format("{0}\nmaxComputeBufferInputsVertex:{1}",
                    //    ms_tempFPSText, SystemInfo.maxComputeBufferInputsVertex);
                    //ms_tempFPSText = string.Format("{0}\nmaxComputeBufferInputsFragment:{1}",
                    //    ms_tempFPSText, SystemInfo.maxComputeBufferInputsFragment);
                    //ms_tempFPSText = string.Format("{0}\ngraphicsShaderLevel:{1}",
                    //    ms_tempFPSText, SystemInfo.graphicsShaderLevel);
                    //ms_tempFPSText = string.Format("{0}\ngraphicsDeviceVendorID:{1}",
                    //    ms_tempFPSText, SystemInfo.graphicsDeviceVendorID);
                    //ms_tempFPSText = string.Format("{0}\nmaxComputeWorkGroupSizeX:{1}",
                    //    ms_tempFPSText, SystemInfo.maxComputeWorkGroupSizeX);
                    //ms_tempFPSText = string.Format("{0}\nmaxComputeWorkGroupSize:{1}",
                    //    ms_tempFPSText, SystemInfo.maxComputeWorkGroupSize);
                }
                if (AppendExtendString != null)
                {
                    string extendText = "\n===========================";
                    AppendExtendString(ref extendText);                    
                    ms_tempFPSText = string.Format("{0}{1}",
                        ms_tempFPSText, extendText);                    
                }
            }
            if (ms_tempFPSText == null)
                return;

            GUI.Label(ms_tempPrintRect, ms_tempFPSText, m_style);
        }


        [SerializeField]
        private Color m_textColor = new Color(0.0f, 0.0f, 0.5f, 1.0f);
        [SerializeField]
        private bool m_showDebugInfo = true;
        [SerializeField]
        private bool m_onlyShowFPS = false;
        [SerializeField]
        private bool m_leftSide = false;
        [SerializeField]
        private bool m_displayFPS = true;
        [SerializeField]
        [Range(0.0f, 500.0f)]
        private float m_leftSiedeOffest = 120;
        [SerializeField]        
        private int m_targetFrameRate = 60;

        private GUIStyle m_style = null;
        private float m_printFPSTime = 1.0f;
        private float m_restPrintFPSTime = 0.0f;
        private static string ms_tempFPSText = null;
        private static Rect ms_tempPrintRect;

        private float m_timeSpan = 0.0f;
        private int m_fps = 0;
        private WaitForSeconds m_waitForSeconds = new WaitForSeconds(1.0f);
    }
}