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
    [SerializeField]
	ComputeShader densityShader = default;
    [SerializeField]
	ComputeShader forceShader = default;
    [SerializeField]
    ComputeShader integrationShader = default;



    private int particleNumber;
    private float particleRadius;
    private Vector3 spawnOffset;
    private int partitionKi;
    private int offsetKi;
    private int densityKi;
    private int forceKi;
    private int integrationKi;

    private FluidParticle[] particlesArray;
    private int[] particlesIndexArray;
    private float[] cellIndexArray;
    private int[] offsetArray;
    private float[] densityArray;
    private float[] forceArray;
    private float[] sortedCellIndexArray;

    // GPU Buffer
    private ComputeBuffer particlesBuffer;
    private ComputeBuffer particlesIndexBuffer;
    private ComputeBuffer cellIndexBuffer;
    private ComputeBuffer sortedCellIndexBuffer;
    private ComputeBuffer offsetBuffer;
    private ComputeBuffer densityBuffer;
    private ComputeBuffer forceBuffer;
    

    // Bounds for Unity's frustum culling
    private Bounds particleBound;

    private MergeSort.BitonicMergeSort _sort;

    struct FluidParticle{
        public Vector3 pos;
        public Vector3 v;
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
        densityArray = new float[particleNumber];
        forceArray = new float[particleNumber];
        sortedCellIndexArray = new float[particleNumber];

        int length = (int) Mathf.Pow(particleNumber, 1f / 3f);
    
        for(int i = 0; i < particleNumber; ++i) {
            float x_pos = ((i % length) + spawnOffset.x) * particleRadius;
            float y_pos = (((i / length) % length) + spawnOffset.y) * particleRadius;
            float z_pos = (((i / (length * length))) + spawnOffset.z) * particleRadius;
            particlesArray[i].pos = new Vector3(x_pos, y_pos, z_pos);

            particlesArray[i].v = new Vector3(0f, 0f, 0f);

            particlesIndexArray[i] = i;

            // Debug.Log(particlesArray[i].x + "\t" + particlesArray[i].y + "\t" + particlesArray[i].z);
        }

        particlesBuffer = new ComputeBuffer(particlesArray.Length, 6 * 4);
        particlesBuffer.SetData(particlesArray);

        particlesIndexBuffer = new ComputeBuffer(particlesIndexArray.Length, sizeof(int));
        particlesIndexBuffer.SetData(particlesIndexArray);

        cellIndexBuffer = new ComputeBuffer(particleNumber, sizeof(float));
        cellIndexBuffer.SetData(cellIndexArray);

        offsetBuffer = new ComputeBuffer(particleNumber, sizeof(int));
        offsetBuffer.SetData(offsetArray);

        densityBuffer = new ComputeBuffer(particleNumber, sizeof(int));
        densityBuffer.SetData(densityArray);

        forceBuffer = new ComputeBuffer(particleNumber, sizeof(int));
        forceBuffer.SetData(forceArray);

        sortedCellIndexBuffer = new ComputeBuffer(particleNumber, sizeof(float));
        sortedCellIndexBuffer.SetData(sortedCellIndexArray);

        partitionKi = partitionShader.FindKernel("calcCellIndices");
        partitionShader.SetBuffer(partitionKi, "particlesBuffer", particlesBuffer);
        partitionShader.SetBuffer(partitionKi, "particlesIndexBuffer", particlesIndexBuffer);
        partitionShader.SetBuffer(partitionKi, "cellIndexBuffer", cellIndexBuffer);
        partitionShader.SetBuffer(partitionKi, "sortedCellIndexBuffer", sortedCellIndexBuffer);
        partitionShader.SetBuffer(partitionKi, "offsetBuffer", offsetBuffer);

        offsetKi = offsetShader.FindKernel("calcOffset");
        offsetShader.SetBuffer(offsetKi, "particlesIndexBuffer", particlesIndexBuffer);
        offsetShader.SetBuffer(offsetKi, "cellIndexBuffer", cellIndexBuffer);
        offsetShader.SetBuffer(offsetKi, "offsetBuffer", offsetBuffer);

        densityKi = densityShader.FindKernel("calcDensity");
        densityShader.SetBuffer(densityKi, "particlesBuffer", particlesBuffer);
        densityShader.SetBuffer(densityKi, "particlesIndexBuffer", particlesIndexBuffer);
        densityShader.SetBuffer(densityKi, "cellIndexBuffer", sortedCellIndexBuffer);
        densityShader.SetBuffer(densityKi, "offsetBuffer", offsetBuffer);
        densityShader.SetBuffer(densityKi, "densityBuffer", densityBuffer);

        forceKi = forceShader.FindKernel("calcForce");
        forceShader.SetBuffer(forceKi, "particlesBuffer", particlesBuffer);
        forceShader.SetBuffer(forceKi, "particlesIndexBuffer", particlesIndexBuffer);
        forceShader.SetBuffer(forceKi, "cellIndexBuffer", sortedCellIndexBuffer);
        forceShader.SetBuffer(forceKi, "offsetBuffer", offsetBuffer);
        forceShader.SetBuffer(forceKi, "densityBuffer", densityBuffer);
        forceShader.SetBuffer(forceKi, "forceBuffer", forceBuffer);

        integrationKi = integrationShader.FindKernel("calcIntegration");
        integrationShader.SetBuffer(integrationKi, "particlesBuffer", particlesBuffer);
        integrationShader.SetBuffer(integrationKi, "particlesIndexBuffer", particlesIndexBuffer);
        integrationShader.SetBuffer(integrationKi, "densityBuffer", densityBuffer);
        integrationShader.SetBuffer(integrationKi, "forceBuffer", forceBuffer);

        material.SetBuffer("particlesBuffer", particlesBuffer);
        material.SetFloat("particleRadius", particleRadius);
        
        _sort = new MergeSort.BitonicMergeSort(sortShader);
        
        particleBound = new Bounds(Vector3.zero, Vector3.one);

    }

    void OnDisable () {
		particlesBuffer.Release();
		particlesBuffer = null;
	}


    /* Function to display arrays in a convenient format */
    void PrintArray<T>(string name, T[] array) {
        string str = "";
        for (int i = 0; i < array.Length; ++i) {
            str += array[i].ToString() + "\t";
        }
        Debug.Log(name + ":\t\t" + str);
    }
    

    void Update()
    {
        /* Executing one Timestep per frame */
        ExecuteTimeStep();

        /* The following is for debugging */
        if (Input.GetKeyDown("0")) {
            Debug.Log("Executing one time step ...");
            ExecuteTimeStep();
        }
        else if (Input.GetKeyDown("1")) {
            Debug.Log("Executing Partition Shader ...");
            partitionShader.Dispatch(partitionKi, 4, 1, 1);

            particlesBuffer.GetData(particlesArray);
            particlesIndexBuffer.GetData(particlesIndexArray);
            cellIndexBuffer.GetData(cellIndexArray);
            sortedCellIndexBuffer.GetData(sortedCellIndexArray);
            offsetBuffer.GetData(offsetArray);

            PrintArray("particlesArray\t", particlesIndexArray);
            PrintArray("cellIndexArray\t", cellIndexArray);
            PrintArray("sortedCellIndexArray", sortedCellIndexArray);
            PrintArray("offsetArray\t", offsetArray);
        }
        else if (Input.GetKeyDown("2")) {
            Debug.Log("Executing Sort Shader ...");
            _sort.Sort(particlesIndexBuffer, cellIndexBuffer, sortedCellIndexBuffer);
            particlesIndexBuffer.GetData(particlesIndexArray);
            cellIndexBuffer.GetData(cellIndexArray);
            sortedCellIndexBuffer.GetData(sortedCellIndexArray);

            PrintArray("particlesArray\t", particlesIndexArray);
            PrintArray("cellIndexArray\t", cellIndexArray);
            PrintArray("sortedCellIndexArray", sortedCellIndexArray);
        }
        else if (Input.GetKeyDown("3")) {
            Debug.Log("Executing Offset Shader ...");
            offsetShader.Dispatch(offsetKi, 4, 1, 1);
            particlesIndexBuffer.GetData(particlesIndexArray);
            sortedCellIndexBuffer.GetData(sortedCellIndexArray);
            offsetBuffer.GetData(offsetArray);

            PrintArray("particelsIndexBuffer", particlesIndexArray);
            PrintArray("sortedCellIndexArray", sortedCellIndexArray);
            PrintArray("offsetArray\t", offsetArray);
        }
        else if (Input.GetKeyDown("4")) {
            Debug.Log("Executing Density Shader ...");
            densityShader.Dispatch(densityKi, 4, 1, 1);
            densityBuffer.GetData(densityArray);

            PrintArray("densityArray\t", densityArray);
        }
        else if (Input.GetKeyDown("5")) {
            Debug.Log("Executing Force Shader ...");
            forceShader.Dispatch(forceKi, 4, 1, 1);
            forceBuffer.GetData(forceArray);

            PrintArray("forceArray\t", forceArray);
        }
        else if (Input.GetKeyDown("6")) {
            Debug.Log("Executing Integration Shader ...");
            integrationShader.Dispatch(integrationKi, 4, 1, 1);    
        }

        /* Draw Meshes on GPU */
        Graphics.DrawMeshInstancedProcedural(mesh, 0, material, particleBound, particleNumber);
    }

    void ExecuteTimeStep() {
        partitionShader.Dispatch(partitionKi, 4, 1, 1);
        _sort.Sort(particlesIndexBuffer, cellIndexBuffer, sortedCellIndexBuffer);
        offsetShader.Dispatch(offsetKi, 4, 1, 1);
        densityShader.Dispatch(densityKi, 4, 1, 1);
        forceShader.Dispatch(forceKi, 4, 1, 1);
        integrationShader.Dispatch(integrationKi, 4, 1, 1);
        
        Graphics.DrawMeshInstancedProcedural(mesh, 0, material, particleBound, particleNumber);
    }
}
