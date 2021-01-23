#ifndef GLOBALS
#define GLOBALS

#define PI 3.14159265358979323846
#define SQRT2 1.41421356237309504880

/* Threads per thread group */
static const uint THREADS = 256;

struct FluidParticle{
    float3 pos;
	float3 v;
	float3 posLF;
	float3 vLF;
};

/* Used in SPHPartition, SPHDensity, and SPHForce */
inline uint SPH_GridHash(int3 cellIndex, uint particleCount)
{
	const uint p1 = 73856093;
	const uint p2 = 19349663;
	const uint p3 = 83492791;
	int n = (p1*cellIndex.x) ^ (p2*cellIndex.y) ^ (p3*cellIndex.z);
	n %= particleCount;
	return n;
}

#endif /* GLOBALS */