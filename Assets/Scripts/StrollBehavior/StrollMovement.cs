/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/24
/// Desc:
/// </summary>

using UnityEngine;

namespace SB.ProceduralGrass
{
    public interface IStrollMovement
    {
        float GetMoveSpeed();
        float GetScopeRadius();
        Vector3 GetPosition();
        void SetPosition(Vector3 position);
        void SetYawAngle(float yawAngle);
    }
    public class StrollMovement
    {
        public bool ReInitialize(IStrollMovement theInterface, Vector3 origion)
        {
            m_interface = theInterface;
            m_origion = origion;
            ProcessNextMovePoint();
            return true;
        }
        public void ProcessMovement()
        {
            if (m_interface == null)
                return;

            var deltaYawAngle = m_destYawAngle - m_sourceYawAngle;
            if (deltaYawAngle != 0.0f)
            {   
                m_sourceYawAngle += Mathf.Clamp01(Time.smoothDeltaTime * m_rotateSpeed) * 
                    deltaYawAngle;
                if (Mathf.Abs(m_destYawAngle - m_sourceYawAngle) <= 0.001f)
                    m_sourceYawAngle = m_destYawAngle;
                m_interface.SetYawAngle(m_sourceYawAngle);
            }

            if (m_restMoveTime > 0.0)
            {
                m_restMoveTime = Mathf.Max(m_restMoveTime - Time.deltaTime, 0.0f);
                float moveRatio = 1.0f - m_restMoveTime / m_moveTime;
                m_interface.SetPosition(m_startMovePoint + moveRatio * m_delta);
            }
            if (m_restMoveTime <= 0.0f)
                ProcessNextMovePoint();
        }
        private void ProcessNextMovePoint()
        {
            if (m_interface == null)
                return;

            Vector2 rendomVector = Random.insideUnitCircle *
                m_interface.GetScopeRadius();

            m_destPoint = new Vector3(m_origion.x +  rendomVector.x, 0.0f,
                m_origion.z + rendomVector.y);
            m_startMovePoint = m_interface.GetPosition();
            m_delta = m_destPoint - m_startMovePoint;
            m_delta.y = 0.0f;

            if (m_delta.magnitude <= 0.0f)
                return;

            m_destYawAngle = Mathf.Rad2Deg* Mathf.Atan2(m_delta.x, m_delta.z);
            m_restMoveTime = m_moveTime = m_delta.magnitude / m_interface.GetMoveSpeed();
        }

        private IStrollMovement m_interface = null;

        private Vector3 m_origion;        
        public float m_rotateSpeed = 5.0f;

        private Vector3 m_startMovePoint;
        private Vector3 m_destPoint;
        private Vector3 m_delta;
        private float m_restMoveTime = 0.0f;
        private float m_moveTime = 0.0f;

        private float m_destYawAngle = 0.0f;
        private float m_sourceYawAngle = 0.0f;
    }
}