// Glossy rubber-glove look that does not depend on scene lights: a fixed soft light from above-front plus a highlight.
Shader "Chorewars/GloveShaded"
{
    Properties { _Color ("Glove colour", Color) = (1,0.78,0.1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Cull Back
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 view : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_OUTPUT(v2f, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.view = normalize(WorldSpaceViewDir(v.vertex));
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.n);
                float3 l = normalize(float3(0.25, 0.85, 0.45));
                float diff = saturate(dot(n, l)) * 0.55 + 0.45;
                float3 h = normalize(l + normalize(i.view));
                float spec = pow(saturate(dot(n, h)), 40.0) * 0.45;
                float rim = pow(1.0 - saturate(dot(n, normalize(i.view))), 3.0) * 0.18;
                return fixed4(_Color.rgb * diff + spec + rim * _Color.rgb, 1);
            }
            ENDCG
        }
    }
}
