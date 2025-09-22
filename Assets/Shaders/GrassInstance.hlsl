/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/19
/// Desc:
/// </summary>

#ifndef GRASS_INSTANCE_INCLUDED
#define GRASS_INSTANCE_INCLUDED

struct GrassInstanceData
{
    float2 position2D;
    half2 sizeFactor;    
    half yawSin;
    half yawCos;
    half wind;
};

float _MaxViewSquareDistance;



#endif //GRASS_INSTANCE_INCLUDED