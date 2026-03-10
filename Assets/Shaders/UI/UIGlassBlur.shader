Shader "UI/UIGlassBlur"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (1,1,1,0.4)

        _BlurRadius ("Blur Radius", Range(0, 3)) = 1.0
        _BlurSteps ("Blur Steps (int)", Range(1, 12)) = 8

        _NoiseIntensity ("Noise Intensity", Range(0, 0.3)) = 0.1

        _BorderColor ("Border Color", Color) = (1,1,1,0.5)
        _BorderThickness ("Border Thickness", Range(0, 0.5)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
            "PreviewType"="Plane"
            "CanvasModulateColor"="1"
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "UIGlassBlur"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            // 来自 URP 的相机不透明纹理与其 TexelSize（需在 URP Asset 中启用 Opaque Texture）
            sampler2D _CameraOpaqueTexture;
            float4 _CameraOpaqueTexture_TexelSize;

            fixed4 _TintColor;

            float _BlurRadius;
            float _BlurSteps;

            float _NoiseIntensity;

            fixed4 _BorderColor;
            float _BorderThickness;

            float4 _ClipRect;

            struct appdata
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
            };

            // 简单哈希噪声，用于玻璃颗粒感
            float rand(float2 co)
            {
                return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 计算当前像素在屏幕上的 UV（用于从 _CameraOpaqueTexture 采样）
                float2 screenUV = i.vertex.xy / i.vertex.w;
                screenUV = screenUV * 0.5f + 0.5f;

                // 高斯近似的邻域模糊（简单多采样，注意性能）
                int steps = (int)_BlurSteps;
                steps = max(1, steps);

                float2 texel = _CameraOpaqueTexture_TexelSize.xy * _BlurRadius;

                float3 accumColor = 0;
                float accumWeight = 0;

                // 中心权重
                float centerWeight = 1.0;
                accumColor += tex2D(_CameraOpaqueTexture, screenUV).rgb * centerWeight;
                accumWeight += centerWeight;

                // 四个方向 + 对角线方向简单采样
                // 注意：不要对这个循环使用 [unroll]，否则在 D3D11 上可能因迭代次数估计过大而报错
                for (int k = 1; k <= steps; k++)
                {
                    float w = exp(- (k * k) * 0.25); // 简单权重衰减
                    float2 offset = texel * k;

                    accumColor += tex2D(_CameraOpaqueTexture, screenUV + float2( offset.x,  0)) .rgb * w;
                    accumColor += tex2D(_CameraOpaqueTexture, screenUV + float2(-offset.x,  0)) .rgb * w;
                    accumColor += tex2D(_CameraOpaqueTexture, screenUV + float2( 0,  offset.y)).rgb * w;
                    accumColor += tex2D(_CameraOpaqueTexture, screenUV + float2( 0, -offset.y)).rgb * w;

                    // 对角线方向（可选，增加柔和感）
                    accumColor += tex2D(_CameraOpaqueTexture, screenUV + float2( offset.x,  offset.y)).rgb * (w * 0.5);
                    accumColor += tex2D(_CameraOpaqueTexture, screenUV + float2(-offset.x,  offset.y)).rgb * (w * 0.5);
                    accumColor += tex2D(_CameraOpaqueTexture, screenUV + float2( offset.x, -offset.y)).rgb * (w * 0.5);
                    accumColor += tex2D(_CameraOpaqueTexture, screenUV + float2(-offset.x, -offset.y)).rgb * (w * 0.5);

                    accumWeight += w * 4.0 + w * 0.5 * 4.0;
                }

                float3 blurred = accumColor / max(accumWeight, 1e-4);

                // 叠加玻璃色调
                fixed4 glass = fixed4(blurred, 1.0);
                glass.rgb *= _TintColor.rgb;
                glass.a = _TintColor.a;

                // 内部简单高光边缘（根据局部 UV 与边缘距离）
                float2 local = i.uv;
                float2 edgeDist = min(local, 1.0 - local);
                float minEdge = min(edgeDist.x, edgeDist.y);
                float borderMask = smoothstep(_BorderThickness, 0.0, minEdge); // 越靠近边缘越亮

                glass.rgb = lerp(glass.rgb, _BorderColor.rgb, borderMask * _BorderColor.a);

                // 噪声（磨砂感）：轻微随机亮度变化
                if (_NoiseIntensity > 0.001)
                {
                    float n = rand(screenUV * _ScreenParams.xy);
                    float3 noise = (n - 0.5) * 2.0 * _NoiseIntensity;
                    glass.rgb += noise;
                }

                // 采样前景图（如果你想叠加自身 Sprite 纹理，例如玻璃上有图案）
                fixed4 mainCol = tex2D(_MainTex, i.uv) * i.color;
                // 将玻璃与主纹理混合：主纹理主要影响 Alpha 和轻微颜色
                glass.rgb = lerp(glass.rgb, glass.rgb * mainCol.rgb, mainCol.a);

                // UGUI 剪裁支持（Mask/RectMask2D）
                float mask = UnityGet2DClipping(i.vertex.xy, _ClipRect);
                glass.a *= mask;

                return glass;
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}

