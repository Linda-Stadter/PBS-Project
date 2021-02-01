Shader "Custom/GPUSurfaceShader"
{
    
    Properties
    {
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
    }
    SubShader
    {
        CGPROGRAM
        #include "./Globals.cginc"

		#pragma surface ConfigureSurface Standard fullforwardshadows addshadow
        #pragma instancing_options assumeuniformscaling procedural:ConfigureProcedural
        #pragma editor_sync_compilation
		#pragma target 4.5

		struct Input {
			float3 worldPos;
		};


        #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
            StructuredBuffer<FluidParticle> particlesBuffer;
            StructuredBuffer<float> densityBuffer;
            float particleRadius;
            float refDensity;
        #endif

        void ConfigureProcedural () {
            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                float3 position = particlesBuffer[unity_InstanceID].pos;
                unity_ObjectToWorld = 0.0;
                unity_ObjectToWorld._m03_m13_m23_m33 = float4(position, 1.0);
                unity_ObjectToWorld._m00_m11_m22 = particleRadius * 2;
			#endif
        }

        float3 hsv_to_rgb(float3 HSV)
        {
            float h_i = floor(HSV.x / 60);
            float f = HSV.x / 60 - h_i;
            float p = HSV.z * (1 - HSV.y);
            float q = HSV.z * (1 - HSV.y * f);
            float t = HSV.z * (1 - HSV.y * (1 - f));

            if (h_i == 1) { return float3(q, HSV.z, p); }
            else if (h_i == 2) { return float3(p, HSV.z, t); }
            else if (h_i == 3) { return float3(p, q, HSV.z); }
            else if (h_i == 4) { return float3(t, p, HSV.z); }
            else if (h_i == 5) { return float3(HSV.z, p, q); }
            else { return float3(HSV.z, t, p); }
        }

        float _Smoothness;
        void ConfigureSurface (Input input, inout SurfaceOutputStandard surface) {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                float sat = (densityBuffer[unity_InstanceID]) *  1.0f / refDensity;/// 2000.0f;
                float3 col = float3(0.0f, sat, 1.0f); // use hsv to interpolate between colors
                surface.Albedo = saturate(hsv_to_rgb(col)); // transform hsv to rgb
            #endif
            // surface.Albedo = saturate(input.worldPos * 0.5 + 0.5);
            // surface.Smoothness = _Smoothness;
        }

        

		ENDCG
    }
    FallBack "Diffuse"
}
