Shader "Chorewars/DollhouseClean"
{
    Properties
    {
        _CleanAmount ("Clean Amount", Range(0, 1)) = 1
        _RoomColour ("Room Base Tint", Color) = (0.3, 0.8, 0.5, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float _CleanAmount;
            float4 _RoomColour;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _CleanAmount;

                float3 dirty = float3(0.15, 0.08, 0.05);
                float3 mid   = float3(0.55, 0.50, 0.40);
                float3 clean = _RoomColour.rgb;

                float3 baseCol = lerp(lerp(dirty, mid, saturate(t * 2.0)),
                                      lerp(mid, clean, saturate(t * 2.0 - 1.0)),
                                      saturate(t * 2.0));

                float n = noise(i.uv * 40.0 + _Time.y * 0.1) * 0.15;
                float grunge = (1.0 - t) * n;
                baseCol -= grunge;

                if (t > 0.85)
                {
                    float sparkle = sin((i.uv.x + i.uv.y) * 200.0 + _Time.y * 8.0);
                    sparkle = step(0.97, sparkle) * (t - 0.85) * 3.0;
                    baseCol += sparkle * 0.25;
                }

                return fixed4(baseCol, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Unlit/Color"
}
