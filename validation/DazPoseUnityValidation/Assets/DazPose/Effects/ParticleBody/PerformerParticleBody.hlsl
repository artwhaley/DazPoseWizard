#include "Assets/DazPose/Effects/Dissolve/Shaders/PerformerDissolveField.hlsl"

void PerformerParticleBodyInitialize(inout VFXAttributes attributes, in float3 SourceCenter, in float SurfaceSeed)
{
    // PositionMesh has already put this particle at its stable surface address.
    // Velocity is deliberately used as persistent per-particle storage; VFX integration is disabled.
    attributes.velocity = attributes.position - SourceCenter;
    attributes.seed = SurfaceSeed;
    attributes.lifetime = 100000000.0;
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

    float3 surfaceWorld = attributes.position;
    float3 surfaceLocal = mul(WorldToLocalMatrix, float4(surfaceWorld, 1.0)).xyz;
    float field = PerformerDissolveField(surfaceLocal, DissolveBoundsMin, DissolveBoundsSize, DissolveFieldParams);

    float3 cloudOffset = attributes.velocity * CloudScale;
    float angle = 6.28318530718 * SwirlTurns * saturate(TransitProgress) + attributes.seed * 6.28318530718;
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

    float wobble = sin(TransitProgress * 6.28318530718 + attributes.seed * 19.0) * TurbulenceStrength;
    float3 turbulence = float3(wobble, sin(wobble * 3.1 + attributes.seed * 7.0), -wobble * 0.7);
    float releaseProgress = max(saturate(DepartureProgress), saturate(DissolveProgress));
    float released = smoothstep(field - 0.035, field + 0.035, releaseProgress);

    if (Phase == Follow)
    {
        attributes.position = surfaceWorld;
        attributes.alpha = 1.0;
    }
    else if (Phase == Departure)
    {
        float3 cloudPosition = SourceCenter + cloudOffset + turbulence;
        attributes.position = lerp(surfaceWorld, cloudPosition, released);
        attributes.alpha = released;
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
        // The mesh reveals when DissolveProgress (1 - MaterializeProgress) falls below
        // this surface address. Particles converge shortly before that same threshold.
        float revealAt = saturate(1.0 - field);
        float arriveBy = max(0.0, revealAt - min(0.04, revealAt * 0.5));
        float approachStart = max(0.0, arriveBy - 0.22);
        float localProgress = smoothstep(approachStart, max(approachStart + 0.001, arriveBy),
            saturate(MaterializeProgress));
        float3 cloudPosition = DestinationCenter + cloudOffset + turbulence;
        attributes.position = lerp(cloudPosition, surfaceWorld, localProgress);
        float fadeEnd = max(revealAt, arriveBy + 0.001);
        attributes.alpha = 1.0 - smoothstep(arriveBy, fadeEnd, saturate(MaterializeProgress));
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
    attributes.size = CoreSize;
}

void PerformerParticleBodyGlowOutput(inout VFXAttributes attributes, in float4 GlowColor, in float GlowSize)
{
    attributes.color = GlowColor.rgb;
    attributes.alpha *= GlowColor.a;
    attributes.size = GlowSize;
}
