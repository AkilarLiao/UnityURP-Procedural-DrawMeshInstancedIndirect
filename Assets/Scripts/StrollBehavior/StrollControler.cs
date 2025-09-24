/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/24
/// Desc:
/// </summary>

using UnityEngine;

namespace SB.ProceduralGrass
{
    public class StrollControler : MonoBehaviour, IStrollMovement
    {
        private void Start()
        {
            m_selfTransform = transform;
            m_strollMovement.ReInitialize(this, m_selfTransform.position);
        }

        private void Update()
        {   
            m_strollMovement.ProcessMovement();
        }

        float IStrollMovement.GetMoveSpeed()
        {
            return m_moveSpeed;
        }
        float IStrollMovement.GetScopeRadius()
        {
            return m_scopeRadius;
        }
        Vector3 IStrollMovement.GetPosition()
        {
            return m_selfTransform.position;
        }
        void IStrollMovement.SetPosition(Vector3 position)
        {
            m_selfTransform.position = position;
        }
        void IStrollMovement.SetYawAngle(float yawAngle)
        {
            Vector3 eulerAngles = m_selfTransform.eulerAngles;
            m_selfTransform.eulerAngles = new Vector3(eulerAngles.x, yawAngle, eulerAngles.z);
        }
        [SerializeField]
        [Range(0.01f, 50.0f)]
        private float m_moveSpeed = 5.0f;
        
        [SerializeField]
        [Range(1.0f, 50.0f)]
        private float m_scopeRadius = 20.0f;

        private StrollMovement m_strollMovement = new StrollMovement();
        private Transform m_selfTransform = null;
    }
}