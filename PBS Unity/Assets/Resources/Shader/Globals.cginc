#ifndef GLOBALS
#define GLOBALS

#define PI 3.14159265358979323846
#define SQRT2 1.41421356237309504880

/* Threads per thread group */
static const uint THREADS = 256;

struct FluidParticle{
    float3 pos;
	float3 v;
};

// Physical parameters for SPH
static const float xSPH_h 		= 1.0f;									// smoothing radius
static const float xSPH_h_rcp 	= 1.0f / xSPH_h;						// 1.0f / smoothing radius
static const float xSPH_h2 		= xSPH_h * xSPH_h;						// smoothing radius ^ 2
static const float xSPH_h3 		= xSPH_h * xSPH_h * xSPH_h;				// smoothing radius ^ 3

static const float xSPH_poly6_constant 		= 315 / (64 * PI * pow(xSPH_h, 9));		// precomputed Poly6 kernel constant term
static const float xSPH_spiky_constant		= -45 / (PI * pow(xSPH_h, 6));			// precomputed Spiky kernel function constant term
static const float xSPH_K					= 250.0f; //0.000001f;							// pressure constant (stiffness parameter: higher k -> faster force respond but smaller time-steps)
static const float xSPH_p0					= 1.0f; //59.0;									// reference density
static const float xSPH_e					= 0.018f; //1.3;								// viscosity constant

static const float3 xSPH_g					= float3(0, -9.8f, 0);
static const float xSPH_damping				= 0.5f;

static const float xSPH_mass	= 1.0; //28.0f;							// particle mass
static const uint xSPH_gamma	= 7;


/* Used in SPHPartition, SPHDensity, and SPHForce */
inline uint SPH_GridHash(int3 cellIndex, uint particleCount)
{
	const uint p1 = 73856093;
	const uint p2 = 19349663;
	const uint p3 = 83492791;
	int n = p1 * cellIndex.x ^ p2*cellIndex.y ^ p3*cellIndex.z;
	n %= particleCount;
	return n;
}

#endif /* GLOBALS */