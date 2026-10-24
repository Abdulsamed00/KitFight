// Instanced grass for the arena floor (drawn by GrassField with Graphics.RenderMeshInstanced).
// Blades sway in the wind and rustle around the fighters: GrassField uploads up to 8 "pushers"
// (_KFPushers: xyz = position, w = strength 0..1, scaled by the fighter's speed; current positions plus a
// short fading trail). Near a pusher the blades lean away a little and shake; the shake dies out with the trail.
Shader "KitFight/Grass"
{
    Properties
    {
        _BaseColor ("Root Colour", Color) = (0.16, 0.30, 0.10, 1)
        _TipColor ("Tip Colour", Color) = (0.55, 0.74, 0.30, 1)
        _BladeHeight ("Blade Height (m, set by GrassField)", Float) = 0.45
        _WindStrength ("Wind Strength", Float) = 0.08
        _WindSpeed ("Wind Speed", Float) = 1.6
        _PushRadius ("Push Radius", Float) = 1.1
        _PushStrength ("Lean Away Strength", Float) = 0.22
        _RustleStrength ("Rustle Strength", Float) = 0.14
        _RustleSpeed ("Rustle Speed", Float) = 13
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _TipColor;
            float _BladeHeight;
            float _WindStrength;
            float _WindSpeed;
            float _PushRadius;
            float _PushStrength;
            float _RustleStrength;
            float _RustleSpeed;
        CBUFFER_END

        float4 _KFPushers[8];
        float _KFPusherCount;

        // Displaces a blade vertex in world space; h = 0 at the root, 1 at the tip.
        float3 Bend(float3 positionOS, out float h)
        {
            h = saturate(positionOS.y / max(_BladeHeight, 0.01));
            float3 ws = TransformObjectToWorld(positionOS);
            float3 root = TransformObjectToWorld(float3(0, 0, 0));
            float bend = h * h;

            float phase = _Time.y * _WindSpeed + root.x * 0.35 + root.z * 0.27;
            ws.xz += float2(sin(phase), cos(phase * 0.8)) * _WindStrength * bend;

            float2 push = 0;
            float rustle = 0;
            for (int i = 0; i < 8; i++)
            {
                if (i >= (int)_KFPusherCount) break;
                float2 d = root.xz - _KFPushers[i].xz;
                float dist = length(d);
                float k = saturate(1 - dist / _PushRadius) * _KFPushers[i].w;
                push += (dist > 0.001 ? d / dist : float2(0, 0)) * k * k;   // squared: mostly the fighter itself, not the trail
                rustle = max(rustle, k);
            }
            ws.xz += push * _PushStrength * bend;
            // Quick per-blade shake; each clump has its own phase so it reads as rustling, not a wave.
            float t = _Time.y * _RustleSpeed + root.x * 7.3 + root.z * 5.1;
            ws.xz += float2(sin(t), sin(t * 1.37 + 1.7)) * rustle * _RustleStrength * bend;
            return ws;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float h : TEXCOORD1;
                float fog : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionWS = Bend(v.positionOS.xyz, o.h);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 col = lerp(_BaseColor.rgb, _TipColor.rgb, i.h);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                // Blades are thin and double-sided: light them as if facing up, darker at the root.
                float3 lit = col * light.color * (0.45 + 0.55 * light.shadowAttenuation) * (0.6 + 0.4 * i.h)
                           + col * SampleSH(float3(0, 1, 0)) * 0.8;
                return half4(MixFog(lit, i.fog), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float h;
                return TransformWorldToHClip(Bend(v.positionOS.xyz, h));
            }

            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
