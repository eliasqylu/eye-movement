// Made by Gemini
Shader "Unlit/CircularProgressBar"
{
    Properties
    {
        [Header(Appearance)]
        _Color ("Bar Color", Color) = (1,1,1,1)
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {} // Needed for UI Image component

        [Header(Progress)]
        _Progress ("Progress (0-1)", Range(0.0, 1.0)) = 0.5
        _Thickness ("Thickness (0-0.5)", Range(0.0, 0.5)) = 0.1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            // Standard transparency settings
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            
            // Turn off fog
            Fog { Mode Off }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // Input from the mesh (e.g., a Quad)
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            // Data passed from vertex to fragment shader
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            // Properties from the shader inspector
            fixed4 _Color;
            float _Progress;
            float _Thickness;

            // Vertex Shader: Prepares data for the fragment shader
            v2f vert (appdata v)
            {
                v2f o;
                // Convert vertex position to screen clip space
                o.vertex = UnityObjectToClipPos(v.vertex);
                // Pass UV coordinates through
                o.uv = v.uv;
                return o;
            }

            // Fragment Shader: Calculates the color for each pixel
            fixed4 frag (v2f i) : SV_Target
            {
                // 1. Center the UV coordinates to make (0,0) the middle of the quad
                float2 uv = i.uv - 0.5;

                // 2. Calculate the angle of the pixel.
                // We use atan2(x, y) which conveniently starts at the top (12 o'clock)
                // and increases clockwise. Its range is [-PI, PI].
                float angle = atan2(uv.x, uv.y);

                // Remap the angle from [-PI, PI] to a continuous [0, 2*PI] range
                if (angle < 0.0)
                {
                    angle += 2.0 * UNITY_PI;
                }
                
                // Normalize the angle to a [0, 1] range to match our _Progress property
                float fill = angle / (2.0 * UNITY_PI);

                // 3. Calculate the distance from the center (radius)
                float radius = length(uv);

                // 4. Create masks with anti-aliasing for smooth edges
                float aa_width = fwidth(radius) * 0.5; // Screen-space anti-aliasing width

                // Mask for the ring shape based on radius and thickness
                float outer_radius = 0.5;
                float inner_radius = outer_radius - _Thickness;
                float ring_mask = smoothstep(inner_radius - aa_width, inner_radius + aa_width, radius) *
                                  (1.0 - smoothstep(outer_radius - aa_width, outer_radius + aa_width, radius));

                // Mask for the progress fill based on the angle
                // This creates a smooth fade at the edge of the progress line
                float progress_mask = 1.0 - smoothstep(_Progress - (fwidth(fill) * 0.5), _Progress + (fwidth(fill) * 0.5), fill);

                // 5. Combine the masks and apply the final color
                fixed4 final_color = _Color;
                final_color.a *= ring_mask * progress_mask;

                return final_color;
            }
            ENDCG
        }
    }
    FallBack "Transparent/VertexLit"
}