Shader "Unlit/eyeGestureControl2"
{
    Properties
    {
        _Thickness ("Thickness", Range(0.001, 0.5)) = 0.1 // Thickness of the circle ring (0.5 makes a filled circle)
        _Smoothness ("Smoothness", Range(0.0001, 0.1)) = 0.01 // Anti-aliasing for circle edges
        _customAlpha ("custom visibility", Float) = 1
        
        // Input angle: The FINAL center angle (in degrees) for Segment 1, calculated in C#.
        // This includes the 180-degree offset for orientation.
        _Segment1CenterAngleDeg ("Segment 1 Center Angle (Deg)", Float) = 0 
        _SegmentColor1 ("Segment 1 Color", Color) = (1,1,1,1) // Default White
        _SegmentColor2 ("Segment 2 Color", Color) = (1,0,0,1) // Default Red
        _SegmentRatio ("Segment 1 Ratio (0-1)", Range(0.01, 0.99)) = 0.3333 // 1/3 of the circumference for Segment 1
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"       // Render in the transparent queue
            "RenderType"="Transparent"  // Treat as a transparent object
            "IgnoreProjector"="True"    // Usually good for UI
            "PreviewType"="Plane"       // Show on a plane in material preview
        }
        LOD 100 // Level of Detail

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha // Standard alpha blending
            ZWrite Off                      // Don't write to depth buffer for transparents
            Cull Off                        // Render both sides (or use Cull Back if always front-facing)

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc" // Standard Unity shader library

            // Define degree/radian conversion constants for HLSL
            #ifndef UNITY_DEG_TO_RAD
            #define UNITY_DEG_TO_RAD 0.01745329251 // (PI / 180)
            #endif
            #ifndef UNITY_RAD_TO_DEG
            #define UNITY_RAD_TO_DEG 57.2957795131 // (180 / PI)
            #endif

            // Helper function to wrap angles to 0 to 360 degrees
            float WrapAngleDeg(float angle)
            {
                return fmod(angle + 360.0, 360.0);
            }

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

            // Shader properties declared as uniforms
            float _Thickness;
            float _Smoothness;
            float _Segment1CenterAngleDeg; // Directly the center angle of Segment 1 (in degrees)
            fixed4 _SegmentColor1;
            fixed4 _SegmentColor2;
            float _SegmentRatio;
            float _customAlpha;

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
                float2 center = float2(0.5, 0.5); // Center of the UV space (circle center)
                float dist = distance(i.uv, center); // Distance from the current UV to the center

                // Define the circle's ring boundaries
                float outer_radius = 0.5; // The circle touches the edges of the quad
                float inner_radius = outer_radius - _Thickness; // Inner boundary based on thickness

                // Anti-aliasing for the outer edge of the ring
                float outer_edge_aa = smoothstep(outer_radius + _Smoothness, outer_radius - _Smoothness, dist);

                // Anti-aliasing for the inner edge of the ring
                float inner_edge_aa = smoothstep(inner_radius - _Smoothness, inner_radius + _Smoothness, dist);
                
                // The final alpha for the ring is achieved when a point is inside the outer smoothed edge
                // AND outside the inner smoothed edge.
                float ring_alpha = outer_edge_aa * inner_edge_aa;

                // If the pixel is completely outside the ring, discard it early
                if (ring_alpha < 0.001) { // Use a small threshold for precision
                    discard;
                }

                // --- Segment Coloring Logic (All in Degrees) ---

                // The center angle for Segment 1 is directly provided by the C# script
                // We ensure it's wrapped, though C# should already pass it wrapped.
                float segment1_center_angle_deg = WrapAngleDeg(_Segment1CenterAngleDeg);


                // Determine the angle of the current UV pixel relative to the ring's center (0.5, 0.5)
                // Angle from East (+X) counter-clockwise.
                float2 uv_centered = i.uv - center;
                float current_pixel_angle_rad = atan2(uv_centered.y, uv_centered.x); // atan2(y,x) gives -PI to PI
                float current_pixel_angle_deg = current_pixel_angle_rad * UNITY_RAD_TO_DEG;
                current_pixel_angle_deg = WrapAngleDeg(current_pixel_angle_deg);


                // Calculate the angular arc of Segment 1 (full arc, not half) in degrees
                float segment1_full_arc_deg = _SegmentRatio * 360.0;
                float segment1_half_arc_deg = segment1_full_arc_deg / 2.0;

                // Calculate the start and end angles for Segment 1 in degrees
                float segment1_start_angle_deg = WrapAngleDeg(segment1_center_angle_deg - segment1_half_arc_deg);
                float segment1_end_angle_deg = WrapAngleDeg(segment1_center_angle_deg + segment1_half_arc_deg);

                bool is_segment1;
                // Handle wrapping: if start angle is greater than end angle, it means the segment crosses the 0/360 boundary
                if (segment1_start_angle_deg > segment1_end_angle_deg)
                {
                    is_segment1 = (current_pixel_angle_deg >= segment1_start_angle_deg || current_pixel_angle_deg <= segment1_end_angle_deg);
                }
                else
                {
                    is_segment1 = (current_pixel_angle_deg >= segment1_start_angle_deg && current_pixel_angle_deg <= segment1_end_angle_deg);
                }

                fixed4 final_color;
                if (is_segment1)
                {
                    final_color = _SegmentColor1;
                }
                else
                {
                    final_color = _SegmentColor2;
                }
                
                final_color.a *= ring_alpha; // Apply the calculated alpha for the ring shape
                final_color.a *= _customAlpha;

                return final_color;
            }
            ENDCG
        }
    }
    Fallback "Transparent/VertexLit" // Fallback for older hardware or unsupported platforms
}

