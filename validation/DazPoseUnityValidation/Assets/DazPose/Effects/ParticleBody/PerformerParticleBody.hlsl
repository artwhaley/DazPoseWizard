#include "Assets/DazPose/Effects/Dissolve/Shaders/PerformerDissolveField.hlsl"

void PerformerParticleBodyInitialize(inout VFXAttributes attributes, in float3 SourceCenter, in float SurfaceSeed)
{
    // PositionMesh has already put this particle at its stable surface address.
    // Velocity is deliberately used as persistent per-particle storage; VFX integration is disabled.
    attributes.velocity = attributes.position - SourceCenter;
    // VFX's seed attribute is uint. Preserve the binding's fractional seed in 24 bits.
    attributes.seed = (uint)(saturate(SurfaceSeed) * 16777215.0);
    attributes.lifetime = 100000000.0;
    // Age integration is disabled; use this persistent scalar for the source release field.
    attributes.age = -1.0;
}

void PerformerParticleBodyUpdate(
    inout VFXAttributes attributes,
    in int Phase,
    in float DissolveProgress,
    in float DepartureProgress,
    in float MaterializeProgress,
    in float TransitProgress,
    in float3 SourceCenter,
    in float3 DestinationCenter,
    in float TransitArcHeight,
    in float CloudScale,
    in float SwirlTurns,
    in float TurbulenceStrength,
    in float4 DissolveFieldParams,
    in float3 DissolveBoundsMin,
    in float3 DissolveBoundsSize,
    in float4x4 WorldToLocalMatrix)
{
    const int Follow = 0;
    const int Departure = 1;
    const int Detached = 2;
    const int Transit = 3;
    const int Reform = 4;
    const int Hidden = 5;
    const int StreamDepart = 6;
    const int StreamArrive = 7;
    const int StreamOut = 8;
    const int StreamIn = 9;

    float3 surfaceWorld = attributes.position;
    float3 surfaceLocal = mul(WorldToLocalMatrix, float4(surfaceWorld, 1.0)).xyz;
    float field = PerformerDissolveField(surfaceLocal, DissolveBoundsMin, DissolveBoundsSize, DissolveFieldParams);

    // Seed-based 3D scatter breaks the original body silhouette so every binding joins the swarm.
    float seed = (float)(attributes.seed & 0x00FFFFFFu) / 16777216.0;
    // Embedded Custom HLSL emits only this selected function; keep its scatter math inline.
    float3 seeds = seed + float3(0.173, 0.617, 0.913);
    float3 randomVector = frac(sin(seeds * float3(127.1, 311.7, 74.7)) * 43758.5453) * 2.0 - 1.0;
    float3 randomDirection = normalize(randomVector + float3(0.0001, 0.0001, 0.0001));
    if (Phase == StreamOut)
    {
        // age remains the frozen source-surface dissolve field; velocity remains the
        // frozen binding offset. No phase-6/7 attributes or timing are repurposed.
        if (attributes.age < 0.0) attributes.age = field;
        const float releaseWindow = 0.62;
        float clock = saturate(TransitProgress);
        float releaseAt = saturate(attributes.age) * releaseWindow;
        float3 start = SourceCenter + attributes.velocity;
        float rise = TransitArcHeight * lerp(1.4, 3.0, frac(seed * 17.31));
        float lateralAngle = seed * 6.28318530718 + SwirlTurns * 1.7;
        float lateralDistance = CloudScale * lerp(0.08, 0.28, frac(seed * 31.416));
        float3 lateral = float3(cos(lateralAngle), 0.0, sin(lateralAngle)) * lateralDistance;
        float3 end = start + float3(0.0, rise, 0.0) + lateral;
        float3 bend1 = start + float3(0.0, rise * 0.28, 0.0) + lateral * 0.18;
        float3 bend2 = end - float3(0.0, rise * 0.24, 0.0) + lateral * 0.18;
        float flight = saturate((clock - releaseAt) / max(1.0 - releaseAt, 0.0001));
        float inverseFlight = 1.0 - flight;
        float3 path = inverseFlight * inverseFlight * inverseFlight * start
            + 3.0 * inverseFlight * inverseFlight * flight * bend1
            + 3.0 * inverseFlight * flight * flight * bend2
            + flight * flight * flight * end;
        float envelope = sin(3.14159265359 * flight);
        float flutter = seed * 6.28318530718 + flight * SwirlTurns * 6.28318530718;
        float3 wisp = float3(sin(flutter), cos(flutter * 0.71), sin(flutter * 0.47))
            * (CloudScale * 0.035) + randomDirection * sin(flutter * 1.31) * TurbulenceStrength;
        attributes.position = path + wisp * envelope
            + attributes.direction * (0.004 * (1.0 - smoothstep(0.0, 0.06, flight)));
        float appearAt = max(0.0, releaseAt - 0.055);
        float visible = smoothstep(appearAt, max(appearAt + 0.001, releaseAt - 0.005), clock);
        float fade = smoothstep(0.68, 1.0, flight);
        float shimmer = 0.8 + 0.2 * sin(clock * 29.0 + seed * 47.3);
        attributes.alpha = visible * (1.0 - fade) * shimmer;
        return;
    }
    if (Phase == StreamIn)
    {
        // PositionMesh supplies the live destination binding each update. Reusing age
        // records that binding's field order while pose changes continue to move its end.
        if (attributes.age < 0.0) attributes.age = field;
        float clock = saturate(TransitProgress);
        float arrivalAt = 0.12 + (1.0 - saturate(attributes.age)) * 0.64;
        float flight = saturate(clock / max(arrivalAt, 0.0001));
        float rise = TransitArcHeight * lerp(1.4, 3.0, frac(seed * 17.31));
        float lateralDistance = CloudScale * lerp(0.08, 0.28, frac(seed * 31.416));
        float3 lateral = randomDirection * lateralDistance;
        float3 end = surfaceWorld;
        float3 start = end + float3(0.0, rise, 0.0) + lateral;
        float3 delta = end - start;
        float3 bend1 = start + delta * 0.30 + lateral * 0.18;
        float3 bend2 = end - delta * 0.24 + lateral * 0.12 + float3(0.0, rise * 0.12, 0.0);
        float inverseFlight = 1.0 - flight;
        float3 path = inverseFlight * inverseFlight * inverseFlight * start
            + 3.0 * inverseFlight * inverseFlight * flight * bend1
            + 3.0 * inverseFlight * flight * flight * bend2
            + flight * flight * flight * end;
        float envelope = sin(3.14159265359 * flight);
        float flutter = seed * 6.28318530718 + flight * SwirlTurns * 6.28318530718;
        float3 wisp = float3(sin(flutter), cos(flutter * 0.71), sin(flutter * 0.47))
            * (CloudScale * 0.035) + randomDirection * sin(flutter * 1.31) * TurbulenceStrength;
        attributes.position = path + wisp * envelope;
        float visible = smoothstep(0.0, 0.055, clock);
        float fade = smoothstep(arrivalAt, min(1.0, arrivalAt + 0.23), clock);
        float shimmer = 0.8 + 0.2 * sin(clock * 29.0 + seed * 47.3);
        attributes.alpha = visible * (1.0 - fade) * shimmer;
        return;
    }
    if (Phase == StreamDepart || Phase == StreamArrive)
    {
        // Freeze each source surface address and release time. PositionMesh continues to
        // supply the actual destination binding after the actor is relocated invisibly.
        if (attributes.age < 0.0) attributes.age = field;
        float releaseWindow = max(DepartureProgress, 0.0001);
        float fadeWindow = max(MaterializeProgress, 0.0001);
        float clock = max(TransitProgress, 0.0);
        float releaseAt = attributes.age * releaseWindow;
        float destinationBlend = Phase == StreamArrive ? smoothstep(releaseWindow, 1.0, clock) : 0.0;
        float destinationField = lerp(attributes.age, field, destinationBlend);
        // Destination patches reform in the same order as the native mesh reveal.
        // The extra flight interval guarantees arrivals occur after hidden relocation.
        float arrivalAt = 1.0 + (1.0 - destinationField) * releaseWindow;
        float flight = saturate((clock - releaseAt) / max(arrivalAt - releaseAt, 0.0001));
        float3 start = SourceCenter + attributes.velocity;
        float3 predictedEnd = DestinationCenter + attributes.velocity;
        float3 end = lerp(predictedEnd, surfaceWorld, destinationBlend);
        float3 delta = end - start;
        float3 side = cross(float3(0.0, 1.0, 0.0), delta);
        side = dot(side, side) > 0.0001 ? normalize(side) : float3(1.0, 0.0, 0.0);
        float strand = sin(seed * 91.7) * CloudScale * 0.45;
        float rise = TransitArcHeight * lerp(0.65, 1.8, frac(seed * 17.31));
        float3 bend1 = start + delta * 0.28 + side * strand + float3(0.0, rise, 0.0);
        float3 bend2 = end - delta * 0.28 + side * strand * 0.6 + float3(0.0, rise * 0.8, 0.0);
        float inverseFlight = 1.0 - flight;
        float3 path = inverseFlight * inverseFlight * inverseFlight * start
            + 3.0 * inverseFlight * inverseFlight * flight * bend1
            + 3.0 * inverseFlight * flight * flight * bend2
            + flight * flight * flight * end;
        float envelope = sin(3.14159265359 * flight);
        float flutter = seed * 6.28318530718 + flight * SwirlTurns * 6.28318530718;
        // Small individual curls ride the arcing strands; they never orbit a common ball.
        float3 wisp = side * sin(flutter) * CloudScale * 0.16
            + float3(0.0, cos(flutter * 0.83), 0.0) * CloudScale * 0.09
            + randomDirection * sin(flutter * 1.37) * TurbulenceStrength;
        // The lead-in lights each surface patch before it dissolves. A tiny normal
        // offset makes those waiting embers visible above the opaque skin, then eases
        // away as they depart so the established flight and landing positions stay exact.
        float surfaceLift = 0.006 * (1.0 - smoothstep(0.0, 0.08, flight));
        attributes.position = path + wisp * envelope + attributes.direction * surfaceLift;
        float appearAt = max(0.0, releaseAt - releaseWindow * 0.22);
        float visible = smoothstep(appearAt, appearAt + releaseWindow * 0.07, clock);
        float fade = smoothstep(arrivalAt + fadeWindow * 0.2, arrivalAt + fadeWindow, clock);
        float shimmer = 0.78 + 0.22 * sin(clock * 27.0 + seed * 43.7);
        attributes.alpha = visible * (1.0 - fade) * shimmer;
        return;
    }

    float scatterRadius = CloudScale * lerp(0.35, 1.0, frac(seed * 31.416));
    float3 cloudOffset = attributes.velocity * (CloudScale * 0.20) + randomDirection * scatterRadius;
    float swarmProgress = saturate(DepartureProgress) + saturate(TransitProgress);
    float angle = 6.28318530718 * SwirlTurns * swarmProgress + seed * 6.28318530718;
    float sine;
    float cosine;
    sincos(angle, sine, cosine);
    float2 rotatedXZ = float2(
        cloudOffset.x * cosine - cloudOffset.z * sine,
        cloudOffset.x * sine + cloudOffset.z * cosine);
    cloudOffset.x = rotatedXZ.x;
    cloudOffset.z = rotatedXZ.y;
    float resolveCloud = smoothstep(0.75, 1.0, saturate(TransitProgress));
    cloudOffset *= lerp(1.0, 0.82, resolveCloud);

    float motionPhase = swarmProgress * 6.28318530718 + seed * 19.0;
    float3 turbulence = float3(
        sin(motionPhase * 1.13),
        sin(motionPhase * 0.83 + 2.1),
        cos(motionPhase * 1.27 + 4.2)) * TurbulenceStrength;
    float releaseProgress = saturate(DissolveProgress);
    float released = smoothstep(field - 0.035, field + 0.035, releaseProgress);

    if (Phase == Follow)
    {
        attributes.position = surfaceWorld;
        attributes.alpha = 1.0;
    }
    else if (Phase == Departure)
    {
        float3 cloudPosition = SourceCenter + cloudOffset + turbulence;
        // Keep every revealed particle on its surface address through mesh replacement
        // and a full-body hold. Only then let the complete body break into a swarm.
        float scatter = smoothstep(0.70, 1.0, saturate(DepartureProgress));
        attributes.position = lerp(surfaceWorld, cloudPosition, scatter);
        attributes.alpha = lerp(released, 1.0, step(1.0, releaseProgress));
    }
    else if (Phase == Detached)
    {
        attributes.position = SourceCenter + cloudOffset + turbulence;
        attributes.alpha = 1.0;
    }
    else if (Phase == Transit)
    {
        float progress = saturate(TransitProgress);
        float eased = smoothstep(0.0, 1.0, progress);
        float3 center = lerp(SourceCenter, DestinationCenter, eased);
        center.y += TransitArcHeight * 4.0 * progress * (1.0 - progress);
        attributes.position = center + cloudOffset + turbulence;
        attributes.alpha = 1.0;
    }
    else if (Phase == Reform)
    {
        // All particles reach Lara's surface by 60% progress. Hold that formed cloud until
        // 75%, then fade it only as the matching native mesh dissolve reveals underneath.
        float localProgress = smoothstep(0.0, 0.60, saturate(MaterializeProgress));
        float3 cloudPosition = DestinationCenter + cloudOffset + turbulence;
        attributes.position = lerp(cloudPosition, surfaceWorld, localProgress);
        attributes.alpha = 1.0 - smoothstep(0.75, 1.0, saturate(MaterializeProgress));
    }
    else if (Phase == Hidden)
    {
        attributes.position = surfaceWorld;
        attributes.alpha = 0.0;
    }
}

void PerformerParticleBodyCoreOutput(inout VFXAttributes attributes, in float4 CoreColor, in float CoreSize)
{
    attributes.color = CoreColor.rgb;
    attributes.alpha *= CoreColor.a;
    float emberSeed = (float)(attributes.seed & 0x00FFFFFFu) / 16777216.0;
    attributes.size = CoreSize * lerp(0.65, 1.0, frac(emberSeed * 53.13));
}

void PerformerParticleBodyGlowOutput(inout VFXAttributes attributes, in float4 GlowColor, in float GlowSize)
{
    attributes.color = GlowColor.rgb;
    attributes.alpha *= GlowColor.a;
    float emberSeed = (float)(attributes.seed & 0x00FFFFFFu) / 16777216.0;
    attributes.size = GlowSize * lerp(0.65, 1.0, frac(emberSeed * 53.13));
}
