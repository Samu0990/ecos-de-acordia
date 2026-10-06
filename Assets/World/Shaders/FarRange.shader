// Serras distantes dos reinos (anéis de relevo além do terreno jogável): rocha com estrias de ruído, sol e
// ambiente do reino, neve opcional nos topos e NÉVOA PRÓPRIA por camada (perto mais nítida, longe quase só a
// cor da neblina) — perspectiva atmosférica em camadas, em vez de uma silhueta chapada. A névoa usa a cor da
// neblina da cena (unity_FogColor), então casa com o céu (Elyndra/WorldSky) e com a Corrupção do reino.
Shader "Elyndra/FarRange"
{
    Properties
    {
        _Color ("Rocha", Color) = (0.25,0.23,0.26,1)
        _Noise ("Ruído", 2D) = "gray" {}
        _HazeK ("Névoa da camada", Range(0,1)) = 0.5
        _MistH ("Névoa no pé (m)", Float) = 80
        _BaseY ("Base (m)", Float) = 0
        _TopH ("Altura dos picos (m)", Float) = 300
        _Snow ("Neve nos topos", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            float4 _Color; sampler2D _Noise; float _HazeK, _MistH, _BaseY, _TopH, _Snow;
            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; float3 n : TEXCOORD1; };
            v2f vert (appdata_base v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz; o.n = UnityObjectToWorldNormal(v.normal);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.n);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float h01 = saturate((i.wp.y - _BaseY) / max(1, _TopH));
                // estrias de rocha (ruído esticado na vertical) + manchas grandes
                float s1 = tex2D(_Noise, float2(i.wp.x + i.wp.z, i.wp.y * 3.0) * 0.0021).r;
                float s2 = tex2D(_Noise, i.wp.xz * 0.0006).r;
                float3 alb = _Color.rgb * (0.7 + 0.45 * s1) * (0.85 + 0.3 * s2);
                // neve: topos e faces viradas para cima
                float snow = _Snow * smoothstep(0.45, 0.75, h01 + (s1 - 0.5) * 0.25) * smoothstep(0.35, 0.7, n.y);
                alb = lerp(alb, float3(0.85, 0.87, 0.9), snow);
                float ndl = saturate(dot(n, L) * 0.75 + 0.25);
                float3 amb = lerp(unity_AmbientGround.rgb, unity_AmbientSky.rgb, n.y * 0.5 + 0.5) * 0.9 + unity_AmbientEquator.rgb * 0.25;
                float3 col = alb * (_LightColor0.rgb * ndl * 0.85 + amb);
                // névoa da camada + bruma no pé das serras
                float mist = 1 - smoothstep(0, _MistH, i.wp.y - _BaseY);
                float haze = saturate(_HazeK + (1 - _HazeK) * mist * 0.75);
                col = lerp(col, unity_FogColor.rgb, haze);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
