/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/19
/// Desc:
/// </summary>

#ifndef SIMPLEX_NOISE_INCLUDED
#define SIMPLEX_NOISE_INCLUDED

static const float c_RingSegment = 17.0;
static const float c_RingSize = c_RingSegment * c_RingSegment;
static const float c_PointCount = 41.0;

float3 mod289(float3 x)
{
    return x - floor(x * (1.0 / c_RingSize)) * c_RingSize;
}

float2 mod289(float2 x)
{
    return x - floor(x * (1.0 / c_RingSize)) * c_RingSize;
}

float3 permute(float3 x)
{
    return mod289(((x * 34.0) + 1.0) * x);
}

float GetSimplexNoise(float2 v)
{
    const float4 C = float4(
        0.211324865405187,
        0.366025403784439,
        -0.577350269189626,
        1.0 / c_PointCount);

    // The first corner
    float2 i = floor(v + dot(v, C.yy));
    float2 x0 = v - i + dot(i, C.xx);

    // The other corners
    float2 i1;
    i1 = (x0.x > x0.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);
    float4 x12 = x0.xyxy + C.xxzz;
    x12.xy -= i1;

    // Permutations
    // Avoid truncation effects in permutation
    i = mod289(i);
    float3 p = permute(permute(i.y + float3(0.0, i1.y, 1.0))
        + i.x + float3(0.0, i1.x, 1.0));

    float3 m = max(0.5 - float3(dot(x0, x0), dot(x12.xy, x12.xy), dot(x12.zw, x12.zw)), 0.0);
    m = m * m;
    m = m * m;

    // Gradients: 41 points uniformly over a line, mapped onto a diamond.
    // The ring size 17*17 = 289 is close to a multiple of 41 (41*7 = 287)

    float3 x = 2.0 * frac(p * C.www) - 1.0;
    float3 h = abs(x) - 0.5;
    float3 ox = floor(x + 0.5);
    float3 a0 = x - ox;

    // Normalise gradients implicitly by scaling m
    // Approximation of: m *= inversesqrt( a0*a0 + h*h );
    m *= 1.79284291400159 - 0.85373472095314 * (a0 * a0 + h * h);

    // Compute final noise value at P
    float3 g;
    g.x = a0.x * x0.x + h.x * x0.y;
    g.yz = a0.yz * x12.xz + h.yz * x12.yw;

    return dot(m, g);
}
#endif //SIMPLEX_NOISE_INCLUDED