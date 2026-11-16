Shader "Custom/BrushShader"
{
    Properties
    {
        _MainTex ("Existing Canvas", 2D) = "white" {}
        _CenterUV ("Brush Center UV", Vector) = (0.5, 0.5, 0, 0)
        _BrushSize ("Brush Size", Range(0, 0.5)) = 0.05
        _BrushColor ("Brush Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _CenterUV;
            float _BrushSize;
            fixed4 _BrushColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Get the color from the existing canvas
                fixed4 existingColor = tex2D(_MainTex, i.uv);

                // Calculate distance from the current pixel to the brush's center
                float dist = distance(i.uv, _CenterUV.xy);

                // If inside the new brush stroke, return the brush color.
                // Otherwise, just return the color that was already there.
                if (dist < _BrushSize)
                {
                    // Using max keeps the white from overwriting other white strokes
                    return max(existingColor, _BrushColor);
                }
                else
                {
                    return existingColor;
                }
            }
            ENDCG
        }
    }
}