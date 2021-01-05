using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GPURendering : MonoBehaviour
{
    [SerializeField]
    Material material = default;
	[SerializeField]
	Mesh mesh = default;
    [SerializeField]
	ComputeShader computeShader = default;

    private int particleNumber;
    private float particleRadius;
    private Vector3 spawnOffset;
    private int kiCalc;

    private FluidParticle[] particlesArray;
    private int[] particlesIndexArray;

    // GPU Buffer
    private ComputeBuffer particlesBuffer;
    private ComputeBuffer particlesIndexBuffer;

    // Bounds for Unity's frustum culling
    private Bounds particleBound;

    struct FluidParticle{
        public Vector3 pos;
        public Vector3 v;

        public float density;
        public float pressForce;
        public float visForce;
    }

    void Start()
    {
        particleNumber = 1000;
        particleRadius = 0.5f;
        spawnOffset = new Vector3(-5, 5, -5);

        particlesArray = new FluidParticle[particleNumber];
        particlesIndexArray = new int[particleNumber];

        int length = (int) Mathf.Pow(particleNumber, 1f / 3f);
    
        for(int i = 0; i < particleNumber; ++i) {
            float x_pos = ((i % length) + spawnOffset.x) * particleRadius;
            float y_pos = (((i / length) % length) + spawnOffset.y) * particleRadius;
            float z_pos = (((i / (length * length))) + spawnOffset.z) * particleRadius;
            particlesArray[i].pos = new Vector3(x_pos, y_pos, z_pos);

            particlesArray[i].v = new Vector3(0f, 0f, 0f);

            particlesArray[i].density = 0f;
            particlesArray[i].pressForce = 0f;
            particlesArray[i].visForce = 0f;

            particlesIndexArray[i] = i;

            // Debug.Log(particlesArray[i].x + "\t" + particlesArray[i].y + "\t" + particlesArray[i].z);
        }

        particlesBuffer = new ComputeBuffer(particlesArray.Length, 9 * 4);
        particlesBuffer.SetData(particlesArray);

        particlesIndexBuffer = new ComputeBuffer(particlesIndexArray.Length, 4);
        particlesIndexBuffer.SetData(particlesIndexArray);

        computeShader.SetInt("nParticles", particleNumber);

        kiCalc = computeShader.FindKernel("calculate");
        computeShader.SetBuffer(kiCalc, "particlesBuffer", particlesBuffer);
        computeShader.SetBuffer(kiCalc, "particlesIndexBuffer", particlesIndexBuffer);

        material.SetBuffer("particlesBuffer", particlesBuffer);
        material.SetFloat("particleRadius", particleRadius);
        
        particleBound = new Bounds(Vector3.zero, Vector3.one);
    }

    void OnEnable () {
        
	}

    void OnDisable () {
		particlesBuffer.Release();
		particlesBuffer = null;
	}

    void Update()
    {
        UpdateOnGPU();
    }

    void UpdateOnGPU() {
        computeShader.Dispatch(kiCalc, 2, 2, 2);
        
        
        Graphics.DrawMeshInstancedProcedural(mesh, 0, material, particleBound, particleNumber);
    }
}
