Shader "Universal Render Pipeline/GhostPreview"
{
    Properties
    {
        // Base tint of the ghost. Driven at runtime (green = placeable,
        // red = blocked) by BuildPlacementController via material.SetColor.
        _Color       ("Color", Color)                 = (0.2, 1.0, 0.3, 1)
        // Overall opacity multiplier for the whole ghost.
        _Alpha       ("Alpha", Range(0, 1))           = 0.35
        // How strongly the silhouette edges (fresnel rim) glow.
        _FresnelPower("Fresnel Power", Range(0.1, 8)) = 2.5
        _RimStrength ("Rim Strength", Range(0, 4))    = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent"
        }
        LOD 100
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "GhostUnlit"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Alpha;
                float  _FresnelPower;
                float  _RimStrength;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posIn = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrmIn = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = posIn.positionCS;
                OUT.positionWS  = posIn.positionWS;
                OUT.normalWS    = nrmIn.normalWS;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 normalWS  = normalize(IN.normalWS);
                float3 viewDirWS = normalize(GetCameraPositionWS() - IN.positionWS);

                // Fresnel: bright at grazing angles, giving the translucent
                // "hologram" silhouette glow regardless of the tint colour.
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);

                half3 rgb = _Color.rgb;
                // Edges glow toward white-tinted rim; faces stay translucent.
                half  alpha = saturate(_Alpha + fresnel * _RimStrength) * _Color.a;
                rgb += fresnel * _RimStrength * _Color.rgb;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
