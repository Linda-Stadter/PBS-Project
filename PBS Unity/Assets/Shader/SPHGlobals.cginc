#ifndef GLOBALS
#define GLOBALS

#define PI 3.14159265358979323846
#define SQRT2 1.41421356237309504880

static const uint THREADS = 256;
static const uint PARTICLE_COUNT = 1024;

static const float xSPH_h 		= 1.0f;									// smoothing radius
static const float xSPH_h_rcp 	= 1.0f / xSPH_h;						// 1.0f / smoothing radius
static const float xSPH_h2 		= xSPH_h * xSPH_h;						// smoothing radius ^ 2
static const float xSPH_h3 		= xSPH_h * xSPH_h * xSPH_h;				// smoothing radius ^ 3
static const float xSPH_mass = 1.0f;

float xSPH_poly6_constant 		= 315 / (64 * PI * pow(xSPH_h, 9));		// precomputed Poly6 kernel constant term
float xSPH_spiky_constant		= -45 / (PI * pow(xSPH_h, 6));			// precomputed Spiky kernel function constant term
float xSPH_K					= 250.0f;								// pressure constant
float xSPH_p0					= 1.0f;									// reference density
float xSPH_e					= 0.018f;								// viscosity constant


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