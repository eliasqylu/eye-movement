Shader "Unlit/nodButton"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ColorBody("body color", Color) = (1,1,1,0.5)
        _ButtonPressure("button pressure", float) = 0
        _ColorCenterDot("center dot colour", Color) = (1,1,1,1)
        _RadiusCenterDot("center dot radius", float) = 0.5
        _ThicknessCenterDotBorder("center dot border thickness", float) = 0.01
    }
    SubShader
    {
        // Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        // LOD 100

        // ZWrite Off
        // Blend SrcAlpha OneMinusSrcAlpha 
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Back

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
            float4 _ColorBody;
            float _ButtonPressure;
            float4 _ColorCenterDot;
            float _RadiusCenterDot;
            float _ThicknessCenterDotBorder;

            // Function to remap a value from one range to another
            float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
            {
                return toMin + (value - fromMin) * (toMax - toMin) / (fromMax - fromMin);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                 //center coordinate to middle of raw image
                float a = i.uv.x-0.5;
                float b = i.uv.y-0.5;

                // sample the texture
                fixed4 col = float4(0,0,0,0);

                //center dot border
                float borderWideEdge = _RadiusCenterDot+_ThicknessCenterDotBorder;
                
                //remap button pressure
                float cursorRadius = Remap(_ButtonPressure, 0,1,0.5,borderWideEdge);
                if (a*a+b*b <= cursorRadius*cursorRadius){
                    col = _ColorBody;
                }
                //border edge
                if (a*a+b*b <= borderWideEdge*borderWideEdge){
                    col = float4(0,0,0,1);
                }
                //show center dot when button fully down
                if (a*a+b*b <=_RadiusCenterDot*_RadiusCenterDot){
                    if (_ButtonPressure>0.99f){
                        col = _ColorCenterDot;
                    }
                    else{
                        col = float4(0,0,0,0);
                    }
                }
                
                return col;
            }
            ENDCG
        }
    }
}
