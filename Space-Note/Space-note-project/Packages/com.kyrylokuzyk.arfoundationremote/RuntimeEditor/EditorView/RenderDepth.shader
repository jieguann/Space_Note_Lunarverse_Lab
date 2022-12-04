Shader "ARFoundationRemote/RenderDepth" {
    Properties {
        _PrevDepthTex("", 2D) = "white"{}
    }
    
    SubShader {
        Tags {
            "Queue" = "Overlay"
            "RenderType" = "Overlay"
        }
        
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _PrevDepthTex;
        ENDCG

        Pass {
            Cull Off
            ZTest Always
            ZWrite Off
            ColorMask R
            
            CGPROGRAM
            struct fragmentOutput {
                float4 color : SV_Target;
            };

            #pragma vertex vert_img

            sampler2D _CameraDepthTexture;
            #pragma fragment frag
            fragmentOutput frag (const v2f_img i) {
                fragmentOutput o;
                const float prevDepth = tex2D(_PrevDepthTex, i.uv).r;
                float curDepth = tex2D(_CameraDepthTexture, i.uv).r;
                #if UNITY_REVERSED_Z
                    curDepth = 1.0f - curDepth;
                #endif
                
                float depth = min(prevDepth, curDepth);
                o.color = float4(depth, 0.0, 0.0, 0.0);
                return o;
            }
            ENDCG
        }
    }
    
    FallBack Off
}
