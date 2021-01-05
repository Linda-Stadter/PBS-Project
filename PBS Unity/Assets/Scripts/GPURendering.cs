﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GPURendering : MonoBehaviour
{
    [SerializeField]
    Material material = default;
	[SerializeField]
	Mesh mesh = default;
    [SerializeField]
	ComputeShader partitionShader = default;
    [SerializeField]
    ComputeShader sortShader = default;
    [SerializeField]
	ComputeShader offsetShader = default;

    private int particleNumber;
    private float particleRadius;
    private Vector3 spawnOffset;
    private int partitionKi;
    private int offsetKi;

    private FluidParticle[] particlesArray;
    private int[] particlesIndexArray;
    private float[] cellIndexArray;
    private int[] offsetArray;

    // GPU Buffer
    private ComputeBuffer particlesBuffer;
    private ComputeBuffer particlesIndexBuffer;
    private ComputeBuffer cellIndexBuffer;
    private ComputeBuffer offsetBuffer;

    // Bounds for Unity's frustum culling
    private Bounds particleBound;

    private MergeSort.BitonicMergeSort _sort;

    struct FluidParticle{
        public Vector3 pos;
        public Vector3 v;

        public float density;
        public float pressForce;
        public float visForce;
    }

    void Start()
    {
        particleNumber = 1024;
        particleRadius = 0.5f;
        spawnOffset = new Vector3(-5, 5, -5);

        particlesArray = new FluidParticle[particleNumber];
        particlesIndexArray = new int[particleNumber];
        cellIndexArray = new float[particleNumber];
        offsetArray = new int[particleNumber];

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

        cellIndexBuffer = new ComputeBuffer(particleNumber, 4);
        cellIndexBuffer.SetData(cellIndexArray);

        offsetBuffer = new ComputeBuffer(particleNumber, 4);
        offsetBuffer.SetData(offsetArray);

        partitionKi = partitionShader.FindKernel("calcCellIndices");
        partitionShader.SetBuffer(partitionKi, "particlesBuffer", particlesBuffer);
        partitionShader.SetBuffer(partitionKi, "particlesIndexBuffer", particlesIndexBuffer);
        partitionShader.SetBuffer(partitionKi, "cellIndexBuffer", cellIndexBuffer);

        offsetKi = offsetShader.FindKernel("calcOffset");
        offsetShader.SetBuffer(offsetKi, "particlesIndexBuffer", particlesIndexBuffer);
        offsetShader.SetBuffer(offsetKi, "cellIndexBuffer", cellIndexBuffer);
        offsetShader.SetBuffer(offsetKi, "offsetBuffer", offsetBuffer);

        material.SetBuffer("particlesBuffer", particlesBuffer);
        material.SetFloat("particleRadius", particleRadius);
        
        _sort = new MergeSort.BitonicMergeSort(sortShader);
        
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
        partitionShader.Dispatch(partitionKi, 4, 1, 1);
        _sort.Sort(particlesIndexBuffer, cellIndexBuffer);
        offsetShader.Dispatch(offsetKi, 4, 1, 1);
        // densityShader.Dispatch()
        // forceShader.Dispatch()
        // integrationShader.Dispatch()
        
        Graphics.DrawMeshInstancedProcedural(mesh, 0, material, particleBound, particleNumber);
    }
}