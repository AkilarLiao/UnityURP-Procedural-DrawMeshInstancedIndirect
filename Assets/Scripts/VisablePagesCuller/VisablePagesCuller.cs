/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/19
/// Desc:
/// </summary>

using System.Collections.Generic;
using UnityEngine;

namespace SB
{
    public class VisableCellsCuller
    {
        public BoundsOctree<uint> GetBoundsOctree()
        {
            return m_boundsOctTree;
        }
        public List<uint> GetVisibleCellIndices()
        { 
            return m_visibleCellIndices;
        }
        public void ProcessCulling(Camera camera,
            in Vector2 pageSize, in Rect worldRect,
            in Vector2 worldMinMaxHeight)
        {
            RefereshOctreeBounds(in pageSize, in worldRect, in worldMinMaxHeight);
            m_visibleCellIndices.Clear();
            if (m_boundsOctTree != null)
            {
                m_boundsOctTree.GetWithinFrustum(camera,
                    m_visibleCellIndices);
            }
        }

        public void RefereshOctreeBounds(in Vector2 pageSize, in Rect worldRect, in Vector2 worldMinMaxHeight)
        {
            if ((m_pageSize == pageSize) &&
                (m_worldRect == worldRect) &&
                (m_worldMinMaxHeight == worldMinMaxHeight))
                return;

            m_worldRect = worldRect;
            m_worldMinMaxHeight = worldMinMaxHeight;
            m_pageSize = pageSize;

            Vector2 worldSizeFactor = m_worldRect.size;
            float worldSize = Mathf.Max(worldSizeFactor.x,
                worldSizeFactor.y);
            if (worldSize <= 0.0f)
                return;

            var center = m_worldRect.center;
            var position = new Vector3(center.x, 0.0f, center.y);
            m_boundsOctTree = new BoundsOctree<uint>(worldSize,
                position, System.Math.Max(m_pageSize.x,
                m_pageSize.y), 1.25f);
            var worldMin = m_worldRect.min;

            var pageRowCount = (uint)Mathf.CeilToInt(
                m_worldRect.width / m_pageSize.x);

            var pageColumnCount = (uint)Mathf.CeilToInt(
                m_worldRect.height / pageSize.y);

            for (uint pageRowIndex = 0; pageRowIndex<pageRowCount;
                ++pageRowIndex)
            {
                for (int pageColumnIndex = 0;
                    pageColumnIndex<pageColumnCount;
                    ++pageColumnIndex)
                {
                    var pageBound = new Bounds();
                    pageBound.min =
                        new Vector3(
                            worldMin.x + pageColumnIndex *
                            m_pageSize.x,
                            worldMinMaxHeight.x,
                            worldMin.y + pageRowIndex *
                            m_pageSize.y);
                    Vector3 pageDestMin = pageBound.min;
                    pageBound.max =
                        new Vector3(
                            pageDestMin.x + m_pageSize.x,
                            worldMinMaxHeight.y,
                            pageDestMin.z + m_pageSize.y);
                    m_boundsOctTree.Add((uint)(pageRowIndex *
                        pageColumnCount + pageColumnIndex),
                        pageBound);
                }
            }
        }

        public void ProcessDrawDebugInfo()
        {
            // Draw node boundaries
            m_boundsOctTree.DrawAllBounds();
            // Draw object boundaries
            m_boundsOctTree.DrawAllObjects();
        }
        private List<uint> m_visibleCellIndices = new List<uint>();
        private BoundsOctree<uint> m_boundsOctTree = null;
        private Rect m_worldRect = new Rect();
        private Vector2 m_worldMinMaxHeight = Vector2.zero;
        private Vector2 m_pageSize = Vector2.zero;
    }
}