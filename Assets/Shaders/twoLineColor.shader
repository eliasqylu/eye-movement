Shader "Unlit/TwoColorLine"
{
    Properties
    {
        _Color1 ("Color 1 (Left)", Color) = (0,1,0,1) // Green
        _Color2 ("Color 2 (Right)", Color) = (0,0,1,1) // Blue
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" } // Ensure transparency for blending
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha // Standard alpha blending
            Cull Off // Render both sides

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0; // Add UV coordinates to appdata
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0; // Pass UV coordinates to fragment shader
            };

            fixed4 _Color1;
            fixed4 _Color2;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; // Pass UVs directly
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Interpolate color based on the U coordinate (horizontal position along the line)
                // Assuming UV.x ranges from 0 (left) to 1 (right) along the line segment.
                // LineRenderer's UVs are typically 0 at start point, 1 at end point.
                if (i.uv.x < 0.5){
                    return _Color1;
                }
                else{
                    return _Color2;
                }

            }
            ENDCG
        }
    }
}