// Névoa do desfiladeiro e borrifo das cachoeiras (partículas): fumaça macia em alfa, iluminada pela
// cor ambiente (lua/Fenda via harmônicos esféricos) com um toque da cor da partícula; névoa do Unity.
Shader "Campanula/Mist"
{
    Properties
    {
        _MainTex ("Fumaça", 2D) = "white" {}
        _Lift ("Brilho próprio", Float) = 0.06
        _AmbK ("Ambiente", Float) = 2.6
    }
    SubShader
    {
        Tags { "Queue"="Transparent+4" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Lift, _AmbK;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_FOG_COORDS(1) };
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float4 t = tex2D(_MainTex, i.uv);
                float3 amb = ShadeSH9(float4(0, 1, 0, 1));
                float3 col = i.color.rgb * (amb * _AmbK + _Lift);
                float a = t.a * i.color.a;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return float4(col, a);
            }
            ENDCG
        }
    }
}
