Shader "Custom/PortalStereo"
{
    Properties
    {
        _InactiveColour ("Inactive Colour", Color) = (1, 1, 1, 1)
        _LeftEyeTex ("Left Eye Texture", 2D) = "white" {}
        _RightEyeTex ("Right Eye Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // XR / Instancing support
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO
            #pragma multi_compile_fragment _ _XR_STEREO_MULTIVIEW _XR_STEREO_INSTANCING

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _LeftEyeTex;
            sampler2D _RightEyeTex;
            float4 _InactiveColour;
            int displayMask;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 uv = i.screenPos.xy / i.screenPos.w;

                fixed4 portalCol;
                // Works for multipass and single-pass VR
                if (unity_StereoEyeIndex == 0)
                    portalCol = tex2D(_LeftEyeTex, uv);
                else
                    portalCol = tex2D(_RightEyeTex, uv);

                return portalCol * displayMask + _InactiveColour * (1 - displayMask);
            }
            ENDCG
        }
    }
    Fallback "Standard"
}
