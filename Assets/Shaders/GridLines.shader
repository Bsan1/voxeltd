Shader "Unlit/GridLines"
{
    Properties
    {
        _LineColor  ("Line Color", Color)              = (0.55, 0.85, 1.0, 1)
        _Color      ("Tooling Color Alias", Color)     = (0, 0, 0, 0)
        // Cell size in WORLD units. Driven at runtime from the scene's
        // UnityEngine.Grid component (see GridLineDriver), so there is no
        // manual "grid scale" to keep in sync any more.
        _CellSize   ("Cell Size (world units)", Float) = 1
        _LineWidth  ("Line Width (world units)", Range(0.001, 0.5)) = 0.03
        // World-space origin (xz) the grid lines are anchored to. Driven from
        // the Grid component's transform position.
        _GridOrigin ("Grid Origin (XYZ)", Vector)      = (0, 0, 0, 0)
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
            Name "GridUnlit"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _LineColor;
                float4 _Color;
                float  _CellSize;
                float  _LineWidth;
                float4 _GridOrigin;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = pos.positionCS;
                OUT.positionWS  = pos.positionWS;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float cell = max(_CellSize, 1e-4);

                // Position measured in cells from the grid origin, on the
                // ground (xz) plane. One grid line per integer boundary, so
                // spacing tracks _CellSize automatically.
                float2 gridUV = (IN.positionWS.xz - _GridOrigin.xz) / cell;

                // Distance (in cells) to the nearest line on each axis.
                float2 distToLine = abs(frac(gridUV) - 0.5);
                distToLine = 0.5 - distToLine; // 0 on a line, 0.5 mid-cell

                // Screen-space derivative -> constant pixel-width feather so
                // lines stay anti-aliased at any camera distance.
                float2 aa = fwidth(gridUV);

                // Convert the world-space line width into cell units.
                float halfW = (_LineWidth * 0.5) / cell;

                float2 lineAxis = 1.0 - smoothstep(float2(0, 0), aa + halfW, distToLine);
                float lineMask = saturate(max(lineAxis.x, lineAxis.y));

                // Everything that is not a line is fully transparent.
                half4 col = _LineColor;
                col.a *= lineMask;
                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
