/* Copyright (c) 2020 Lyuma <xn.lyuma@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE. */
#ifndef BOX_CLIP_CGINC_
#define BOX_CLIP_CGINC_
#include "UnityCG.cginc"
#include "AutoLight.cginc"
#include "UnityLightingCommon.cginc"

// #pragma warning (error : 3205)
// #pragma warning (error : 3206)

#ifndef _TANGENT_TO_WORLD
#define _TANGENT_TO_WORLD 1
#endif

struct internalBoxQuad {
    float3 a;
    // float3 b;
    // float3 c;
    // float3 d;
    float4 planeNormalZComp;
    float4 ulineAndLength;
    float4 vlineAndLength;
};

#define BOXCLIP_MAKE_QUAD(a,b,d) { \
        (a), \
        float4(normalize(cross((b) - (a), (d) - (a))), 0.01), \
        float4(normalize((b) - (a)), 1. / dot((b) - (a), normalize((b) - (a)))), \
        float4(normalize((d) - (a)), 1. / dot((d) - (a), normalize((d) - (a)))) \
    }



internalBoxQuad boxMakeQuadPoints(float3 a, float3 b, float3 c, float3 d) {
    internalBoxQuad q;
    q.a = a;
    // q.b = (center + xvec - yvec);
    // q.c = (center - xvec - yvec);
    // q.d = (center - xvec + yvec);
    q.planeNormalZComp = float4(normalize(cross(b - a, d - a)), 0.01);
    q.ulineAndLength = float4(normalize(b - a), 1. / dot(b - a, normalize(b - a)));
    q.vlineAndLength = float4(normalize(d - a), 1. / dot(d - a, normalize(d - a)));
    return q;
}

internalBoxQuad boxMakeQuad(float3 center, float3 xvec, float3 yvec) {
    float3 a = (center + xvec + yvec);
    float3 b = (center + xvec - yvec);
    float3 c = (center - xvec - yvec);
    float3 d = (center - xvec + yvec);
    return boxMakeQuadPoints(a, b, c, d);
}


// Namespacing Conventions:
// MACROS: BOXCLIP_UNDERSCORE_SEPARATED
// UNIFORMS: _BoxClipCamelCase (**only if unconfigured**)
// STATIC VARS: boxCamelCase
// PRIVATE FUNCTIONS: internalBoxCamelCase
// PUBLIC FUNCTIONS: boxCamelCase

#ifdef BOXCLIP_CONFIGURED
#define BOXCLIP_FOR_LOOP_UNROLL UNITY_UNROLL

// Baked shader will define these
DECLARE_BOXCLIP_ClipShow_ARRAY
DECLARE_BOXCLIP_ClipHide_ARRAY
DECLARE_BOXCLIP_ShowVolume_ARRAY
DECLARE_BOXCLIP_HideVolume_ARRAY
DECLARE_BOXCLIP_ShowCameraWithin_ARRAY
DECLARE_BOXCLIP_HideCameraWithin_ARRAY
DECLARE_BOXCLIP_ZCompress_ARRAY

#else // BOXCLIP_CONFIGURED

uniform float _BoxClipScale;
uniform float _BoxClipAllowInFront;
#define BOXCLIP_SCALE _BoxClipScale
#define BOXCLIP_ALLOW_IN_FRONT _BoxClipAllowInFront

#define BOXCLIP_RECT_UNIFORMS(quadlist, num) \
    uniform float4 _BoxClipUniform ## quadlist [4 * num]; \

#define BOXCLIP_GET_RECT_QUAD(quadlist, num) { \
        _BoxClipUniform ## quadlist [num * 4 + 0].xyz, \
        _BoxClipUniform ## quadlist [num * 4 + 1], \
        _BoxClipUniform ## quadlist [num * 4 + 2], \
        _BoxClipUniform ## quadlist [num * 4 + 3] }

    // uniform float _BoxClipCount ## quadlist; \
    // static uint boxQuad_ ## quadlist ## _Count = (uint)_BoxClipCount ## quadlist; \

/*
    static uint boxQuad_ ## quadlist ## _Count = (\
        _BoxClipUniform ## quadlist[0].w + \
        _BoxClipUniform ## quadlist[4].w + \
        _BoxClipUniform ## quadlist[8].w + \
        _BoxClipUniform ## quadlist[12].w + \
        _BoxClipUniform ## quadlist[16].w + \
        _BoxClipUniform ## quadlist[20].w + \
        _BoxClipUniform ## quadlist[24].w + \
        _BoxClipUniform ## quadlist[28].w + \
        _BoxClipUniform ## quadlist[32].w + \
        _BoxClipUniform ## quadlist[36].w + \
        _BoxClipUniform ## quadlist[40].w + \
        _BoxClipUniform ## quadlist[44].w + \
        _BoxClipUniform ## quadlist[48].w + \
        _BoxClipUniform ## quadlist[52].w + \
        _BoxClipUniform ## quadlist[56].w + \
        _BoxClipUniform ## quadlist[60].w \
        ); \
 */


#define DECLARE_QUAD_ARRAY(quadlist) \
    uniform float4 _BoxClipUniform ## quadlist [4 * 16]; \
    uniform float _BoxClipCount ## quadlist; \
    static uint boxQuad_ ## quadlist ## _Count = (uint)_BoxClipCount ## quadlist; \
    static internalBoxQuad boxQuad_ ## quadlist ## _Quads[16] = { \
        BOXCLIP_GET_RECT_QUAD(quadlist,0), \
        BOXCLIP_GET_RECT_QUAD(quadlist,1), \
        BOXCLIP_GET_RECT_QUAD(quadlist,2), \
        BOXCLIP_GET_RECT_QUAD(quadlist,3), \
        BOXCLIP_GET_RECT_QUAD(quadlist,4), \
        BOXCLIP_GET_RECT_QUAD(quadlist,5), \
        BOXCLIP_GET_RECT_QUAD(quadlist,6), \
        BOXCLIP_GET_RECT_QUAD(quadlist,7), \
        BOXCLIP_GET_RECT_QUAD(quadlist,8), \
        BOXCLIP_GET_RECT_QUAD(quadlist,9), \
        BOXCLIP_GET_RECT_QUAD(quadlist,10), \
        BOXCLIP_GET_RECT_QUAD(quadlist,11), \
        BOXCLIP_GET_RECT_QUAD(quadlist,12), \
        BOXCLIP_GET_RECT_QUAD(quadlist,13), \
        BOXCLIP_GET_RECT_QUAD(quadlist,14), \
        BOXCLIP_GET_RECT_QUAD(quadlist,15) \
    };

DECLARE_QUAD_ARRAY(ZCompress);
DECLARE_QUAD_ARRAY(ClipShow);
DECLARE_QUAD_ARRAY(ClipHide);
DECLARE_QUAD_ARRAY(ShowVolume);
DECLARE_QUAD_ARRAY(HideVolume);
DECLARE_QUAD_ARRAY(ShowCameraWithin);
DECLARE_QUAD_ARRAY(HideCameraWithin);

#endif // !BOXCLIP_CONFIGURED

#ifndef BOXCLIP_FOR_LOOP_UNROLL
#ifdef BOXCLIP_CONFIGURED
#define BOXCLIP_FOR_LOOP_UNROLL UNITY_UNROLL
#else
#define BOXCLIP_FOR_LOOP_UNROLL UNITY_LOOP
#endif
#endif

#ifndef BOXCLIP_SCALE
#define BOXCLIP_SCALE 1
#endif
#ifndef BOXCLIP_ALLOW_IN_FRONT
#define BOXCLIP_ALLOW_IN_FRONT 0
#endif

#define BOXCLIP_FOR_EACH_QUAD(quadlist, statements) \
    {BOXCLIP_FOR_LOOP_UNROLL \
    for (uint i = 0; i < boxQuad_ ## quadlist ## _Count; i++) { \
        internalBoxQuad q = boxQuad_ ## quadlist ## _Quads[i]; \
        statements \
    }}



// static float4 _Center = float4(0,0,0,0);
// static float4 _Extent = float4(4.00 * scaleDist,2.25 * scaleDist,0.0001 * scaleDist,0);

static float3 boxObjectSpaceCameraPos = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
#ifdef USING_STEREO_MATRICES
static float3 boxObjectSpaceCameraPosStereo = mul(unity_WorldToObject, float4(lerp(unity_StereoWorldSpaceCameraPos[0], unity_StereoWorldSpaceCameraPos[1], 0.5), 1)).xyz;
#else
static float3 boxObjectSpaceCameraPosStereo = boxObjectSpaceCameraPos;
#endif

void boxPreprocessVertex(inout float4 vert, inout float3 norm, inout float4 tang) {
    vert.xyz *= (BOXCLIP_SCALE > 0 ? BOXCLIP_SCALE : 1);
}
void boxPreprocessVertex(inout float4 vert, inout float3 norm) {
    float4 tangent = 0;
    boxPreprocessVertex(vert, norm, tangent);
}
void boxPreprocessVertex(inout float4 vert) {
    float3 normal = 0;
    boxPreprocessVertex(vert, normal);
}

//flawed -- this algorithm only works if the quad is parallel with the camera view plane.
// because it looks distThresh along u and v... but that's not enough if it's not parallel.
// MAYBE we can correct for this...???

float2 internalBoxCameraNormalAdjust(internalBoxQuad q) {
    /*
    float3 uline = q.ulineAndLength.xyz;
    float3 vline = q.vlineAndLength.xyz;
    return float2(
        sqrt(1 - dot(1, normalize(mul((float3x3)unity_WorldToCamera, mul((float3x3)unity_ObjectToWorld, uline))).zz)),
        sqrt(1 - dot(1, normalize(mul((float3x3)unity_WorldToCamera, mul((float3x3)unity_ObjectToWorld, vline))).zz)));
        */

    return abs(normalize(mul((float3x3)unity_WorldToCamera, mul((float3x3)unity_ObjectToWorld, q.planeNormalZComp.xyz))).z);
}

bool internalBoxFrustumCheck(float3 p, internalBoxQuad q, float distThresh) {
    float3 v = boxObjectSpaceCameraPos;
    float3 planeNormal = q.planeNormalZComp.xyz;
    float3 uline = q.ulineAndLength.xyz;
    float3 vline = q.vlineAndLength.xyz;

    if (dot(planeNormal, v - q.a) < 0) {
        return false;
    }

    float ratio = (dot(q.a - v, planeNormal) / dot(p - v, planeNormal));
    float3 planeIntercept = lerp(v, p, ratio);

    if (dot(planeNormal, p - q.a) > distThresh) {
        return false;
    }

    float3 corners[4];
    corners[0] = q.a;
    corners[1] = q.a + q.ulineAndLength.xyz / q.ulineAndLength.w;
    corners[2] = corners[1] + q.vlineAndLength.xyz / q.vlineAndLength.w;
    corners[3] = q.a + q.vlineAndLength.xyz / q.vlineAndLength.w;

    float2 normUV = float2(
        (dot(planeIntercept - q.a, uline) * q.ulineAndLength.w),
        (dot(planeIntercept - q.a, vline) * q.vlineAndLength.w));
    bool2 insideSide = abs(normUV - 0.5) < 0.5;
    bool isInside = all(insideSide);
    bool isFrustum = any(insideSide);

    float planeOrLineDist;

    uint2 planeIdx = (abs(normUV.x - 0.5) < abs(normUV.y - 0.5)) ? (normUV.y < 0.5 ? uint2(0,1) : uint2(2,3)) : (normUV.x < 0.5 ? uint2(3,0) : uint2(1,2));

    float3 frustumPlaneNormal = cross(v - corners[planeIdx.x], v - corners[planeIdx.y]);
    planeOrLineDist = dot(normalize(frustumPlaneNormal), p - corners[planeIdx.x]);

    uint2 planeIdxY = (abs(normUV.x - 0.5) < abs(normUV.y - 0.5)) ? (normUV.x < 0.5 ? uint2(3,0) : uint2(1,2)) : (normUV.y < 0.5 ? uint2(0,1) : uint2(2,3));

    float3 frustumPlaneNormalY = cross(v - corners[planeIdxY.x], v - corners[planeIdxY.y]);
    planeOrLineDist = max(planeOrLineDist, dot(normalize(frustumPlaneNormalY), p - corners[planeIdxY.x]));

    /*
    if (isFrustum) {
        uint2 planeIdx = insideSide.x && (!insideSide.y || abs(normUV.x - 0.5) < abs(normUV.y - 0.5)) ? (normUV.y < 0.5 ? uint2(0,1) : uint2(2,3)) : (normUV.x < 0.5 ? uint2(3,0) : uint2(1,2));

        float3 frustumPlaneNormal = cross(v - corners[planeIdx.x], v - corners[planeIdx.y]);
        planeOrLineDist = dot(normalize(frustumPlaneNormal), p - corners[planeIdx.x]);
    } else {
        uint lineIdx = normUV.y < 0.5 ? (normUV.x < 0.5 ? 0 : 1) : (normUV.x < 0.5 ? 3 : 2);
        float3 lineVector = normalize(v - corners[lineIdx]);

        planeOrLineDist = length(cross(p - v, lineVector));
    }
    */
    return planeOrLineDist < distThresh;
    /*
    float3 frustumPlaneNormalAB = cross(v - a, v - b);
    float3 frustumPlaneNormalBC = cross(v - b, v - c);
    float3 frustumPlaneNormalCD = cross(v - c, v - d);
    float3 frustumPlaneNormalDA = cross(v - d, v - a);

    float distanceP1 = dot(normalize(frustumPlaneNormalAB), p - a);
    float distanceP2 = dot(normalize(frustumPlaneNormalBC), p - b);
    float distanceP3 = dot(normalize(frustumPlaneNormalCD), p - c);
    float distanceP4 = dot(normalize(frustumPlaneNormalDA), p - d);
    */
/*
    if (dot(planeNormal, v - q.a) < 0) {
            // || (BOXCLIP_ALLOW_IN_FRONT <= 1e-8 && dot(planeNormal, p - q.a) > 0)) {
        return false;
    }

    float2 distToPlane = float2(dot(planeIntercept - q.a, uline), dot(planeIntercept - q.a, vline));
    float4 divisor = float2(q.ulineAndLength.w, q.vlineAndLength.w).xyxy;
    float4 compareUV = float4(1 / divisor.xy - distToPlane, distToPlane);
    float4 compareUVDist = compareUV * divisor >= 1 ? 1e+9 : min(internalBoxCameraNormalAdjust(q).xyxy * compareUV, 0);
    float mindist = min(min(length(compareUVDist.xy), length(compareUVDist.xw)), min(length(compareUVDist.zy), length(compareUVDist.zw)));
    return mindist <= distThresh;
*/
}

bool internalBoxPointNearQuad(float3 p, internalBoxQuad q, float distThresh) {
    float3 v = boxObjectSpaceCameraPos;
    float3 planeNormal = q.planeNormalZComp.xyz;
    float3 uline = q.ulineAndLength.xyz;
    float3 vline = q.vlineAndLength.xyz;

    if (dot(planeNormal, v - q.a) < 0) {
            // || (BOXCLIP_ALLOW_IN_FRONT <= 1e-8 && dot(planeNormal, p - q.a) > 0)) {
        return false;
    }

    float ratio = (dot(q.a - v, planeNormal) / dot(p - v, planeNormal));
    float3 planeIntercept = lerp(v, p, ratio);

    float2 distToPlane = float2(dot(planeIntercept - q.a, uline), dot(planeIntercept - q.a, vline));
    float4 divisor = float2(q.ulineAndLength.w, q.vlineAndLength.w).xyxy;
    float4 compareUV = float4(1 / divisor.xy - distToPlane, distToPlane);
    float4 compareUVDist = compareUV * divisor >= 1 ? 1e+9 : min(internalBoxCameraNormalAdjust(q).xyxy * compareUV, 0);
    float mindist = min(min(length(compareUVDist.xy), length(compareUVDist.xw)), min(length(compareUVDist.zy), length(compareUVDist.zw)));
    return mindist <= distThresh;
}

bool internalBoxPointInQuad(float3 p, internalBoxQuad q, inout float3 outp, out float2 uv) {
    float3 v = boxObjectSpaceCameraPos;
    float3 planeNormal = q.planeNormalZComp.xyz;
    float3 uline = q.ulineAndLength.xyz;
    float3 vline = q.vlineAndLength.xyz;

    if (dot(planeNormal, v - q.a) < 0) {
            // || (BOXCLIP_ALLOW_IN_FRONT <= 1e-8 && dot(planeNormal, p - q.a) > 0)) {
        uv = 0;
        return false;
    }

    float ratio = (dot(q.a - v, planeNormal) / dot(p - v, planeNormal));
    float3 planeIntercept = lerp(v, p, ratio);
    outp = planeIntercept;

    uv = 1 - float2(
        (dot(planeIntercept - q.a, uline) * q.ulineAndLength.w),
        (dot(planeIntercept - q.a, vline) * q.vlineAndLength.w));

    // FIXME: BOXCLIP_ALLOW_IN_FRONT used to use maximum over all dimensions. I removed this.
    bool found = all(abs(float3(uv, ratio) - 0.5) < float3(0.5,0.5,0.5 + BOXCLIP_ALLOW_IN_FRONT));
    return found;
}

bool internalBoxPointWithinVolume(float3 p, internalBoxQuad q) {
    float3 relvec = q.a - p;
    float3 planeNormal = q.planeNormalZComp.xyz;
    float3 uline = q.ulineAndLength.xyz;
    float3 vline = q.vlineAndLength.xyz;
    float3 dotAxes = float3(
        dot(relvec, uline),
        dot(relvec, vline),
        dot(relvec, planeNormal)) * float3(
        q.ulineAndLength.w,
        q.vlineAndLength.w,
        1.0/q.planeNormalZComp.w) - float3(-0.5,-0.5,0.0);
    // return (//all(dotAxes >= 0) &&
    //     all(dotAxes * compareLengths <= 1));
    return all(abs(dotAxes) < 0.5);
}

bool internalBoxTriangleIntersectsQuad(float3 p1, float3 p2, float3 p3, internalBoxQuad q) {
    float3 x1;
    float2 x2;
    float3 v = boxObjectSpaceCameraPos;
    float3 planeNormal = q.planeNormalZComp.xyz;
    float3 uline = q.ulineAndLength.xyz;
    float3 vline = q.vlineAndLength.xyz;

    if (dot(planeNormal, v - q.a) < 0) {
            // || (dot(planeNormal, p1 - q.a) > 0 && dot(planeNormal, p2 - q.a) > 0 && dot(planeNormal, p3 - q.a) > 0)) {
        return false;
    }

    float ratio1 = (dot(q.a - v, planeNormal) / dot(p1 - v, planeNormal));
    float3 planeIntercept1 = lerp(v, p1, ratio1);
    float2 uv1 = float2(
        (dot(planeIntercept1 - q.a, uline) * q.ulineAndLength.w),
        (dot(planeIntercept1 - q.a, vline) * q.vlineAndLength.w));

    float ratio2 = (dot(q.a - v, planeNormal) / dot(p2 - v, planeNormal));
    float3 planeIntercept2 = lerp(v, p2, ratio2);
    float2 uv2 = float2(
        (dot(planeIntercept2 - q.a, uline) * q.ulineAndLength.w),
        (dot(planeIntercept2 - q.a, vline) * q.vlineAndLength.w));

    float ratio3 = (dot(q.a - v, planeNormal) / dot(p3 - v, planeNormal));
    float3 planeIntercept3 = lerp(v, p3, ratio3);
    float2 uv3 = float2(
        (dot(planeIntercept3 - q.a, uline) * q.ulineAndLength.w),
        (dot(planeIntercept3 - q.a, vline) * q.vlineAndLength.w));

    float2 s1 = uv2 - uv1;

    float sA = -s1.y/s1.x * (uv1.x) + (uv1.y);
    float tA = (- 1 * (uv1.x)) / (s1.x);
    float sB = (uv1.x) - s1.x/s1.y * (uv1.y);
    float tB = ( 1 * (uv1.y)) / (-s1.y);

    float2 ss1 = uv3 - uv1;
    float ssA = -ss1.y/ss1.x * (uv1.x) + (uv1.y);
    float ttA = (- 1 * (uv1.x)) / (ss1.x);
    float ssB = (uv1.x) - ss1.x/ss1.y * (uv1.y);
    float ttB = ( 1 * (uv1.y)) / (-ss1.y);

    uv1 = (1 - uv1);
    uv2 = (1 - uv2);
    uv3 = (1 - uv3);

    s1 = uv2 - uv1;
    float sC = -s1.y/s1.x * (uv1.x) + (uv1.y);
    float tC = (- 1 * (uv1.x)) / (s1.x);
    float sD = (uv1.x) - s1.x/s1.y * (uv1.y);
    float tD = ( 1 * (uv1.y)) / (-s1.y);

    ss1 = uv3 - uv1;
    float ssC = -ss1.y/ss1.x * (uv1.x) + (uv1.y);
    float ttC = (- 1 * (uv1.x)) / (ss1.x);
    float ssD = (uv1.x) - ss1.x/ss1.y * (uv1.y);
    float ttD = ( 1 * (uv1.y)) / (-ss1.y);

    if (any(abs(float4(sA, sB, sC, sD) - 0.5) <= 0.5 && abs(float4(tA, tB, tC, tD) - 0.5) <= 0.5) ||
            any(abs(float4(ssA, ssB, ssC, ssD) - 0.5) <= 0.5 && abs(float4(ttA, ttB, ttC, ttD) - 0.5) <= 0.5)) {
        return true;
    }

// FIXME(lyuma): Some of these conditions were safely removed in selphina
    if (all(abs(float3(uv1, ratio1) - 0.5) <= 0.5) ||
            all(abs(float3(uv2, ratio2) - 0.5) <= 0.5) ||
            all(abs(float3(uv3, ratio3) - 0.5) <= 0.5)) {
        return true;
    }

    if (any(float3(dot(p1, p1), dot(p2, p2), dot(p3, p3)) < 2.5)) {
        return true;
    }
    return false;
}

bool boxConditionalClipAll() {
//    if (withinBox) {
        return true;
//    }
//    return boxScaledCameraPos.x > 1.0 || boxScaledCameraPos.z > 1.0;
//    return abs(boxScaledCameraPos.z) > 1.0;
}



float4 boxZCompressClip(float4 vert) {
    float4 opos = UnityObjectToClipPos(vert);
    float2 thisuv;
    float3 thispos = vert.xyz;
    float4 outposZComp = float4(vert.xyz, 1);
    BOXCLIP_FOR_EACH_QUAD(ZCompress, {
        outposZComp = internalBoxPointInQuad(vert.xyz, q, thispos, thisuv) ?
                float4(thispos, q.planeNormalZComp.w) : outposZComp;
    });
    float4 xopos = UnityObjectToClipPos(outposZComp.xyz);
    opos.z = lerp(opos.z, xopos.z * opos.w / xopos.w, 1 - saturate(outposZComp.w));
    return opos;
}

float4 boxZCompressClip(float3 vert) {
    return boxZCompressClip(float4(vert, 1));
}

#ifdef UnityObjectToClipPos
#undef UnityObjectToClipPos
#endif
#define UnityObjectToClipPos boxZCompressClip


bool boxConditionCheck(float3 origvertpos) {
    bool found = true;
    float3 thispos = boxObjectSpaceCameraPos;
    float2 thisuv = 0;
    BOXCLIP_FOR_EACH_QUAD(ShowVolume, {
        found = false;
    });
    BOXCLIP_FOR_EACH_QUAD(ShowCameraWithin, {
        found = false;
    });
    BOXCLIP_FOR_EACH_QUAD(ClipShow, {
        found = false;
    });
    BOXCLIP_FOR_EACH_QUAD(ShowCameraWithin, {
        if (internalBoxPointWithinVolume(boxObjectSpaceCameraPosStereo, q)) {
            found = true;
        }
    });
    BOXCLIP_FOR_EACH_QUAD(HideCameraWithin, {
        if (internalBoxPointWithinVolume(boxObjectSpaceCameraPosStereo, q)) {
            found = false;
        }
    });
    BOXCLIP_FOR_EACH_QUAD(ShowVolume, {
        if (internalBoxPointWithinVolume(origvertpos.xyz, q)) {
            found = true;
        }
    });
    BOXCLIP_FOR_EACH_QUAD(HideVolume, {
        if (internalBoxPointWithinVolume(origvertpos.xyz, q)) {
            found = false;
        }
    });
    BOXCLIP_FOR_EACH_QUAD(ClipShow, {
        if (internalBoxPointInQuad(origvertpos.xyz, q, thispos, thisuv)) {
            found = true;
        }
    });
    BOXCLIP_FOR_EACH_QUAD(ClipHide, {
        if (internalBoxPointInQuad(origvertpos.xyz, q, thispos, thisuv)) {
            found = false;
        }
    });
    return found;
}

void boxConditionalClipLocal(float3 localPos) {
    clip(boxConditionCheck(localPos) ? 0 : -1);
}

void boxConditionalClipLocal(float4 localPos) {
    clip(boxConditionCheck(localPos.xyz) ? 0 : -1);
}

void boxConditionalClipWorld(float3 worldPos) {
    clip(boxConditionCheck(mul(unity_WorldToObject, float4(worldPos, 1)).xyz) ? 0 : -1);
}

void boxConditionalClipWorld(float4 worldPos) {
    clip(boxConditionCheck(mul(unity_WorldToObject, worldPos).xyz) ? 0 : -1);
}

bool geometry_check(float3 v1, float3 v2, float3 v3) {
    float3 thispos = 0;
    float2 thisuv = 0;
    bool found = true;
    BOXCLIP_FOR_EACH_QUAD(ShowVolume, {
        found = false;
    });
    BOXCLIP_FOR_EACH_QUAD(ShowCameraWithin, {
        found = false;
    });
    BOXCLIP_FOR_EACH_QUAD(ClipShow, {
        found = false;
    });
    BOXCLIP_FOR_EACH_QUAD(ShowCameraWithin, {
        if (internalBoxPointWithinVolume(boxObjectSpaceCameraPosStereo, q)) {
            found = true;
        }
    });
    BOXCLIP_FOR_EACH_QUAD(HideCameraWithin, {
        if (internalBoxPointWithinVolume(boxObjectSpaceCameraPosStereo, q)) {
            found = false;
        }
    });
    BOXCLIP_FOR_EACH_QUAD(ShowVolume, {
        if (internalBoxPointWithinVolume(v1.xyz, q) || internalBoxPointWithinVolume(v2.xyz, q) || internalBoxPointWithinVolume(v3.xyz, q)) {
            found = true;
        }
    });
    BOXCLIP_FOR_EACH_QUAD(HideVolume, {
        if (internalBoxPointWithinVolume(v1.xyz, q) && internalBoxPointWithinVolume(v2.xyz, q) && internalBoxPointWithinVolume(v3.xyz, q)) {
            found = false;
        }
    });
    BOXCLIP_FOR_EACH_QUAD(ClipShow, {
        if (internalBoxTriangleIntersectsQuad(v1.xyz, v2.xyz, v3.xyz, q)) {
            found = true;
        }
    });
    BOXCLIP_FOR_EACH_QUAD(ClipHide, {
        if (internalBoxPointInQuad(v1.xyz, q, thispos, thisuv) &&
                internalBoxPointInQuad(v2.xyz, q, thispos, thisuv) &&
                internalBoxPointInQuad(v3.xyz, q, thispos, thisuv)) {
            found = true;
        }
    });
    return found;
}

bool modified_vert_check(float3 localPos, float length) {
    bool found = true;
    BOXCLIP_FOR_EACH_QUAD(ShowVolume, {
        found = false;
    });
    BOXCLIP_FOR_EACH_QUAD(ShowCameraWithin, {
        found = false;
    });
    BOXCLIP_FOR_EACH_QUAD(ClipShow, {
        found = false;
    });
    BOXCLIP_FOR_EACH_QUAD(ShowCameraWithin, {
        if (internalBoxPointWithinVolume(boxObjectSpaceCameraPosStereo, q)) {
            found = true;
        }
    });
    BOXCLIP_FOR_EACH_QUAD(HideCameraWithin, {
        if (internalBoxPointWithinVolume(boxObjectSpaceCameraPosStereo, q)) {
            found = false;
        }
    });
    BOXCLIP_FOR_EACH_QUAD(ShowVolume, {
        if (internalBoxPointWithinVolume(localPos, q)) {
            found = true;
        }
    });
    BOXCLIP_FOR_EACH_QUAD(HideVolume, {
        if (internalBoxPointWithinVolume(localPos, q)) {
            found = false;
        }
    });
    BOXCLIP_FOR_EACH_QUAD(ClipShow, {
        if (internalBoxFrustumCheck(localPos, q, length)) {
            found = true;
        }
    });
    float3 thispos = 0;
    float2 thisuv = 0;
/*
        if (internalBoxPointInQuad(localPos, q, thispos, thisuv)) {
            float2 cameraNormalAdjust = internalBoxCameraNormalAdjust(q);
            if (all(float4(thisuv.xy, 1 - thisuv.xy) * cameraNormalAdjust.xyxy > float2(length * q.ulineAndLength.w, length * q.vlineAndLength.w).xyxy)) {
                found = false;
            }
        }
*/
    BOXCLIP_FOR_EACH_QUAD(ClipHide, {
        if (internalBoxFrustumCheck(localPos, q, -length)) {
            found = false;
        }
    });
    return found;
}

#define BOXCLIP_GEOM_SHADER_LOCALPOS(V2FStruct, localPosVar) \
    [maxvertexcount(3)] \
    void boxGeom(triangle V2FStruct IN[3], inout TriangleStream<V2FStruct> tristream) {\
        float3 localPos[3]; \
        UNITY_UNROLL \
        for (uint ii = 0; ii < 3; ii++) { \
            V2FStruct i = IN[ii]; \
            localPos[ii] = (localPosVar).xyz; \
        } \
        float lengthVert0 = max(distance(localPos[0], localPos[1]),distance(localPos[0], localPos[2])); \
        float lengthVert1 = max(distance(localPos[0], localPos[1]),distance(localPos[1], localPos[2])); \
        float lengthVert2 = max(distance(localPos[0], localPos[2]),distance(localPos[1], localPos[2])); \
        if (geometry_check(localPos[0], localPos[1], localPos[2])) { \
            tristream.Append(IN[0]); \
            tristream.Append(IN[1]); \
            tristream.Append(IN[2]); \
        } \
    }

/*
        if ((modified_vert_check(localPos[0], lengthVert0) && \
            modified_vert_check(localPos[1], lengthVert1) && \
            modified_vert_check(localPos[2], lengthVert2))) {
*/

#define BOXCLIP_GEOM_SHADER_WORLDPOS(V2FStruct, worldPosVar) \
    BOXCLIP_GEOM_SHADER_LOCALPOS(V2FStruct, mul(unity_WorldToObject, float4((worldPosVar).xyz, 1)).xyz)

#if defined(BOXCLIP_NO_NORMAL)
#define BOXCLIP_PREPROCESS_ARGS i.vertex
#elif defined(BOXCLIP_NO_TANGENT)
#define BOXCLIP_PREPROCESS_ARGS i.vertex, i.normal
#else
#define BOXCLIP_PREPROCESS_ARGS i.vertex, i.normal, i.tangent
#endif

#define BOXCLIP_ALL_SHADERS_WORLDPOS(fragName, V2FStruct, vertName, APPDATAStruct, worldPosVar) \
    BOXCLIP_GEOM_SHADER_WORLDPOS(V2FStruct, worldPosVar) \
    V2FStruct boxVert(APPDATAStruct i) { \
        boxPreprocessVertex(BOXCLIP_PREPROCESS_ARGS); \
        return vertName(i); \
    } \
    float4 boxFrag(V2FStruct i) : SV_Target { \
        boxConditionalClipWorld(worldPosVar); \
        return fragName(i); \
    } \

#define BOXCLIP_ALL_SHADERS_LOCALPOS(fragName, V2FStruct, vertName, APPDATAStruct, localPosVar) \
    BOXCLIP_GEOM_SHADER_LOCALPOS(V2FStruct, worldPosVar) \
    V2FStruct boxVert(APPDATAStruct i) { \
        boxPreprocessVertex(BOXCLIP_PREPROCESS_ARGS); \
        return vertName(i); \
    } \
    float4 boxFrag(V2FStruct i) : SV_Target { \
        boxConditionalClipLocal(localPosVar); \
        return fragName(i); \
    } \


#define BOXCLIP_ALL_SHADERS_NOPOS(fragName, V2FStruct, vertName, APPDATAStruct) \
    struct boxV2FWrapper { \
        V2FStruct os; \
	float3 localPos : LOCALPOS; \
    }; \
    BOXCLIP_GEOM_SHADER_LOCALPOS(boxV2FWrapper, i.localPos) \
    boxV2FWrapper boxVert(APPDATAStruct i) { \
        boxPreprocessVertex(BOXCLIP_PREPROCESS_ARGS); \
	boxV2FWrapper o; \
	o.localPos = i.vertex; \
        o.os = vertName(i); \
	return o; \
    } \
    float4 boxFrag(boxV2FWrapper is) : SV_Target { \
        boxConditionalClipLocal(is.localPos); \
        return fragName(is.os); \
    }


#endif
