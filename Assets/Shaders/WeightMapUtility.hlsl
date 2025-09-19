/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/19
/// Desc:
/// </summary>

#ifndef WEIGHT_MAP_UTILITY_INCLUDED
#define WEIGHT_MAP_UTILITY_INCLUDED

//x,y is min
//z,w is max
float4 _WorldMinMax;

half2 GetWorldUV(in float2 worldPosition2D)
{
	float2 clmapPosition2D = clamp(worldPosition2D, _WorldMinMax.xy, _WorldMinMax.zw);
	return half2((clmapPosition2D.x - _WorldMinMax.x) / (_WorldMinMax.z - _WorldMinMax.x),
		(clmapPosition2D.y - _WorldMinMax.y) / (_WorldMinMax.w - _WorldMinMax.y));
}

#endif //WEIGHT_MAP_UTILITY_INCLUDED