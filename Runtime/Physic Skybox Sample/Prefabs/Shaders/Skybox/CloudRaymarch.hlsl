#ifndef CLOUD_RAYMARCH_INCLUDED
#define CLOUD_RAYMARCH_INCLUDED


// ============================================================
// CLOUD RAYMARCH
// Designed for Unity URP / Shader Graph Custom Function
//
// Features:
// - Horizontal cylindrical cloud container
// - 3D noise
// - 2D coverage map
// - 2D height/type map
// - Wind movement
// - Beer-Lambert extinction
// - Henyey-Greenstein phase function
// - Approximate sun light march
// - Early ray termination
// ============================================================


// ------------------------------------------------------------
// Constants
// ------------------------------------------------------------

#define CLOUD_PI 3.14159265359


// ------------------------------------------------------------
// Henyey-Greenstein phase function
// g > 0  : forward scattering
// g < 0  : backward scattering
// g = 0  : isotropic
// ------------------------------------------------------------

float CloudHG(float cosTheta, float g)
{
    float g2 = g * g;

    float denominator =
        1.0 +
        g2 -
        2.0 * g * cosTheta;

    denominator = max(denominator, 0.0001);

    return
        (1.0 - g2) /
        (4.0 * CLOUD_PI * pow(denominator, 1.5));
}


// ------------------------------------------------------------
// Beer-Lambert transmission
// ------------------------------------------------------------

float CloudBeer(float density, float absorption, float stepSize)
{
    return exp(-density * absorption * stepSize);
}


// ------------------------------------------------------------
// Sample 3D cloud noise
// ------------------------------------------------------------

float CloudSampleNoise(
    float3 worldPosition,
    float cloudScale,
    float3 windOffset,
    Texture3D noiseTexture,
    SamplerState noiseTextureSampler)
{
    float3 uvw =
        worldPosition * cloudScale +
        windOffset;

    return SAMPLE_TEXTURE3D(
        noiseTexture,
        noiseTextureSampler,
        uvw
    ).r;
}


// ------------------------------------------------------------
// Coverage map
//
// XZ world position is converted to 0-1 UVs.
// ------------------------------------------------------------

float CloudSampleCoverage(
    float3 worldPosition,
    float3 cloudCenter,
    float cloudRadius,
    Texture2D coverageTexture,
    SamplerState coverageTextureSampler)
{
    float2 uv =
        (worldPosition.xz - cloudCenter.xz) /
        (cloudRadius * 2.0) +
        0.5;

    // Outside the container = no cloud
    if (uv.x < 0.0 ||
        uv.x > 1.0 ||
        uv.y < 0.0 ||
        uv.y > 1.0)
    {
        return 0.0;
    }

    return SAMPLE_TEXTURE2D(
        coverageTexture,
        coverageTextureSampler,
        uv
    ).r;
}


// ------------------------------------------------------------
// Height / cloud type map
//
// R channel controls local cloud type / vertical shape.
// ------------------------------------------------------------

float CloudSampleHeightMap(
    float3 worldPosition,
    float3 cloudCenter,
    float cloudRadius,
    Texture2D heightTexture,
    SamplerState heightTextureSampler)
{
    float2 uv =
        (worldPosition.xz - cloudCenter.xz) /
        (cloudRadius * 2.0) +
        0.5;

    if (uv.x < 0.0 ||
        uv.x > 1.0 ||
        uv.y < 0.0 ||
        uv.y > 1.0)
    {
        return 0.0;
    }

    return SAMPLE_TEXTURE2D(
        heightTexture,
        heightTextureSampler,
        uv
    ).r;
}


// ------------------------------------------------------------
// Vertical cloud profile
//
// Creates a soft cloud base and soft cloud top.
//
// type controls the vertical distribution.
// ------------------------------------------------------------

float CloudVerticalProfile(
    float height01,
    float cloudType)
{
    // Prevent completely flat clouds.

    float baseSoftness =
        lerp(0.08, 0.22, cloudType);

    float topSoftness =
        lerp(0.12, 0.35, cloudType);

    float bottom =
        smoothstep(
            0.0,
            baseSoftness,
            height01
        );

    float top =
        1.0 -
        smoothstep(
            1.0 - topSoftness,
            1.0,
            height01
        );

    float profile =
        bottom * top;

    // Move density upward/downward depending on cloud type.

    float center =
        lerp(0.45, 0.62, cloudType);

    float verticalShape =
        1.0 -
        abs(height01 - center) * 1.5;

    verticalShape =
        saturate(verticalShape);

    return profile * verticalShape;
}


// ------------------------------------------------------------
// Full density function
// ------------------------------------------------------------

float CloudDensity(
    float3 worldPosition,

    float cloudBottom,
    float cloudTop,

    float cloudScale,
    float cloudDensityMultiplier,
    float cloudCoverage,

    float detailStrength,

    float3 windOffset,

    float3 cloudCenter,
    float cloudRadius,

    Texture3D noiseTexture,
    SamplerState noiseTextureSampler,

    Texture2D coverageTexture,
    SamplerState coverageTextureSampler,

    Texture2D heightTexture,
    SamplerState heightTextureSampler)
{
    // --------------------------------------------------------
    // Vertical coordinate
    // --------------------------------------------------------

    float height01 =
        saturate(
            (worldPosition.y - cloudBottom) /
            max(cloudTop - cloudBottom, 0.001)
        );


    // --------------------------------------------------------
    // Coverage
    // --------------------------------------------------------

    float coverage =
        CloudSampleCoverage(
            worldPosition,
            cloudCenter,
            cloudRadius,
            coverageTexture,
            coverageTextureSampler
        );

    // Artist controlled coverage.
    coverage =
        smoothstep(
            1.0 - cloudCoverage,
            1.0,
            coverage
        );


    // --------------------------------------------------------
    // Cloud type / height
    // --------------------------------------------------------

    float cloudType =
        CloudSampleHeightMap(
            worldPosition,
            cloudCenter,
            cloudRadius,
            heightTexture,
            heightTextureSampler
        );


    // --------------------------------------------------------
    // Main 3D shape
    // --------------------------------------------------------

    float noise =
        CloudSampleNoise(
            worldPosition,
            cloudScale,
            windOffset,
            noiseTexture,
            noiseTextureSampler
        );


    // --------------------------------------------------------
    // Detail noise
    //
    // Uses the same 3D texture but at a higher frequency.
    // This avoids needing another Texture3D.
    // --------------------------------------------------------

    float3 detailPosition =
        worldPosition * (cloudScale * 4.0) +
        windOffset * 1.7;

    float detail =
        SAMPLE_TEXTURE3D(
            noiseTexture,
            noiseTextureSampler,
            detailPosition
        ).r;


    // --------------------------------------------------------
    // Erode the main shape with detail
    // --------------------------------------------------------

    noise =
        noise -
        detail * detailStrength;


    // --------------------------------------------------------
    // Density threshold
    // --------------------------------------------------------

    float shape =
        smoothstep(
            0.35,
            0.65,
            noise
        );


    // --------------------------------------------------------
    // Vertical profile
    // --------------------------------------------------------

    float verticalProfile =
        CloudVerticalProfile(
            height01,
            cloudType
        );


    // --------------------------------------------------------
    // Final density
    // --------------------------------------------------------

    float density =
        shape *
        coverage *
        verticalProfile;

    density *= cloudDensityMultiplier;

    return saturate(density);
}


// ------------------------------------------------------------
// Cylinder intersection
//
// Cloud container:
//
//          cloudTop
//  =======================
//
//          CLOUDS
//
//  =======================
//          cloudBottom
//
// Radius limits the horizontal area.
// ------------------------------------------------------------

bool CloudIntersectContainer(
    float3 rayOrigin,
    float3 rayDirection,

    float3 cloudCenter,
    float cloudRadius,

    float cloudBottom,
    float cloudTop,

    out float tEnter,
    out float tExit)
{
    tEnter = 0.0;
    tExit = 1000000.0;


    // --------------------------------------------------------
    // Y slab
    // --------------------------------------------------------

    if (abs(rayDirection.y) < 0.00001)
    {
        // Ray parallel to cloud layer.

        if (rayOrigin.y < cloudBottom ||
            rayOrigin.y > cloudTop)
        {
            return false;
        }
    }
    else
    {
        float t0 =
            (cloudBottom - rayOrigin.y) /
            rayDirection.y;

        float t1 =
            (cloudTop - rayOrigin.y) /
            rayDirection.y;

        if (t0 > t1)
        {
            float temp = t0;
            t0 = t1;
            t1 = temp;
        }

        tEnter = max(tEnter, t0);
        tExit = min(tExit, t1);
    }


    // --------------------------------------------------------
    // XZ cylinder
    // --------------------------------------------------------

    float3 originXZ =
        rayOrigin - cloudCenter;

    float2 ro =
        originXZ.xz;

    float2 rd =
        rayDirection.xz;

    float A =
        dot(rd, rd);

    float B =
        2.0 * dot(ro, rd);

    float C =
        dot(ro, ro) -
        cloudRadius * cloudRadius;


    if (A > 0.000001)
    {
        float discriminant =
            B * B -
            4.0 * A * C;

        if (discriminant < 0.0)
        {
            return false;
        }

        float sqrtDiscriminant =
            sqrt(discriminant);

        float tc0 =
            (-B - sqrtDiscriminant) /
            (2.0 * A);

        float tc1 =
            (-B + sqrtDiscriminant) /
            (2.0 * A);

        if (tc0 > tc1)
        {
            float temp = tc0;
            tc0 = tc1;
            tc1 = temp;
        }

        tEnter =
            max(tEnter, tc0);

        tExit =
            min(tExit, tc1);
    }
    else
    {
        // Ray almost vertical.
        // If outside radius, reject.

        if (dot(ro, ro) >
            cloudRadius * cloudRadius)
        {
            return false;
        }
    }


    if (tExit <= tEnter)
        return false;

    if (tExit < 0.0)
        return false;


    tEnter =
        max(tEnter, 0.0);

    return true;
}


// ------------------------------------------------------------
// Approximate sunlight transmittance
//
// IMPORTANT:
//
// This is deliberately small.
// We do NOT perform a 32-step secondary raymarch.
//
// Only a few samples are used.
// ------------------------------------------------------------

float CloudLightTransmittance(
    float3 position,
    float3 sunDirection,

    float lightStepSize,
    int lightSteps,

    float cloudBottom,
    float cloudTop,

    float cloudScale,
    float cloudDensityMultiplier,
    float cloudCoverage,

    float detailStrength,

    float3 windOffset,

    float3 cloudCenter,
    float cloudRadius,

    Texture3D noiseTexture,
    SamplerState noiseTextureSampler,

    Texture2D coverageTexture,
    SamplerState coverageTextureSampler,

    Texture2D heightTexture,
    SamplerState heightTextureSampler)
{
    float transmittance = 1.0;

    float3 samplePosition =
        position;

    [unroll]
    for (int i = 0; i < 8; i++)
    {
        if (i >= lightSteps)
            break;

        samplePosition +=
            sunDirection *
            lightStepSize;

        float density =
            CloudDensity(
                samplePosition,

                cloudBottom,
                cloudTop,

                cloudScale,
                cloudDensityMultiplier,
                cloudCoverage,

                detailStrength,

                windOffset,

                cloudCenter,
                cloudRadius,

                noiseTexture,
                noiseTextureSampler,

                coverageTexture,
                coverageTextureSampler,

                heightTexture,
                heightTextureSampler
            );

        transmittance *=
            CloudBeer(
                density,
                1.0,
                lightStepSize
            );

        if (transmittance < 0.01)
            break;
    }

    return saturate(transmittance);
}


// ============================================================
// MAIN FUNCTION
// ============================================================

void CloudRaymarch_float(

    // --------------------------------------------------------
    // Camera
    // --------------------------------------------------------

    float3 RayOrigin,
    float3 RayDirection,


    // --------------------------------------------------------
    // Cloud container
    // --------------------------------------------------------

    float3 CloudCenter,
    float CloudRadius,

    float CloudBottom,
    float CloudTop,


    // --------------------------------------------------------
    // Cloud shape
    // --------------------------------------------------------

    float CloudScale,
    float CloudDensityMultiplier,
    float CloudCoverage,
    float CloudDetailStrength,


    // --------------------------------------------------------
    // Wind
    // --------------------------------------------------------

    float3 WindDirection,
    float WindSpeed,
    float Time,


    // --------------------------------------------------------
    // Noise
    // --------------------------------------------------------

    Texture3D NoiseTexture,
    SamplerState NoiseTextureSampler,


    // --------------------------------------------------------
    // Coverage
    // --------------------------------------------------------

    Texture2D CoverageTexture,
    SamplerState CoverageTextureSampler,


    // --------------------------------------------------------
    // Height / Type
    // --------------------------------------------------------

    Texture2D HeightTexture,
    SamplerState HeightTextureSampler,


    // --------------------------------------------------------
    // Sun
    // --------------------------------------------------------

    float3 SunDirection,
    float3 SunColor,
    float SunIntensity,


    // --------------------------------------------------------
    // Lighting
    // --------------------------------------------------------

    float Absorption,
    float PhaseG,

    int RaymarchSteps,
    float StepSize,

    int LightSteps,
    float LightStepSize,


    // --------------------------------------------------------
    // Outputs
    // --------------------------------------------------------

    out float3 CloudColor,
    out float CloudAlpha,
    out float CloudTransmittance
)
{
    // --------------------------------------------------------
    // Defaults
    // --------------------------------------------------------

    CloudColor = 0.0;
    CloudAlpha = 0.0;
    CloudTransmittance = 1.0;


    // --------------------------------------------------------
    // Normalize directions
    // --------------------------------------------------------

    float3 rayDir =
        normalize(RayDirection);

    float3 sunDir =
        normalize(SunDirection);


    // --------------------------------------------------------
    // Container intersection
    // --------------------------------------------------------

    float tEnter;
    float tExit;

    bool hit =
        CloudIntersectContainer(
            RayOrigin,
            rayDir,

            CloudCenter,
            CloudRadius,

            CloudBottom,
            CloudTop,

            tEnter,
            tExit
        );

    if (!hit)
        return;


    // --------------------------------------------------------
    // Limit ray distance
    //
    // Prevent huge horizon rays.
    // --------------------------------------------------------

    tExit =
        min(
            tExit,
            tEnter +
            StepSize *
            RaymarchSteps
        );


    float rayLength =
        tExit - tEnter;

    if (rayLength <= 0.0)
        return;


    // --------------------------------------------------------
    // Wind
    // --------------------------------------------------------

    float3 windOffset =
        WindDirection *
        WindSpeed *
        Time;


    // --------------------------------------------------------
    // Step size
    //
    // If StepSize <= 0, automatically distribute samples.
    // --------------------------------------------------------

    float actualStepSize =
        StepSize;

    if (actualStepSize <= 0.0)
    {
        actualStepSize =
            rayLength /
            max((float)RaymarchSteps, 1.0);
    }


    // --------------------------------------------------------
    // Start position
    // --------------------------------------------------------

    float t =
        tEnter;

    float3 currentPosition =
        RayOrigin +
        rayDir * t;


    // --------------------------------------------------------
    // View / sun phase
    // --------------------------------------------------------

    float cosTheta =
        dot(
            rayDir,
            sunDir
        );

    float phase =
        CloudHG(
            cosTheta,
            PhaseG
        );


    // --------------------------------------------------------
    // Ambient component
    //
    // Small intentionally.
    // Your skybox supplies the actual ambient color.
    // --------------------------------------------------------

    float3 ambient =
        float3(
            0.35,
            0.45,
            0.55
        );


    // --------------------------------------------------------
    // Main accumulation
    // --------------------------------------------------------

    float transmittance =
        1.0;

    float3 radiance =
        0.0;


    // --------------------------------------------------------
    // RAYMARCH
    // --------------------------------------------------------

    [loop]
    for (int step = 0;
         step < 128;
         step++)
    {
        if (step >= RaymarchSteps)
            break;


        // ----------------------------------------------------
        // Density
        // ----------------------------------------------------

        float density =
            CloudDensity(
                currentPosition,

                CloudBottom,
                CloudTop,

                CloudScale,
                CloudDensityMultiplier,
                CloudCoverage,

                CloudDetailStrength,

                windOffset,

                CloudCenter,
                CloudRadius,

                NoiseTexture,
                NoiseTextureSampler,

                CoverageTexture,
                CoverageTextureSampler,

                HeightTexture,
                HeightTextureSampler
            );


        // ----------------------------------------------------
        // Only perform lighting where there is cloud.
        // ----------------------------------------------------

        if (density > 0.001)
        {
            // ------------------------------------------------
            // Sun transmission
            // ------------------------------------------------

            float sunTransmission =
                CloudLightTransmittance(
                    currentPosition,
                    sunDir,

                    LightStepSize,
                    LightSteps,

                    CloudBottom,
                    CloudTop,

                    CloudScale,
                    CloudDensityMultiplier,
                    CloudCoverage,

                    CloudDetailStrength,

                    windOffset,

                    CloudCenter,
                    CloudRadius,

                    NoiseTexture,
                    NoiseTextureSampler,

                    CoverageTexture,
                    CoverageTextureSampler,

                    HeightTexture,
                    HeightTextureSampler
                );


            // ------------------------------------------------
            // Beer-Lambert for this camera sample
            // ------------------------------------------------

            float localTransmission =
                CloudBeer(
                    density,
                    Absorption,
                    actualStepSize
                );


            // Alpha contribution of this sample.
            float sampleAlpha =
                1.0 -
                localTransmission;


            // ------------------------------------------------
            // Sun lighting
            // ------------------------------------------------

            float3 directLight =
                SunColor *
                SunIntensity *
                sunTransmission;


            // ------------------------------------------------
            // Phase scattering
            // ------------------------------------------------

            directLight *= phase;


            // ------------------------------------------------
            // Ambient sky contribution
            // ------------------------------------------------

            float3 lighting =
                directLight +
                ambient * 0.15;


            // ------------------------------------------------
            // Front-to-back integration
            // ------------------------------------------------

            radiance +=
                lighting *
                sampleAlpha *
                transmittance;


            transmittance *=
                localTransmission;


            // ------------------------------------------------
            // Early termination
            // ------------------------------------------------

            if (transmittance < 0.01)
                break;
        }


        // ----------------------------------------------------
        // Advance
        // ----------------------------------------------------

        currentPosition +=
            rayDir *
            actualStepSize;

        t +=
            actualStepSize;


        if (t >= tExit)
            break;
    }


    // --------------------------------------------------------
    // Outputs
    // --------------------------------------------------------

    CloudTransmittance =
        saturate(transmittance);

    CloudAlpha =
        saturate(
            1.0 -
            transmittance
        );

    CloudColor =
        max(radiance, 0.0);
}


// ============================================================
// HALF VERSION
//
// Shader Graph can use this when the graph is compiled in half.
// ============================================================

void CloudRaymarch_half(

    half3 RayOrigin,
    half3 RayDirection,

    half3 CloudCenter,
    half CloudRadius,

    half CloudBottom,
    half CloudTop,

    half CloudScale,
    half CloudDensityMultiplier,
    half CloudCoverage,
    half CloudDetailStrength,

    half3 WindDirection,
    half WindSpeed,
    half Time,

    Texture3D NoiseTexture,
    SamplerState NoiseTextureSampler,

    Texture2D CoverageTexture,
    SamplerState CoverageTextureSampler,

    Texture2D HeightTexture,
    SamplerState HeightTextureSampler,

    half3 SunDirection,
    half3 SunColor,
    half SunIntensity,

    half Absorption,
    half PhaseG,

    half RaymarchSteps,
    half StepSize,

    half LightSteps,
    half LightStepSize,

    out half3 CloudColor,
    out half CloudAlpha,
    out half CloudTransmittance
)
{
    float3 color;
    float alpha;
    float transmittance;

    CloudRaymarch_float(

        RayOrigin,
        RayDirection,

        CloudCenter,
        CloudRadius,

        CloudBottom,
        CloudTop,

        CloudScale,
        CloudDensityMultiplier,
        CloudCoverage,
        CloudDetailStrength,

        WindDirection,
        WindSpeed,
        Time,

        NoiseTexture,
        NoiseTextureSampler,

        CoverageTexture,
        CoverageTextureSampler,

        HeightTexture,
        HeightTextureSampler,

        SunDirection,
        SunColor,
        SunIntensity,

        Absorption,
        PhaseG,

        (int)RaymarchSteps,
        StepSize,

        (int)LightSteps,
        LightStepSize,

        color,
        alpha,
        transmittance
    );

    CloudColor =
        (half3)color;

    CloudAlpha =
        (half)alpha;

    CloudTransmittance =
        (half)transmittance;
}

#endif