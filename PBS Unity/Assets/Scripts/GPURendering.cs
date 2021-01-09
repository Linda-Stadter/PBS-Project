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
	ComputeShader partitionShader = default;
    [SerializeField]
    ComputeShader sortShader = default;
    [SerializeField]
    ComputeShader sortShaderNew = default;
    [SerializeField]
	ComputeShader offsetShader = default;
    [SerializeField]
	ComputeShader densityShader = default;
    [SerializeField]
	ComputeShader forceShader = default;
    [SerializeField]
    ComputeShader integrationShader = default;

    ComputeShader sortShader2;



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
    private float[] sortedCellIndexArray;

    // GPU Buffer
    private ComputeBuffer particlesBuffer;
    private ComputeBuffer particlesIndexBuffer;
    private ComputeBuffer cellIndexBuffer;
    private ComputeBuffer offsetBuffer;
    private ComputeBuffer densityBuffer;

    private ComputeBuffer sortedCellIndexBuffer;
    

    // Bounds for Unity's frustum culling
    private Bounds particleBound;

    private MergeSort.BitonicMergeSort _sort;
    private MergeSort.BitonicMergeSort _sort2;

    struct FluidParticle{
        public Vector3 pos;
        public Vector3 v;

        public float density;
        public float pressForce;
        public float visForce;
    }

    void Start()
    {
        particleNumber = 16;
        particleRadius = 0.5f;
        spawnOffset = new Vector3(-5, 5, -5);

        particlesArray = new FluidParticle[particleNumber];
        particlesIndexArray = new int[particleNumber];
        cellIndexArray = new float[particleNumber];
        offsetArray = new int[particleNumber];
        densityArray = new float[particleNumber];
        sortedCellIndexArray = new float[particleNumber];

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

        densityBuffer = new ComputeBuffer(particleNumber, 4);
        densityBuffer.SetData(densityArray);

        sortedCellIndexBuffer = new ComputeBuffer(particleNumber, 4);
        sortedCellIndexBuffer.SetData(sortedCellIndexArray);

        partitionKi = partitionShader.FindKernel("calcCellIndices");
        partitionShader.SetBuffer(partitionKi, "particlesBuffer", particlesBuffer);
        partitionShader.SetBuffer(partitionKi, "particlesIndexBuffer", particlesIndexBuffer);
        partitionShader.SetBuffer(partitionKi, "cellIndexBuffer", cellIndexBuffer);
        partitionShader.SetBuffer(partitionKi, "sortedCellIndexBuffer", sortedCellIndexBuffer);

        offsetKi = offsetShader.FindKernel("calcOffset");
        offsetShader.SetBuffer(offsetKi, "particlesIndexBuffer", particlesIndexBuffer);
        offsetShader.SetBuffer(offsetKi, "cellIndexBuffer", cellIndexBuffer);
        offsetShader.SetBuffer(offsetKi, "offsetBuffer", offsetBuffer);

        densityKi = densityShader.FindKernel("calcDensity");
        densityShader.SetBuffer(densityKi, "particlesBuffer", particlesBuffer);
        densityShader.SetBuffer(densityKi, "particlesIndexBuffer", particlesIndexBuffer);
        densityShader.SetBuffer(densityKi, "cellIndexBuffer", cellIndexBuffer);
        densityShader.SetBuffer(densityKi, "offsetBuffer", offsetBuffer);
        densityShader.SetBuffer(densityKi, "densityBuffer", densityBuffer);

        forceKi = forceShader.FindKernel("calcForce");
        forceShader.SetBuffer(densityKi, "particlesBuffer", particlesBuffer);
        forceShader.SetBuffer(densityKi, "particlesIndexBuffer", particlesIndexBuffer);
        forceShader.SetBuffer(densityKi, "cellIndexBuffer", cellIndexBuffer);
        forceShader.SetBuffer(densityKi, "offsetBuffer", offsetBuffer);
        forceShader.SetBuffer(densityKi, "densityBuffer", densityBuffer);

        integrationKi = integrationShader.FindKernel("calcIntegration");
        integrationShader.SetBuffer(integrationKi, "cellIndexBuffer", cellIndexBuffer);
        integrationShader.SetBuffer(integrationKi, "particlesBuffer", particlesBuffer);

        material.SetBuffer("particlesBuffer", particlesBuffer);
        material.SetFloat("particleRadius", particleRadius);
        
        _sort = new MergeSort.BitonicMergeSort(sortShader);
        
        particleBound = new Bounds(Vector3.zero, Vector3.one);

        sortShader2 = Instantiate(sortShader);
        _sort2 = new MergeSort.BitonicMergeSort(sortShader2);
    }

    void OnEnable () {
        
	}

    void OnDisable () {
		particlesBuffer.Release();
		particlesBuffer = null;
	}


    void PrintArray<T>(string name, T[] array) {
        string str = "";
        for (int i = 0; i < array.Length; ++i) {
            str += array[i].ToString() + "\t";
        }
        Debug.Log(name + ":\t\t" + str);
    }
    

    void Update()
    {
        if (Input.GetKeyDown("1")) {
            Debug.Log("Executing Partition Shader ...");
            partitionShader.Dispatch(partitionKi, 4, 1, 1);

            particlesBuffer.GetData(particlesArray);
            particlesIndexBuffer.GetData(particlesIndexArray);
            cellIndexBuffer.GetData(cellIndexArray);
            sortedCellIndexBuffer.GetData(sortedCellIndexArray);

            PrintArray("particlesArray\t", particlesIndexArray);
            PrintArray("cellIndexArray\t", cellIndexArray);
            PrintArray("sortedCellIndexArray", sortedCellIndexArray);
        }
        else if (Input.GetKeyDown("2")) {
            Debug.Log("Executing Sort Shader (1/2) ...");
            _sort.Sort(particlesIndexBuffer, cellIndexBuffer, sortedCellIndexBuffer);
            // GpuSort.BitonicSort64(particlesIndexBuffer, cellIndexBuffer, sortShaderNew);
            particlesIndexBuffer.GetData(particlesIndexArray);
            cellIndexBuffer.GetData(cellIndexArray);
            sortedCellIndexBuffer.GetData(sortedCellIndexArray);

            PrintArray("particlesArray\t", particlesIndexArray);
            PrintArray("cellIndexArray\t", cellIndexArray);
            PrintArray("sortedCellIndexArray", sortedCellIndexArray);
        } else if (Input.GetKeyDown("3")) {
            Debug.Log("Executing Sort Shader (2/2) ...");
            
            // _sort.Sort(sortedCellIndexBuffer, cellIndexBuffer, true);
            sortedCellIndexBuffer.GetData(sortedCellIndexArray);
            cellIndexBuffer.GetData(cellIndexArray);
            
            PrintArray("sortedCellIndexArray", sortedCellIndexArray);
            PrintArray("cellIndexArray\t", cellIndexArray);
        }
        else if (Input.GetKeyDown("4")) {
            offsetShader.Dispatch(offsetKi, 4, 1, 1);
            sortedCellIndexBuffer.GetData(sortedCellIndexArray);
            cellIndexBuffer.GetData(cellIndexArray);
            PrintArray("sortedCellIndexArray", sortedCellIndexArray);
            PrintArray("cellIndexArray\t", cellIndexArray);
        }
        else if (Input.GetKeyDown("5")) {
            // int[] srt
            ComputeBuffer keys = new ComputeBuffer(particleNumber, 4);
            ComputeBuffer values = new ComputeBuffer(particleNumber, 4);
            // _sort

            densityShader.Dispatch(densityKi, 4, 1, 1);    
        }
        else if (Input.GetKeyDown("6")) {
            forceShader.Dispatch(forceKi, 4, 1, 1);    
        }
        else if (Input.GetKeyDown("7")) {
            integrationShader.Dispatch(integrationKi, 4, 1, 1);    
        }

        Graphics.DrawMeshInstancedProcedural(mesh, 0, material, particleBound, particleNumber);
    }

    void UpdateOnGPU() {
        partitionShader.Dispatch(partitionKi, 4, 1, 1);
        // _sort.Sort(particlesIndexBuffer, cellIndexBuffer, true);
        offsetShader.Dispatch(offsetKi, 4, 1, 1);
        densityShader.Dispatch(densityKi, 4, 1, 1);
        forceShader.Dispatch(forceKi, 4, 1, 1);
        integrationShader.Dispatch(integrationKi, 4, 1, 1);
        
        Graphics.DrawMeshInstancedProcedural(mesh, 0, material, particleBound, particleNumber);
    }
}
