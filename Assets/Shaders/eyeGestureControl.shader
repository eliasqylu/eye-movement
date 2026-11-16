Shader "Unlit/eyeGestureControl"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1) // Base color of the circle
        _Thickness ("Thickness", Range(0.001, 0.5)) = 0.1 // Thickness of the circle ring (0.5 makes a filled circle)
        _Smoothness ("Smoothness", Range(0.0001, 0.1)) = 0.01 // Anti-aliasing for circle edges
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"           // Render in the transparent queue
            "RenderType"="Transparent"      // Treat as a transparent object
            "IgnoreProjector"="True"        // Usually good for UI
            "PreviewType"="Plane"           // Show on a plane in material preview
        }
        LOD 100 // Level of Detail

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha // Standard alpha blending
            ZWrite Off                     // Don't write to depth buffer for transparents
            Cull Off                       // Render both sides (or use Cull Back if always front-facing)

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // No multi_compile_fog pragma means no fog variants will be compiled by default.
            #pragma target 2.0

            #include "UnityCG.cginc" // Standard Unity shader library

            // Input structure for the vertex shader
            struct appdata
            {
                float4 vertex : POSITION; // Vertex position
                float2 uv : TEXCOORD0;    // UV coordinates
            };

            // Structure passed from vertex to fragment shader
            struct v2f
            {
                float4 vertex : SV_POSITION; // Clip space position
                float2 uv : TEXCOORD0;       // UV coordinates
            };

            // Shader properties
            fixed4 _Color;
            float _Thickness;
            float _Smoothness;

            // Vertex Shader
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex); // Transform vertex to clip space
                o.uv = v.uv;                               // Pass UVs to fragment shader
                return o;
            }

            // Fragment Shader
            fixed4 frag (v2f i) : SV_Target
            {
                float2 center = float2(0.5, 0.5); // Center of the UV space
                float dist = distance(i.uv, center); // Distance from the current UV to the center

                // Define the circle's ring boundaries
                float outer_radius = 0.5; // The circle touches the edges of the quad
                float inner_radius = outer_radius - _Thickness; // Inner boundary based on thickness

                // Anti-aliasing for the outer edge of the ring
                // Alpha is 1 if dist is well inside outer_radius, 0 if well outside.
                float outer_edge_aa = smoothstep(outer_radius + _Smoothness, outer_radius - _Smoothness, dist);

                // Anti-aliasing for the inner edge of the ring
                // Alpha is 1 if dist is well outside inner_radius, 0 if well inside.
                float inner_edge_aa = smoothstep(inner_radius - _Smoothness, inner_radius + _Smoothness, dist);
                
                // The final alpha for the ring is achieved when a point is inside the outer smoothed edge
                // AND outside the inner smoothed edge.
                float ring_alpha = outer_edge_aa * inner_edge_aa;

                fixed4 final_color = _Color;
                final_color.a *= ring_alpha; // Apply the calculated alpha to the input color's alpha

                return final_color;
            }
            ENDCG
        }
    }
    Fallback "Transparent/VertexLit" // Fallback for older hardware or unsupported platforms
}