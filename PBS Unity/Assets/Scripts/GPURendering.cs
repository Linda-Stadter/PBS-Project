using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GPURendering : MonoBehaviour
{
    [SerializeField]
    GameObject ground;
    [SerializeField]
    Material material = default;
	[SerializeField]
	Mesh mesh = default;
    [SerializeField]
	ComputeShader initShader = default;
    [SerializeField]
	ComputeShader partitionShader = default;
    [SerializeField]
    ComputeShader sortShader = default;
    [SerializeField]
	ComputeShader offsetShader = default;
    [SerializeField]
	ComputeShader SPHDensity = default;
    [SerializeField]
	ComputeShader SPHForce = default;
    [SerializeField]
    ComputeShader SPHIntegration = default;

    private const int THREAD_GROUPS = 4;
    private const int PARTICLE_NUMBER = 1024;
    private const float PARTICLE_RADIUS = 0.25f;
    private const bool USE_LEAPFROG = true;
    private Vector3 SPAWN_OFFSET = new Vector3(-5, 2, -5);

    // Kernel
    private int initializeKi;
    private int partitionKi;
    private int offsetKi;
    private int densityKi;
    private int forceKi;
    private int integrationKi;

    // Arrays for Debugging
    private FluidParticle[] particlesArray;
    private int[] particlesIndexArray;
    private float[] cellIndexArray;
    private float[] sortedCellIndexArray;
    private int[] offsetArray;
    private float[] densityArray;
    private Vector3[] forceArray;

    // GPU Buffer
    private ComputeBuffer particlesBuffer;
    private ComputeBuffer particlesIndexBuffer;
    private ComputeBuffer cellIndexBuffer;
    private ComputeBuffer sortedCellIndexBuffer;
    private ComputeBuffer offsetBuffer;
    private ComputeBuffer densityBuffer;
    private ComputeBuffer forceBuffer;
    
    // Particle bounds for Unity's frustum culling
    private Bounds particleBound;
    private MergeSort.BitonicMergeSort bitonicSort;

    struct FluidParticle{
        public Vector3 pos;
        public Vector3 posLF;

        public Vector3 v;
        public Vector3 vLF;
    }


    void OnEnable()
    {
        InitializeArrays();
        InitializeParticles();
        InitializeBuffers();
        InitializeShader();

        material.SetBuffer("particlesBuffer", particlesBuffer);
        material.SetBuffer("densityBuffer", densityBuffer);
        material.SetFloat("particleRadius", PARTICLE_RADIUS);
        
        bitonicSort = new MergeSort.BitonicMergeSort(sortShader);
        particleBound = new Bounds(Vector3.zero, Vector3.one);
    }


    /* Release Memory after terminating simulation */
    void OnDisable () {
		particlesBuffer.Release();
        particlesBuffer.Release();
        particlesIndexBuffer.Release();
        cellIndexBuffer.Release();
        sortedCellIndexBuffer.Release();
        offsetBuffer.Release();
        densityBuffer.Release();
        forceBuffer.Release();
	}


    /* Function to display arrays in a convenient format */
    void PrintArray<T>(string name, T[] array) {
        string str = "";
        for (int i = 0; i < array.Length; ++i) {
            str += array[i].ToString() + "\t";
        }
        Debug.Log(name + ":\t\t" + str);
    }


    void PrintParticlePos(string name, FluidParticle[] array) {
        string str = "";
        for (int i = 0; i < array.Length; ++i) {
            str += array[i].pos + "\t";
        }
        Debug.Log(name + ":\t\t" + str);
    }

    void PrintDebug(string name, Vector3[] array) {
        string str = "";
        for (int i = 0; i < array.Length; ++i) {
            str += array[i] + "\t";
        }
        Debug.Log(name + ":\t\t" + str);
    }


    void InitializeParticles() {
        int length = (int) Mathf.Pow(PARTICLE_NUMBER, 1f / 3f);
    
        for(int i = 0; i < PARTICLE_NUMBER; ++i) {
            float x_pos = ((i % length) + SPAWN_OFFSET.x) * PARTICLE_RADIUS;
            float y_pos = (((i / length) % length) + SPAWN_OFFSET.y) * PARTICLE_RADIUS;
            float z_pos = (((i / (length * length))) + SPAWN_OFFSET.z) * PARTICLE_RADIUS;

            particlesArray[i].pos = new Vector3(x_pos, y_pos, z_pos);
            particlesArray[i].v = new Vector3(0f, 0f, 0f);
            particlesIndexArray[i] = i;

            // Debug.Log(particlesArray[i].pos);
        }
    }


    void InitializeArrays() {
        particlesArray = new FluidParticle[PARTICLE_NUMBER];
        particlesIndexArray = new int[PARTICLE_NUMBER];
        cellIndexArray = new float[PARTICLE_NUMBER];
        offsetArray = new int[PARTICLE_NUMBER];
        densityArray = new float[PARTICLE_NUMBER];
        forceArray = new Vector3[PARTICLE_NUMBER];
        sortedCellIndexArray = new float[PARTICLE_NUMBER];
    }


    void InitializeBuffers() {
        particlesBuffer = new ComputeBuffer(particlesArray.Length, 12 * sizeof(float));
        particlesBuffer.SetData(particlesArray);

        particlesIndexBuffer = new ComputeBuffer(particlesIndexArray.Length, sizeof(int));
        particlesIndexBuffer.SetData(particlesIndexArray);

        cellIndexBuffer = new ComputeBuffer(PARTICLE_NUMBER, sizeof(float));
        cellIndexBuffer.SetData(cellIndexArray);

        offsetBuffer = new ComputeBuffer(PARTICLE_NUMBER, sizeof(int));
        offsetBuffer.SetData(offsetArray);

        densityBuffer = new ComputeBuffer(PARTICLE_NUMBER, sizeof(float));
        densityBuffer.SetData(densityArray);

        forceBuffer = new ComputeBuffer(PARTICLE_NUMBER, 3 * sizeof(float));
        forceBuffer.SetData(forceArray);

        sortedCellIndexBuffer = new ComputeBuffer(PARTICLE_NUMBER, sizeof(float));
        sortedCellIndexBuffer.SetData(sortedCellIndexArray);
    }

    void InitializeShader() {
        initializeKi = initShader.FindKernel("initialize");
        initShader.SetBuffer(initializeKi, "particlesIndexBuffer", particlesIndexBuffer);
        initShader.SetBuffer(initializeKi, "offsetBuffer", offsetBuffer);

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

        densityKi = SPHDensity.FindKernel("calcDensity");
        SPHDensity.SetBuffer(densityKi, "particlesBuffer", particlesBuffer);
        SPHDensity.SetBuffer(densityKi, "particlesIndexBuffer", particlesIndexBuffer);
        SPHDensity.SetBuffer(densityKi, "cellIndexBuffer", sortedCellIndexBuffer);
        SPHDensity.SetBuffer(densityKi, "offsetBuffer", offsetBuffer);
        SPHDensity.SetBuffer(densityKi, "densityBuffer", densityBuffer);

        forceKi = SPHForce.FindKernel("calcForce");
        SPHForce.SetBuffer(forceKi, "particlesBuffer", particlesBuffer);
        SPHForce.SetBuffer(forceKi, "particlesIndexBuffer", particlesIndexBuffer);
        SPHForce.SetBuffer(forceKi, "cellIndexBuffer", sortedCellIndexBuffer);
        SPHForce.SetBuffer(forceKi, "offsetBuffer", offsetBuffer);
        SPHForce.SetBuffer(forceKi, "densityBuffer", densityBuffer);
        SPHForce.SetBuffer(forceKi, "forceBuffer", forceBuffer);

        integrationKi = SPHIntegration.FindKernel("calcIntegration");
        SPHIntegration.SetBuffer(integrationKi, "particlesBuffer", particlesBuffer);
        SPHIntegration.SetBuffer(integrationKi, "particlesIndexBuffer", particlesIndexBuffer);
        SPHIntegration.SetBuffer(integrationKi, "densityBuffer", densityBuffer);
        SPHIntegration.SetBuffer(integrationKi, "forceBuffer", forceBuffer);

        Renderer rend = ground.gameObject.GetComponent<Renderer>();
        Vector3 radiusVector = new Vector3(0.5f * PARTICLE_RADIUS, 0.5f * PARTICLE_RADIUS, 0.5f * PARTICLE_RADIUS);
        SPHIntegration.SetVector("maxBoxBoundarys", rend.bounds.max - radiusVector);
        SPHIntegration.SetVector("minBoxBoundarys", rend.bounds.min + radiusVector);
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
            Debug.Log("Executing Initialization Shader ...");
            initShader.Dispatch(initializeKi, THREAD_GROUPS, 1, 1);
            particlesIndexBuffer.GetData(particlesIndexArray);
            offsetBuffer.GetData(offsetArray);

            PrintArray("particlesIndexBuffer\t", particlesIndexArray);
            PrintArray("offsetArray\t", offsetArray);
        }
        else if (Input.GetKeyDown("2")) {
            Debug.Log("Executing Partition Shader ...");
            partitionShader.Dispatch(partitionKi, THREAD_GROUPS, 1, 1);

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
        else if (Input.GetKeyDown("3")) {
            Debug.Log("Executing Sort Shader ...");
            bitonicSort.Sort(particlesIndexBuffer, cellIndexBuffer, sortedCellIndexBuffer);
            particlesIndexBuffer.GetData(particlesIndexArray);
            cellIndexBuffer.GetData(cellIndexArray);
            sortedCellIndexBuffer.GetData(sortedCellIndexArray);

            PrintArray("particlesArray\t", particlesIndexArray);
            PrintArray("cellIndexArray\t", cellIndexArray);
            PrintArray("sortedCellIndexArray", sortedCellIndexArray);
        }
        else if (Input.GetKeyDown("4")) {
            Debug.Log("Executing Offset Shader ...");
            offsetShader.Dispatch(offsetKi, THREAD_GROUPS, 1, 1);
            particlesIndexBuffer.GetData(particlesIndexArray);
            sortedCellIndexBuffer.GetData(sortedCellIndexArray);
            offsetBuffer.GetData(offsetArray);

            PrintArray("particelsIndexBuffer", particlesIndexArray);
            PrintArray("sortedCellIndexArray", sortedCellIndexArray);
            PrintArray("offsetArray\t", offsetArray);
        }
        else if (Input.GetKeyDown("5")) {
            Debug.Log("Executing Density Shader ...");
            ComputeBuffer debugBuffer = new ComputeBuffer(PARTICLE_NUMBER * 27 * 3 * 3, sizeof(float));
            Vector3[] debugArray = new Vector3[PARTICLE_NUMBER * 27 * 3];
            debugBuffer.SetData(debugArray);
            SPHDensity.SetBuffer(densityKi, "debugBuffer", debugBuffer);

            SPHDensity.Dispatch(densityKi, THREAD_GROUPS, 1, 1);
            densityBuffer.GetData(densityArray);
            particlesBuffer.GetData(particlesArray);
            debugBuffer.GetData(debugArray);

            PrintDebug("DebugArray\t", debugArray);
            PrintParticlePos("Positions\t", particlesArray);
            PrintArray("densityArray\t", densityArray);
        }
        else if (Input.GetKeyDown("6")) {
            Debug.Log("Executing Force Shader ...");
            SPHForce.Dispatch(forceKi, THREAD_GROUPS, 1, 1);
            forceBuffer.GetData(forceArray);

            PrintArray("forceArray\t", forceArray);
        }
        else if (Input.GetKeyDown("7")) {
            Debug.Log("Executing Integration Shader ...");
            SPHIntegration.Dispatch(integrationKi, THREAD_GROUPS, 1, 1);    
        }

        /* Draw Meshes on GPU */
        Graphics.DrawMeshInstancedProcedural(mesh, 0, material, particleBound, PARTICLE_NUMBER);
    }

    void ExecuteTimeStep() {
        initShader.Dispatch(initializeKi, THREAD_GROUPS, 1, 1);
        partitionShader.Dispatch(partitionKi, THREAD_GROUPS, 1, 1);
        bitonicSort.Sort(particlesIndexBuffer, cellIndexBuffer, sortedCellIndexBuffer);
        offsetShader.Dispatch(offsetKi, THREAD_GROUPS, 1, 1);

        if(USE_LEAPFROG)
        {   
            // Compute dens_i and f_i for a_i
            SPHDensity.SetBool("IsLFtime", true);
            SPHForce.SetBool("IsLFtime", true);

            SPHDensity.Dispatch(densityKi, THREAD_GROUPS, 1, 1);
            SPHForce.Dispatch(forceKi, THREAD_GROUPS, 1, 1);
            //compute v_i+0.5 and pos_i+0.5
            SPHIntegration.SetBool("IsLFtime",true);
            SPHIntegration.Dispatch(integrationKi, THREAD_GROUPS, 1, 1);

            //compute dens_i+0.5 and f_i0.5 for a_i+0.5
            SPHDensity.SetBool("IsLFtime", false);
            SPHForce.SetBool("IsLFtime", false);

            SPHDensity.Dispatch(densityKi, THREAD_GROUPS, 1, 1);
            SPHForce.Dispatch(forceKi, THREAD_GROUPS, 1, 1);

            //compute v_i+1 and pos_i+1 and clamp to box
            SPHIntegration.SetBool("IsLFtime", false);
            SPHIntegration.Dispatch(integrationKi, THREAD_GROUPS, 1, 1);
        }
        else //Forward Euler Integration
        {
            SPHDensity.SetBool("IsLFtime", false);
            SPHForce.SetBool("IsLFtime", false);
            SPHIntegration.SetBool("IsLFtime", false);

            SPHDensity.Dispatch(densityKi, THREAD_GROUPS, 1, 1);
            SPHForce.Dispatch(forceKi, THREAD_GROUPS, 1, 1);
            SPHIntegration.Dispatch(integrationKi, THREAD_GROUPS, 1, 1);
        }

        Graphics.DrawMeshInstancedProcedural(mesh, 0, material, particleBound, PARTICLE_NUMBER);
    }
}
