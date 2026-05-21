Shader "Custom/GridBoundaryShader"
{
	Properties
	{
		_GridTex ("GridTex", 2D) = "black" {}
	}

	SubShader
	{
		Tags 
		{
			"Queue" = "Transparent"
			"RenderPipeline" = "UniversalPipeline"
		}

		Pass
		{
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
				float raw = SAMPLE_TEXTURE2D(_GridTex, sampler_GridTex, IN.uv).r;
				uint index = (uint)round(raw * 65535.0);

				return _GridColors[index];
			}
			ENDHLSL
		}
	}
}
