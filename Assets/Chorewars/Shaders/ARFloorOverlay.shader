// The cleaned-floor overlay for passthrough (AR).
//
// In AR the player must keep seeing their real floor, so the overlay cannot be an opaque quad. It draws the
// same texture the dollhouse shares, but only where something has changed: a texel still showing the flat
// "uncleaned" colour is fully transparent, and cleaned, flashing or photographed texels show as a translucent
// tint over the real floor. Built-in render pipeline, unlit.
Shader "Chorewars/ARFloorOverlay"
{
    Properties
    {
        _MainTex ("Floor texture", 2D) = "white" {}
        _Uncleaned ("Uncleaned colour (matches CoverageFloorVisualizer)", Color) = (0.149, 0.165, 0.188, 1)
        _CleanedAlpha ("Cleaned opacity", Range(0, 1)) = 0.5
        _Sensitivity ("How far from uncleaned counts as cleaned", Float) = 6
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Uncleaned;
            fixed _CleanedAlpha;
            float _Sensitivity;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float changed = saturate(length(c.rgb - _Uncleaned.rgb) * _Sensitivity);
                return fixed4(c.rgb, changed * _CleanedAlpha);
            }
            ENDCG
        }
    }
}
