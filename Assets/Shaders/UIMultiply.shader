// =====================================================================================
// UI 겹치기(Multiply) - 형광펜용
// =====================================================================================
// ===== 왜 필요한가? =====
// 유니티 UI의 기본 그리기는 "위에 덮기"다. 노란 칸을 반투명으로 덮으면 흰 종이만
// 노래지는 게 아니라 검은 글자까지 같이 노랗게 섞여서 흐려진다.
//
// 진짜 형광펜은 종이를 "어둡게" 할 뿐 글자를 덮지 않는다. 그 계산이 겹치기(곱하기)다.
//   흰 종이(1,1,1) x 노랑(0.98,0.89,0.30) = 노랑      -> 칠해진 것처럼 보이고
//   검은 글자(0,0,0) x 노랑               = 검정      -> 글자는 그대로 검다
//
// ===== 진하기 조절 =====
// 색의 알파로 조절한다. 1이면 색 그대로 곱하고, 0이면 흰색을 곱해(=아무 변화 없음) 투명해진다.
// (DocumentHighlightPlayer.PenColor 참고)
//
// ===== 잘라내기(RectMask2D) =====
// 자료 칸 밖으로 나간 부분은 잘려야 한다. 그런데 겹치기는 알파를 무시하므로, 잘린 자리를
// 그냥 두면 거기도 어두워진다. 그래서 잘린 픽셀은 흰색으로 내보내 아무 일도 없게 만든다.
Shader "2KH1/UI Multiply"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        ColorMask [_ColorMask]

        // 여기가 핵심: 화면에 이미 그려진 색 x 내 색
        Blend DstColor Zero

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                // 알파를 "진하기"로 쓴다. 0이면 흰색을 곱하게 되어 아무 변화가 없다.
                // (잘려 나간 자리도 알파가 0이 되므로 자동으로 아무 일 없이 지나간다)
                color.rgb = lerp(half3(1, 1, 1), color.rgb, color.a);
                color.a = 1;
                return color;
            }
            ENDCG
        }
    }
    Fallback "UI/Default"
}
