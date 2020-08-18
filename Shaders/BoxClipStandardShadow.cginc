// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see unity_mit_license.txt)

#ifndef BOX_CLIP_STANDARD_SHADOW_CGINC_
#define BOX_CLIP_STANDARD_SHADOW_CGINC_

// NOTE: had to split shadow functions into separate file,
// otherwise compiler gives trouble with LIGHTING_COORDS macro (in UnityStandardCore.cginc)

// No support for Shadowed Metallic or Smoothness
// No support for Shadowed Parallaxmap

#include "UnityCG.cginc"
#include "UnityShaderVariables.cginc"

#if (defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON))
    #define BOX_USE_DITHER_MASK 1
#endif

// Need to output UVs in shadow caster, since we need to sample texture and do clip/dithering based on it
#if defined(_ALPHATEST_ON) || defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON)
#define BOX_USE_SHADOW_UVS 1
#endif

// Has a non-empty shadow caster output struct (it's an error to have empty structs on some platforms...)
// #if !defined(V2F_SHADOW_CASTER_NOPOS_IS_EMPTY) || defined(UNITY_STANDARD_USE_SHADOW_UVS)
#define BOX_USE_SHADOW_OUTPUT_STRUCT 1
// #endif

#ifdef UNITY_STEREO_INSTANCING_ENABLED
#define BOX_USE_STEREO_SHADOW_OUTPUT_STRUCT 1
#endif


half4       _Color;
half        _Cutoff;
sampler2D   _MainTex;
float4      _MainTex_ST;
#ifdef BOX_USE_DITHER_MASK
sampler3D   _DitherMaskLOD;
#endif

struct VertexInput
{
    float4 vertex   : POSITION;
    float3 normal   : NORMAL;
    float2 uv0      : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

#ifdef BOX_USE_SHADOW_OUTPUT_STRUCT
struct BoxVertexOutputShadowCaster
{
    V2F_SHADOW_CASTER_NOPOS
    #if defined(BOX_USE_SHADOW_UVS)
        float2 tex : TEXCOORD1;
    #endif
};
#endif

#ifdef BOX_USE_STEREO_SHADOW_OUTPUT_STRUCT
struct BoxVertexOutputStereoShadowCaster
{
    UNITY_VERTEX_OUTPUT_STEREO
};
#endif

struct BoxVertShadowCasterStruct {
    float4 opos : SV_POSITION;
    #ifdef BOX_USE_SHADOW_OUTPUT_STRUCT
    BoxVertexOutputShadowCaster o;
    #endif
    #ifdef BOX_USE_STEREO_SHADOW_OUTPUT_STRUCT
    BoxVertexOutputStereoShadowCaster os;
    #endif
    float4 localPos : TEXCOORD2;
};

// We have to do these dances of outputting SV_POSITION separately from the vertex shader,
// and inputting VPOS in the pixel shader, since they both map to "POSITION" semantic on
// some platforms, and then things don't go well.


BoxVertShadowCasterStruct boxVert (VertexInput v)
{
    BoxVertShadowCasterStruct ostruct = (BoxVertShadowCasterStruct)0;
    UNITY_SETUP_INSTANCE_ID(v);
    ostruct.localPos = v.vertex;
    float4 tangent = 0;
    boxPreprocessVertex(v.vertex, v.normal, tangent);
    #ifdef BOX_USE_STEREO_SHADOW_OUTPUT_STRUCT
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(ostruct.os);
    #endif
    TRANSFER_SHADOW_CASTER_NOPOS(ostruct.o,ostruct.opos)
    #if defined(BOX_USE_SHADOW_OUTPUT_STRUCT)
    #if defined(BOX_USE_SHADOW_UVS)
        ostruct.o.tex = TRANSFORM_TEX(v.uv0, _MainTex);
    #endif
    #endif
    return ostruct;
}

half4 boxFrag (UNITY_POSITION(vpos)
#ifdef BOX_USE_SHADOW_OUTPUT_STRUCT
    , BoxVertexOutputShadowCaster i
#endif
    , in float4 localPos : TEXCOORD2
) : SV_Target
{
    boxConditionalClipLocal(localPos);
    #if defined(BOX_USE_SHADOW_UVS)
        #if defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A)
            half alpha = _Color.a;
        #else
            half alpha = tex2D(_MainTex, i.tex.xy).a * _Color.a;
        #endif
        #if defined(_ALPHATEST_ON)
            clip (alpha - _Cutoff);
        #endif
        #if defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON)
            #if defined(BOX_USE_DITHER_MASK)
                // Use dither mask for alpha blended shadows, based on pixel position xy
                // and alpha level. Our dither texture is 4x4x16.
                #ifdef LOD_FADE_CROSSFADE
                    #define _LOD_FADE_ON_ALPHA
                    alpha *= unity_LODFade.y;
                #endif
                half alphaRef = tex3D(_DitherMaskLOD, float3(vpos.xy*0.25,alpha*0.9375)).a;
                clip (alphaRef - 0.01);
            #else
                clip (alpha - _Cutoff);
            #endif
        #endif
    #endif // #if defined(UNITY_STANDARD_USE_SHADOW_UVS)

    #ifdef LOD_FADE_CROSSFADE
        #ifdef _LOD_FADE_ON_ALPHA
            #undef _LOD_FADE_ON_ALPHA
        #else
            UnityApplyDitherCrossFade(vpos.xy);
        #endif
    #endif

    SHADOW_CASTER_FRAGMENT(i)
}

BOXCLIP_GEOM_SHADER_LOCALPOS(BoxVertShadowCasterStruct, i.localPos)

#endif // BOX_CLIP_STANDARD_SHADOW_CGINC_
