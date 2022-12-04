Shader "ARFoundationRemote/RenderTextureAndDepth" {
    Properties {
        _MainTex("_MainTex", 2D) = "white"{}
        _DepthTex("_DepthTex", 2D) = "white"{}
    }
    
    SubShader {
        Tags {
            "Queue" = "Overlay"
        }
        
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        sampler2D _DepthTex;
        ENDCG

        Pass {
            Cull Off
            ZTest LEqual
            ZWrite Off
            Blend One OneMinusSrcAlpha
            
            CGPROGRAM
            struct fragmentOutput {
                float4 color : SV_Target;
                float depth : SV_Depth;
            };

            #pragma vertex vert_img

            sampler2D _CameraDepthTexture;
            #pragma fragment frag
            fragmentOutput frag (const v2f_img i) {
                fragmentOutput o;
                const half2 uv = i.uv;
                o.color = tex2D(_MainTex, uv);
                
                const float depth = tex2D(_DepthTex, uv).r;
                #if UNITY_REVERSED_Z
                    o.depth = 1 - depth;
                #else
                    o.depth = depth;
                #endif
                
                return o;
            }
            ENDCG
        }
    }
    
    FallBack Off
}
