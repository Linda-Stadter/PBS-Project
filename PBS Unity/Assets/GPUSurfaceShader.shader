Shader "Custom/GPUSurfaceShader"
{
    
    Properties
    {
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
    }
    SubShader
    {
        CGPROGRAM
		#pragma surface ConfigureSurface Standard fullforwardshadows addshadow
        #pragma instancing_options assumeuniformscaling procedural:ConfigureProcedural
        #pragma editor_sync_compilation
		#pragma target 4.5

		struct Input {
			float3 worldPos;
		};

        struct FluidParticle{
            float3 pos;
            float3 v;

            float density;
            float pressForce;
            float visForce;
        };

        #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
            StructuredBuffer<FluidParticle> particlesBuffer;
            float particleRadius;
        #endif

        void ConfigureProcedural () {
            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                float3 position = particlesBuffer[unity_InstanceID].pos;
                unity_ObjectToWorld = 0.0;
                unity_ObjectToWorld._m03_m13_m23_m33 = float4(position, 1.0);
                unity_ObjectToWorld._m00_m11_m22 = particleRadius;
			#endif
        }

        float _Smoothness;
        void ConfigureSurface (Input input, inout SurfaceOutputStandard surface) {
            surface.Albedo = saturate(input.worldPos * 0.5 + 0.5);
            surface.Smoothness = _Smoothness;
        }

		ENDCG
    }
    FallBack "Diffuse"
}
