#ifndef PERFORMER_DISSOLVE_FIELD_INCLUDED
#define PERFORMER_DISSOLVE_FIELD_INCLUDED
float PerformerDissolveHash3D(float3 point)
{
    point = frac(point * 0.1031);
    point += dot(point, point.yzx + 33.33);
    return frac((point.x + point.y) * point.z);
}
float PerformerDissolveValueNoise3D(float3 point)
{
    float3 cell = floor(point);
    float3 local = frac(point);
    local = local * local * (3.0 - 2.0 * local);
    float c000 = PerformerDissolveHash3D(cell + float3(0,0,0));
    float c100 = PerformerDissolveHash3D(cell + float3(1,0,0));
    float c010 = PerformerDissolveHash3D(cell + float3(0,1,0));
    float c110 = PerformerDissolveHash3D(cell + float3(1,1,0));
    float c001 = PerformerDissolveHash3D(cell + float3(0,0,1));
    float c101 = PerformerDissolveHash3D(cell + float3(1,0,1));
    float c011 = PerformerDissolveHash3D(cell + float3(0,1,1));
    float c111 = PerformerDissolveHash3D(cell + float3(1,1,1));
    float plane0 = lerp(lerp(c000,c100,local.x),lerp(c010,c110,local.x),local.y);
    float plane1 = lerp(lerp(c001,c101,local.x),lerp(c011,c111,local.x),local.y);
    return lerp(plane0,plane1,local.z);
}
float PerformerDissolveField(float3 localPosition, float3 boundsMin, float3 boundsSize, float4 parameters)
{
    float3 safeSize=max(abs(boundsSize),float3(0.0001,0.0001,0.0001));
    float scale=max(abs(parameters.x),0.01);
    float seed=parameters.z;
    float3 p=(localPosition-boundsMin)*scale+float3(seed*13.1,seed*7.7,seed*5.3);
    float noise=PerformerDissolveValueNoise3D(p)*0.55
        +PerformerDissolveValueNoise3D(p*2.03+17.17)*0.30
        +PerformerDissolveValueNoise3D(p*4.01+41.71)*0.15;
    float vertical=saturate((localPosition.y-boundsMin.y)/safeSize.y);
    float field=lerp(noise,vertical,saturate(parameters.y));
    field=saturate((field-0.5)*max(abs(parameters.w),0.05)+0.5);
    return lerp(0.03,0.97,field);
}
void PerformerDissolveEvaluate_float(
    float3 LocalPosition,float3 BoundsMin,float3 BoundsSize,float4 FieldParams,
    float DissolveEnabled,float DissolveProgress,float EdgeWidth,float4 EdgeColor,
    float EdgeEmission,float SourceAlpha,float3 SourceEmission,
    out float ResultAlpha,out float3 ResultEmission,out float Keep)
{
    float enabled=saturate(DissolveEnabled);
    float progress=saturate(DissolveProgress);
    float field=PerformerDissolveField(LocalPosition,BoundsMin,BoundsSize,FieldParams);
    Keep=lerp(1.0,step(progress,field),enabled);
    ResultAlpha=SourceAlpha*Keep;
    float lower=max(abs(EdgeWidth)*0.35,0.0001);
    float upper=max(abs(EdgeWidth),lower+0.0001);
    float band=1.0-smoothstep(lower,upper,abs(field-progress));
    float active=step(0.0001,progress)*(1.0-step(0.9999,progress));
    ResultEmission=SourceEmission+EdgeColor.rgb*max(EdgeEmission,0.0)*band*enabled*active;
}
#endif
