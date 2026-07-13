Shader "Custom/GridBoundaryShader"
{
	Properties
	{
		_GridTex ("GridTex", 2D) = "black" {}
		_UseDirectColor ("Use Direct Color", Float) = 0
		_OverlayAlpha ("Overlay Alpha", Range(0, 1)) = 0.45
	}

	SubShader
	{
		Tags 
		{
			"Queue" = "Transparent"
			"RenderType" = "Transparent"
			"RenderPipeline" = "UniversalPipeline"
		}

		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Cull Off

			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			struct Attributes
			{
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionHCS : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			TEXTURE2D(_GridTex);
			SAMPLER(sampler_GridTex);

			CBUFFER_START(GridColors)
				float4 _GridColors[16];
				float _UseDirectColor;
				float _OverlayAlpha;
			CBUFFER_END

			Varyings vert(Attributes IN)
			{
				Varyings OUT;
				OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
				OUT.uv = IN.uv;
				return OUT;
			}

			float4 frag(Varyings IN) : SV_Target
			{
				float4 sampled = SAMPLE_TEXTURE2D(_GridTex, sampler_GridTex, IN.uv);
				if (_UseDirectColor > 0.5)
					return float4(sampled.rgb, _OverlayAlpha);

				float raw = sampled.r;
				uint index = (uint)round(raw * 65535.0);

				return _GridColors[index];
			}
			ENDHLSL
		}
	}
}
