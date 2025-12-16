Shader "Custom/Slice"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Albedo (RGB)", 2D) = "white" {}
        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0

        sliceNormal("normal", Vector) = (0,0,0,0)
        sliceCentre("centre", Vector) = (0,0,0,0)
        sliceOffsetDst("offset", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 worldPos    : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            half _Glossiness;
            half _Metallic;

            float3 sliceNormal;
            float3 sliceCentre;
            float sliceOffsetDst;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(worldPos);   // stereo-safe
                OUT.worldPos    = worldPos;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // --- Slice logic ---
                float3 adjustedCentre = sliceCentre + sliceNormal * sliceOffsetDst;
                float3 offsetToSliceCentre = adjustedCentre - IN.worldPos;
                clip(dot(offsetToSliceCentre, sliceNormal));

                // --- Albedo ---
                float4 texColor = tex2D(_MainTex, IN.uv) * _Color;

                // --- Safe SurfaceData init ---
                SurfaceData surfaceData = (SurfaceData)0;   // zero all members
                surfaceData.albedo     = texColor.rgb;
                surfaceData.alpha      = texColor.a;
                surfaceData.metallic   = _Metallic;
                surfaceData.smoothness = _Glossiness;
                surfaceData.occlusion  = 1.0;

                // --- InputData (lighting) ---
                InputData inputData = (InputData)0;  
                inputData.positionWS     = IN.worldPos;
                inputData.normalWS       = normalize(IN.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.worldPos);
                inputData.shadowCoord    = TransformWorldToShadowCoord(IN.worldPos);
                inputData.bakedGI        = SampleSH(inputData.normalWS);

                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }
    }
}
