// VFX Graph embeds only the selected entry point. Keep each function self-contained.
// Every particle has an independent path: 12 vapor wisps, 8 smoke puffs, 44 motes
// per group of 64. Output-local filtering does not kill the shared simulation.
void PerformerMagicInitialize(
    inout VFXAttributes attributes,
    in int StyleId, in int EffectMode, in float3 TargetCenter,
    in float TargetRadius, in float TargetHeight, in float ParticleSize,
    in float ParticleLifetime, in int Seed)
{
    float s = frac((float)(attributes.seed & 0x00FFFFFFu) / 16777216.0 + (float)((uint)Seed & 0x0000FFFFu) / 65536.0);
    attributes.position = TargetCenter;
    attributes.targetPosition = TargetCenter + float3(0.0, 0.001, 0.0);
    attributes.velocity = float3(0.0, 1.0, 0.0);
    attributes.lifetime = max(0.1, ParticleLifetime) * (EffectMode == 0 ? 1.0 : lerp(0.85, 1.15, s));
    attributes.age = 0.0;
    attributes.size = ParticleSize;
    attributes.angleZ = 0.0;
    attributes.color = float3(1.0, 1.0, 1.0);
    attributes.alpha = 0.0;
}

void PerformerMagicUpdate(
    inout VFXAttributes attributes,
    in int StyleId, in int EffectMode, in float EffectTime, in float EffectProgress,
    in float3 TargetCenter, in float3 TargetBase, in float TargetRadius, in float TargetHeight,
    in float ParticleSize, in float RiseSpeed, in float SwirlStrength,
    in float Turbulence, in float PulseFrequency, in int Seed)
{
    const float tau = 6.2831853;
    uint lane = attributes.particleId % 64u;
    float s = frac((float)(attributes.seed & 0x00FFFFFFu) / 16777216.0 + (float)((uint)Seed & 0x0000FFFFu) / 65536.0);
    float a = frac(sin((s * 71.3 + 1.1) * 12.9898 + 78.233) * 43758.5453);
    float b = frac(sin((s * 31.7 + 4.2) * 12.9898 + 78.233) * 43758.5453);
    float c = frac(sin((s * 53.1 + 2.7) * 12.9898 + 78.233) * 43758.5453);
    float life = saturate(attributes.age / max(0.1, attributes.lifetime));
    float p = saturate(EffectProgress);
    float envelope = EffectMode == 0
        ? smoothstep(0.0, 0.08 + b * 0.05, p) * (1.0 - smoothstep(0.68 + a * 0.10, 1.0, p))
        : smoothstep(0.0, 0.12, life) * (1.0 - smoothstep(0.72, 1.0, life));
    float radius = max(TargetRadius, 0.01);
    float height = max(TargetHeight, 0.02);
    float floorOffset = TargetBase.y - TargetCenter.y;
    float rate = lerp(0.23, 0.46, c) * (0.65 + RiseSpeed * 0.45);
    if (StyleId == 2) rate *= 1.30;
    if (StyleId == 4) rate *= 0.48;
    float flow = frac(EffectTime * rate + s);
    float cycleFade = smoothstep(0.0, 0.14, flow) * (1.0 - smoothstep(0.65, 1.0, flow));
    float breathing = 0.88 + 0.12 * sin(EffectTime * PulseFrequency * 1.7 + b * tau);
    float surge = EffectMode == 0 ? exp(-pow((p - (0.25 + c * 0.10)) / 0.16, 2.0)) : 0.0;
    attributes.alpha = envelope * cycleFade * breathing;
    attributes.size = ParticleSize * lerp(0.45, 1.25, a);
    attributes.angleZ = b * 360.0 + EffectTime * lerp(-13.0, 13.0, c);

    // Evaluate the same continuous path twice to obtain its local tangent. No linked
    // ribbon endpoints, shared helix, or clock-stepped random jumps are involved.
    float3 currentLocal = float3(0.0, 0.0, 0.0);
    float3 nextLocal = float3(0.0, 0.0, 0.0);
    for (int sampleIndex = 0; sampleIndex < 2; sampleIndex++)
    {
        float t = EffectTime + (float)sampleIndex * 0.02;
        float f = frac(t * rate + s);
        float theta = s * tau + t * SwirlStrength * (0.12 + b * 0.10)
            + sin(t * 0.71 + c * tau) * 0.22;
        float r = radius * lerp(0.52, 1.08, a);
        float3 q = float3(theta * 1.7 + b * 7.0, f * 5.0 + c * 9.0, a * 6.0);
        float3 eddy = float3(
            sin(q.y * 1.3 + t * 1.17) + cos(q.z * 1.6 - t * 0.63),
            sin(q.z * 1.1 + t * 0.91) + cos(q.x * 1.4 + t * 0.53),
            sin(q.x * 1.2 - t * 0.79) + cos(q.y * 1.5 + t * 0.67));
        float3 local = float3(cos(theta) * r, floorOffset + height * (0.06 + f * 0.90), sin(theta) * r);

        if (StyleId == 0)
        {
            // Buoyant combustion: widening embers and compact rolling flame fragments.
            local.xz *= 0.82 + f * 0.34;
            local.y = floorOffset + 0.04 + height * f * 0.94;
            local += eddy * Turbulence * radius * (0.16 + f * 0.22);
        }
        else if (StyleId == 1)
        {
            // Uneven inward gathering and release through twisting pockets of vapor.
            float gather = EffectMode == 0
                ? lerp(1.15, 0.65, smoothstep(0.0, 0.19, p))
                    + 0.46 * smoothstep(0.20 + b * 0.10, 0.56 + b * 0.10, p)
                : 0.95;
            local.xz *= gather;
            local += eddy * Turbulence * radius * 0.38;
        }
        else if (StyleId == 2)
        {
            // Local charged dust and tiny discharges rather than target-height bolts.
            local.xz *= 0.92;
            local += eddy * Turbulence * radius * 0.28;
            local += float3(sin(t * 8.0 + b * 23.0), cos(t * 6.1 + c * 17.0), sin(t * 7.3 + a * 19.0)) * radius * 0.025;
        }
        else if (StyleId == 3)
        {
            // Fine luminous dust carried upward in irregular, slowly opening currents.
            local.xz *= 0.75 + f * 0.30;
            local += eddy * Turbulence * radius * 0.30;
        }
        else
        {
            // Quiet suspended mist, with independent drift rather than rising halos.
            local.y = floorOffset + height * (0.14 + b * 0.70)
                + sin(t * 0.42 + s * tau) * height * 0.04;
            local += eddy * Turbulence * radius * 0.24;
        }
        // Leave floor clearance for the puffs, including the small turbulent excursions.
        local.y = max(local.y, floorOffset + 0.025);
        if (sampleIndex == 0) currentLocal = local;
        else nextLocal = local;
    }
    attributes.position = TargetCenter + currentLocal;
    attributes.targetPosition = TargetCenter + nextLocal;
    attributes.velocity = (nextLocal - currentLocal) / 0.02;

    if (lane < 12u)
    {
        attributes.size = ParticleSize * lerp(5.0, 9.0, a) * lerp(0.70, 1.10, flow);
        attributes.alpha *= 0.72 + surge * 0.28;
    }
    else if (lane < 20u)
    {
        attributes.size = radius * lerp(0.24, 0.46, a) * lerp(0.65, 1.25, flow);
    }
    else
    {
        // Per-particle shimmer gives the cyan family energy without synchronized flashes.
        if (StyleId == 2)
            attributes.alpha *= 0.30 + 0.70 * pow(0.5 + 0.5 * sin(EffectTime * (4.0 + c * 4.0) + b * tau), 3.0);
        attributes.alpha *= lerp(0.55, 1.0, c) * (0.85 + surge * 0.15);
    }
}

void PerformerMagicCoreOutput(
    inout VFXAttributes attributes,
    in int StyleId, in int EffectMode, in float EffectTime, in float EffectProgress,
    in float4 PrimaryColor, in float4 SecondaryColor, in float4 AccentColor,
    in float4 SmokeColor, in float Intensity, in float PulseFrequency, in int Seed)
{
    uint lane = attributes.particleId % 64u;
    if (lane < 20u)
    {
        attributes.alive = false;
        return;
    }
    float s = frac((float)(attributes.seed & 0x00FFFFFFu) / 16777216.0 + (float)((uint)Seed & 0x0000FFFFu) / 65536.0);
    float hot = lerp(0.18, 0.90, pow(s, 3.0));
    attributes.color = lerp(SecondaryColor.rgb, PrimaryColor.rgb, hot) * Intensity;
    attributes.scaleX = 1.0;
    attributes.scaleY = StyleId == 0 || StyleId == 2 ? 1.45 : 1.0;
    attributes.alpha *= saturate(Intensity) * (StyleId == 4 ? 0.65 : 0.90);
}

void PerformerMagicGlowOutput(
    inout VFXAttributes attributes,
    in int StyleId, in int EffectMode, in float EffectTime, in float EffectProgress,
    in float4 PrimaryColor, in float4 SecondaryColor, in float4 AccentColor,
    in float4 SmokeColor, in float Intensity, in float PulseFrequency, in int Seed)
{
    uint lane = attributes.particleId % 64u;
    if (lane < 20u || lane % 4u != 0u)
    {
        attributes.alive = false;
        return;
    }
    attributes.color = SecondaryColor.rgb * Intensity;
    attributes.scaleX = 1.0;
    attributes.scaleY = 1.0;
    attributes.size *= 3.5;
    attributes.alpha *= saturate(Intensity) * 0.16;
}

void PerformerMagicWispOutput(
    inout VFXAttributes attributes,
    in int StyleId, in int EffectMode, in float EffectTime, in float EffectProgress,
    in float4 PrimaryColor, in float4 SecondaryColor, in float4 AccentColor,
    in float4 SmokeColor, in float Intensity, in float PulseFrequency, in int Seed)
{
    uint lane = attributes.particleId % 64u;
    if (lane >= 12u)
    {
        attributes.alive = false;
        return;
    }
    float s = frac((float)(attributes.seed & 0x00FFFFFFu) / 16777216.0 + (float)((uint)Seed & 0x0000FFFFu) / 65536.0);
    attributes.color = lerp(SecondaryColor.rgb, AccentColor.rgb, s * 0.18) * Intensity;
    if (StyleId == 0) attributes.color = lerp(attributes.color, PrimaryColor.rgb * Intensity, 0.18);
    attributes.scaleX = lerp(0.75, 1.10, s);
    attributes.scaleY = StyleId == 0 ? 1.65 : 1.25;
    attributes.alpha *= saturate(Intensity) * (StyleId == 0 ? 0.42 : (StyleId == 4 ? 0.20 : 0.32));
}

void PerformerMagicSmokeOutput(
    inout VFXAttributes attributes,
    in int StyleId, in int EffectMode, in float EffectTime, in float EffectProgress,
    in float4 PrimaryColor, in float4 SecondaryColor, in float4 AccentColor,
    in float4 SmokeColor, in float Intensity, in float PulseFrequency, in int Seed)
{
    uint lane = attributes.particleId % 64u;
    if (lane < 12u || lane >= 20u)
    {
        attributes.alive = false;
        return;
    }
    float s = frac((float)(attributes.seed & 0x00FFFFFFu) / 16777216.0 + (float)((uint)Seed & 0x0000FFFFu) / 65536.0);
    attributes.scaleX = 1.0;
    attributes.scaleY = lerp(0.85, 1.20, s);
    attributes.color = StyleId == 0 ? SmokeColor.rgb
        : lerp(SmokeColor.rgb, SecondaryColor.rgb * Intensity, 0.06 + s * 0.06);
    attributes.alpha *= SmokeColor.a * saturate(Intensity) * (StyleId == 0 ? 0.50 : 0.38);
}

