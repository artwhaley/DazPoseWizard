float PerformerMagicHash(float value)
{
    return frac(sin(value * 12.9898 + 78.233) * 43758.5453);
}

void PerformerMagicInitialize(
    inout VFXAttributes attributes,
    in int StyleId,
    in int EffectMode,
    in float3 TargetCenter,
    in float TargetRadius,
    in float TargetHeight,
    in float ParticleSize,
    in float ParticleLifetime,
    in int Seed)
{
    float particleSeed = frac((float)(attributes.seed & 0x00FFFFFFu) / 16777216.0 + Seed * 0.00317);
    float x = (PerformerMagicHash(particleSeed * 71.3 + 1.1) * 2.0 - 1.0) * TargetRadius;
    float y = (PerformerMagicHash(particleSeed * 31.7 + 4.2) * 2.0 - 1.0) * TargetHeight * 0.43;
    float z = (PerformerMagicHash(particleSeed * 89.9 + 8.4) * 2.0 - 1.0) * TargetRadius;

    if (StyleId == 3) // Verdant Pulse begins at the floor ring.
        y = -TargetHeight * 0.43 + PerformerMagicHash(particleSeed * 11.7) * 0.10;
    else if (StyleId == 0 && particleSeed > 0.82) // A slower stratum becomes soft smoke.
        y = -TargetHeight * 0.32 + PerformerMagicHash(particleSeed * 41.1) * TargetHeight * 0.12;

    attributes.velocity = float3(x, y, z); // Persistent random offset; graph integration is disabled.
    attributes.position = TargetCenter + attributes.velocity;
    attributes.lifetime = max(0.1, ParticleLifetime);
    attributes.age = 0.0;
    attributes.size = ParticleSize * lerp(0.65, 1.35, PerformerMagicHash(particleSeed * 17.9));
    attributes.angle = float3(0.0, 0.0, particleSeed * 6.2831853);
    attributes.color = float3(1.0, 1.0, 1.0);
    attributes.alpha = 1.0;
}

void PerformerMagicUpdate(
    inout VFXAttributes attributes,
    in int StyleId,
    in int EffectMode,
    in float EffectTime,
    in float EffectProgress,
    in float3 TargetCenter,
    in float TargetRadius,
    in float TargetHeight,
    in float RiseSpeed,
    in float SwirlStrength,
    in float Turbulence,
    in float PulseFrequency,
    in int Seed)
{
    float seed = frac((float)(attributes.seed & 0x00FFFFFFu) / 16777216.0 + Seed * 0.00317);
    float life = saturate(attributes.age / max(attributes.lifetime, 0.1));
    float angle = seed * 6.2831853;
    float3 offset = attributes.velocity;
    float3 position = TargetCenter;

    if (StyleId == 0) // Emberfire: licking upward motion, turbulence, distinct smoke drift.
    {
        bool smoke = seed > 0.82;
        float flicker = EffectTime * (smoke ? 1.8 : 8.0) + angle;
        float lateral = Turbulence * (smoke ? 1.2 : 0.65);
        position += offset * lerp(1.0, 0.45, life);
        position += float3(sin(flicker) * lateral, life * RiseSpeed * (smoke ? 0.52 : 1.0),
            cos(flicker * 0.73) * lateral);
        attributes.angle = float3(0.0, 0.0, angle + sin(flicker) * 0.35);
    }
    else if (StyleId == 1) // Rift Bloom: curling magenta wisps around the target volume.
    {
        float orbit = angle + EffectTime * max(0.2, SwirlStrength) * 1.8 + life * 2.1;
        float radius = TargetRadius * lerp(0.95, 0.48, life);
        position += float3(cos(orbit) * radius + offset.x * 0.18,
            offset.y * 0.35 + life * RiseSpeed * 0.55,
            sin(orbit) * radius + offset.z * 0.18);
        float curl = sin(orbit * 1.7 + seed * 19.0) * Turbulence * 0.35;
        position += float3(curl, cos(orbit * 1.3) * curl, -curl);
        attributes.angle = float3(0.0, 0.0, orbit);
    }
    else if (StyleId == 2) // Arc Cyan: short, fast, jagged GPU streaks.
    {
        float crackle = EffectTime * (13.0 + PulseFrequency * 4.0) + angle;
        float snap = sin(floor(crackle * 2.0) * 7.13 + seed * 23.0);
        position += offset * 0.68;
        position += float3(sin(crackle * 1.4) * Turbulence + snap * 0.045,
            cos(crackle * 1.9) * Turbulence * 0.55,
            cos(crackle * 1.2) * Turbulence + snap * 0.045);
        position += float3(0.0, life * RiseSpeed * 0.2, 0.0);
        attributes.angle = float3(0.0, 0.0, crackle + snap * 0.4);
    }
    else if (StyleId == 3) // Verdant Pulse: outward energy ring with rising organic motes.
    {
        float2 direction = normalize(offset.xz + float2(0.0001, 0.0001));
        float radial = TargetRadius * (0.12 + life * 1.25);
        float wobble = sin(life * 9.0 + seed * 39.0) * Turbulence * 0.25;
        position += float3(direction.x * (radial + wobble), offset.y * 0.12 + life * RiseSpeed * 0.45,
            direction.y * (radial + wobble));
        attributes.angle = float3(0.0, 0.0, atan2(direction.y, direction.x));
    }
    else // Violet Serenity: slow, smooth orbit and gentle breathing drift.
    {
        float orbit = angle + EffectTime * max(0.1, SwirlStrength) * 0.34 + life * 0.6;
        float radius = TargetRadius * lerp(0.74, 0.56, life);
        float breathing = sin(EffectTime * max(0.1, PulseFrequency) * 0.8 + seed * 6.2831853) * 0.08;
        position += float3(cos(orbit) * (radius + breathing),
            offset.y * 0.48 + sin(orbit * 0.55) * Turbulence * 0.18 + life * RiseSpeed * 0.16,
            sin(orbit) * (radius + breathing));
        attributes.angle = float3(0.0, 0.0, orbit);
    }

    attributes.position = position;
    if (StyleId == 2)
    {
        float sparkAngle = seed * 6.2831853 + EffectTime * (18.0 + PulseFrequency * 5.0);
        float3 lineDirection = normalize(float3(cos(sparkAngle), sin(sparkAngle * 1.7) * 0.65, sin(sparkAngle)));
        float lineLength = TargetRadius * lerp(0.10, 0.38, PerformerMagicHash(seed * 29.0));
        attributes.targetPosition = position + lineDirection * lineLength;
    }
    else attributes.targetPosition = position;
}

void PerformerMagicApplyOutput(
    inout VFXAttributes attributes,
    in int StyleId,
    in int EffectMode,
    in float EffectTime,
    in float EffectProgress,
    in float4 PrimaryColor,
    in float4 SecondaryColor,
    in float4 AccentColor,
    in float4 SmokeColor,
    in float Intensity,
    in float PulseFrequency,
    in int Seed,
    in float SizeScale,
    in float AlphaScale)
{
    float seed = frac((float)(attributes.seed & 0x00FFFFFFu) / 16777216.0 + Seed * 0.00317);
    float life = saturate(attributes.age / max(attributes.lifetime, 0.1));
    float3 color = lerp(PrimaryColor.rgb, SecondaryColor.rgb, PerformerMagicHash(seed * 13.9));
    float alpha = 1.0;

    if (StyleId == 0)
    {
        if (seed > 0.82)
        {
            color = SmokeColor.rgb;
            alpha *= SmokeColor.a * 0.52;
            attributes.size *= 1.8;
        }
        else
        {
            color = lerp(PrimaryColor.rgb, SecondaryColor.rgb, life * 0.72 + PerformerMagicHash(seed * 27.0) * 0.2);
            color = lerp(color, AccentColor.rgb, life * 0.42);
            alpha *= 0.82 + 0.18 * sin(EffectTime * 21.0 + seed * 49.0);
        }
    }
    else if (StyleId == 1)
    {
        color = lerp(PrimaryColor.rgb, SecondaryColor.rgb, 0.25 + 0.6 * PerformerMagicHash(seed * 31.0));
        color = lerp(color, AccentColor.rgb, 0.12 + 0.25 * life);
    }
    else if (StyleId == 2)
    {
        float flash = step(0.58, PerformerMagicHash(floor(EffectTime * (14.0 + seed * 18.0)) + seed * 91.0));
        color = lerp(SecondaryColor.rgb, PrimaryColor.rgb, flash * 0.8);
        color = lerp(color, AccentColor.rgb, 0.15 + 0.25 * PerformerMagicHash(seed * 83.0));
        alpha *= flash > 0.0 ? 1.0 : 0.42;
        attributes.size *= 0.8;
    }
    else if (StyleId == 3)
    {
        color = lerp(SecondaryColor.rgb, AccentColor.rgb, 0.18 + 0.38 * PerformerMagicHash(seed * 53.0));
        alpha *= 0.82 + 0.18 * sin(EffectTime * PulseFrequency * 2.0 + seed * 6.2831853);
    }
    else
    {
        color = lerp(SecondaryColor.rgb, PrimaryColor.rgb, 0.32 + 0.3 * PerformerMagicHash(seed * 17.0));
        alpha *= 0.76 + 0.12 * sin(EffectTime * PulseFrequency * 0.75 + seed * 6.2831853);
    }

    float particleIn = smoothstep(0.0, 0.06, life);
    float particleOut = 1.0 - smoothstep(0.82, 1.0, life);
    float eventEnvelope = 1.0;
    if (EffectMode == 0)
        eventEnvelope = smoothstep(0.0, 0.06, EffectProgress)
            * (1.0 - smoothstep(0.80, 1.0, EffectProgress));
    attributes.color = color * Intensity;
    attributes.alpha *= alpha * particleIn * particleOut * eventEnvelope * AlphaScale;
    attributes.size *= SizeScale;
}

void PerformerMagicCoreOutput(
    inout VFXAttributes attributes,
    in int StyleId,
    in int EffectMode,
    in float EffectTime,
    in float EffectProgress,
    in float4 PrimaryColor,
    in float4 SecondaryColor,
    in float4 AccentColor,
    in float4 SmokeColor,
    in float Intensity,
    in float PulseFrequency,
    in int Seed)
{
    PerformerMagicApplyOutput(attributes, StyleId, EffectMode, EffectTime, EffectProgress,
        PrimaryColor, SecondaryColor, AccentColor, SmokeColor, Intensity, PulseFrequency,
        Seed, 0.68, 0.86);
}

void PerformerMagicGlowOutput(
    inout VFXAttributes attributes,
    in int StyleId,
    in int EffectMode,
    in float EffectTime,
    in float EffectProgress,
    in float4 PrimaryColor,
    in float4 SecondaryColor,
    in float4 AccentColor,
    in float4 SmokeColor,
    in float Intensity,
    in float PulseFrequency,
    in int Seed)
{
    PerformerMagicApplyOutput(attributes, StyleId, EffectMode, EffectTime, EffectProgress,
        PrimaryColor, SecondaryColor, AccentColor, SmokeColor, Intensity, PulseFrequency,
        Seed, 2.6, 0.3);
}

void PerformerMagicLineOutput(
    inout VFXAttributes attributes,
    in int StyleId,
    in int EffectMode,
    in float EffectTime,
    in float4 PrimaryColor,
    in float4 SecondaryColor,
    in float Intensity,
    in float PulseFrequency,
    in int Seed)
{
    if (StyleId != 2)
    {
        attributes.alpha = 0.0;
        return;
    }

    float seed = frac((float)(attributes.seed & 0x00FFFFFFu) / 16777216.0 + Seed * 0.00317);
    float flicker = step(0.38, PerformerMagicHash(floor(EffectTime * (18.0 + PulseFrequency * 8.0)) + seed * 73.0));
    attributes.color = lerp(SecondaryColor.rgb, PrimaryColor.rgb, 0.28 + 0.55 * flicker);
    attributes.alpha *= Intensity * (EffectMode == 0 ? 0.92 : 0.64) * flicker;
    attributes.size = lerp(0.004, 0.012, PerformerMagicHash(seed * 61.0));
}
