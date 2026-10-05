// Partes comuns da noite da Ruptura (céu, paisagem distante, efeitos do impacto). Os valores
// globais vêm de NightSetup (fixos) e de RuptureSky/ImpactFX (animados pela abertura).
#ifndef ARENNIGHT_COMMON
#define ARENNIGHT_COMMON

float4 _NightMoonDir, _NightMoonCol, _NightAmbSky, _NightAmbGround;
float4 _NightHaze, _NightSkyHorizon, _NightSkyMid;
float4 _NightFogParams;   // x densidade em y0 (1/m), y 1/altura de escala, z y0, w máximo
float4 _NightMist;        // névoa baixa dos vales: x densidade, y 1/altura, z y0
float4 _FendaDirW, _FendaLight;
float4 _ImpactPosW, _ImpactLight;
float4 _NoteLightPos, _NoteLight;   // a nota dourada descendo (acende o chão embaixo dela)
float4 _Shock;            // x raio da 1ª onda (m), y intensidade, z raio da 2ª, w intensidade

float NightWrapPi(float a) { return a - 6.2831853 * floor((a + 3.14159265) / 6.2831853); }

// cor do céu perto do horizonte numa direção (para reflexos e névoa)
float3 NightHorizon(float3 d)
{
    float y = d.y;
    float3 c = lerp(_NightSkyHorizon.rgb, _NightSkyMid.rgb, smoothstep(0.0, 0.25, y));
    c += _NightSkyHorizon.rgb * 0.35 * exp(-abs(y) / 0.04);
    return c;
}

// cor da névoa olhando na direção V: o horizonte + lua, Fenda e clarão espalhados no ar
float3 NightHaze(float3 V)
{
    float3 c = _NightHaze.rgb;
    c += _NightMoonCol.rgb * pow(saturate(dot(V, normalize(_NightMoonDir.xyz))), 6.0) * 0.12;
    float3 F = normalize(_FendaDirW.xyz);
    c += _FendaLight.rgb * (pow(saturate(dot(V, F)), 10.0) * 0.3 + 0.01);
    float3 toI = normalize(_ImpactPosW.xyz - _WorldSpaceCameraPos);
    float ci = saturate(dot(V, toI));
    c += _ImpactLight.rgb * (pow(ci, 40.0) * 0.8 + pow(ci, 6.0) * 0.12);
    return c;
}

float NightFogOD(float3 C, float3 P, float L, float a0, float k, float y0)
{
    float dy = P.y - C.y;
    float a = a0 * exp(-k * (C.y - y0));
    float x = k * dy;
    float f = abs(x) > 1e-3 ? (1.0 - exp(-x)) / x : 1.0;
    return a * L * f;
}

// quanto de névoa entre a câmera C e o ponto P (névoa de altura + névoa baixa dos vales)
float NightFog(float3 C, float3 P, float L)
{
    float od = NightFogOD(C, P, L, _NightFogParams.x, _NightFogParams.y, _NightFogParams.z);
    od += NightFogOD(C, P, L, _NightMist.x, _NightMist.y, _NightMist.z);
    return min(1.0 - exp(-od), _NightFogParams.w);
}

#endif
