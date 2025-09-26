/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/26
/// Desc:
/// </summary>

using System.Collections.Generic;
using UnityEngine.Rendering.Universal;

namespace SB.ProceduralGrass
{
    public interface IAddPassInterface
    {
        void OnAddPass(ScriptableRenderer renderer, in RenderingData renderingData);
    }

    public class AddPassRenderFeature : ScriptableRendererFeature
    {
        public static bool AppendAddPassInterfaces(IAddPassInterface theInterface)
        {
            if ((theInterface == null) || (ms_addPassInterfaces.Find(theInterface) != null))
                return false;
            ms_addPassInterfaces.AddLast(theInterface);
            return true;
        }

        public static bool RemoveAddPassInterfaces(IAddPassInterface theInterface)
        {
            return ms_addPassInterfaces.Remove(theInterface);
        }

        public override void Create()
        {   
        }
        
        //當isActive為false的時候，這裡不會進來…
        public override void AddRenderPasses(ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            if (!isActive)
                return;
            var element = ms_addPassInterfaces.GetEnumerator();
            while (element.MoveNext())
                element.Current.OnAddPass(renderer, in renderingData);
            element.Dispose();
        }

        private static LinkedList<IAddPassInterface> ms_addPassInterfaces =
            new LinkedList<IAddPassInterface>();
    }    
}