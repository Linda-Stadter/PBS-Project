#ifndef GLOBALS
#define GLOBALS

static const uint THREADS = 256;
static const uint PARTICLE_COUNT = 1024;

float xSPH_h = 1.0f;					    // smoothing radius
float xSPH_h_rcp = 1.0f;	                // 1.0f / smoothing radius
float		xSPH_h2;				// smoothing radius ^ 2
float		xSPH_h3;				// smoothing radius ^ 3

float		xSPH_poly6_constant;	// precomputed Poly6 kernel constant term
float		xSPH_spiky_constant;	// precomputed Spiky kernel function constant term
float		xSPH_K;					// pressure constant
float		xSPH_p0;				// reference density


struct FluidParticle{
    float3 pos;
    float3 v;

    float density;
    float pressForce;
    float visForce;
};


inline uint SPH_GridHash(int3 cellIndex)
{
	const uint p1 = 73856093;
	const uint p2 = 19349663;
	const uint p3 = 83492791;
	int n = p1 * cellIndex.x ^ p2*cellIndex.y ^ p3*cellIndex.z;
	n %= PARTICLE_COUNT;
	return n;
}

#endif /* GLOBALS */