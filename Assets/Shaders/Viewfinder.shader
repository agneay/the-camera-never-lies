Shader "Hidden/CNL/Viewfinder"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _VF, _Photo, _Flash, _Blink, _Glitch, _Seed, _Grain, _Vignette, _Brightness;

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233)) + _Seed) * 43758.5453);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;

                // Horizontal tearing for supernatural interference.
                if (_Glitch > 0.001)
                {
                    float ln = floor(uv.y * 48.0);
                    float r = hash(float2(ln, floor(_Seed * 7.0)));
                    if (r < _Glitch * 0.6)
                        uv.x += (hash(float2(ln, 3.1)) - 0.5) * 0.09 * _Glitch;
                }

                float ca = 0.0012 + _VF * 0.0018 + _Glitch * 0.012;
                float3 col;
                col.r = tex2D(_MainTex, uv + float2(ca, 0)).r;
                col.g = tex2D(_MainTex, uv).g;
                col.b = tex2D(_MainTex, uv - float2(ca, 0)).b;
                col *= _Brightness;

                float lum = dot(col, float3(0.299, 0.587, 0.114));

                // Viewfinder: desaturated, slightly cold-green digital sensor look with a lifted black level.
                float3 vf = lerp(col, lum * float3(0.86, 1.0, 0.92), 0.6) * 1.6 + 0.02;
                // Photograph: warm, faded print.
                float3 ph = lerp(col, lum * float3(1.1, 0.96, 0.8), 0.5) * 1.05 + float3(0.02, 0.015, 0.0);

                col = lerp(col, vf, _VF);
                col = lerp(col, ph, _Photo);

                // Sensor scanlines in the viewfinder.
                col *= 1.0 - _VF * 0.07 * (0.5 + 0.5 * sin(uv.y * _ScreenParams.y * 1.6));

                float g = hash(floor(uv * _ScreenParams.xy * 0.5) + _Seed) - 0.5;
                col += g * _Grain * (1.0 + _Glitch);

                float2 d = uv - 0.5;
                col *= saturate(1.0 - dot(d, d) * _Vignette * 2.0);

                col *= 1.0 - _Blink * 0.85;
                col = lerp(col, float3(1, 1, 1), _Flash);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
