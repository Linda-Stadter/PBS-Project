using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Homebrew;

public enum IntegrationMethod { Leapfrog, ForwardEuler };

public class GPURendering : MonoBehaviour
{
    /* Simulation parameters visible in Inspector */
    public bool debugMode;
    public bool spawnAsCube;

    [Foldout("General Parameters", true)]
    public Mesh particleMesh;

    [Range(1, 32)]
    public int spwHeight;
    [Range(1, 32)]
    public int spwWidth;
    [Range(1, 32)]
    public int spwDepth;
    [Range(0.02f, 0.02f)]
    public float boxThickness;
    [Range(0.5f, 10)]
    public float boxWidth;
    [Range(0.5f, 10)]
    public float boxDepth;
    [Range(0.0f, 5.0f)]
    public float spawnDistance;
    [Range(0.0f, 1.0f)]
    public float damping;
    [Space(15)]
    public IntegrationMethod integrationMethod;
    public float timeStep;

    [Foldout("SPH Related Parameters", true)]
    /* Particle mass */
    private float mass;

    /* Smoothing radius */
    private float h;
    private float hInv;
    private float h2;
    private float h3;

    /* Pressure constant */
    public float K;
    /* Reference pressure */
    public float p0;
    /* Viscosity constant */
    public float e;

    /* Kernel constants */
    private float poly6;
    private float spiky;

    [Foldout("Rendering Related Parameters", true)]
    [Range(0.00001f, 5.0f)]
    public float particleRadius;
    [Range(1, 1024)]
    public int referenceNeighbors;

    /* Box constants */
    private float boxHeight;

    private int particlesAlive = 0;

    // Threads are fixed to 256
    private const int THREADS = 256;
    // Amount of thread groups depends on particleNumber
    private int threadGroups;

    private int spawnSpeed = 30;

    struct FluidParticle
    {
        public Vector3 pos;
        public Vector3 v;
        public Vector3 posLF;
        public Vector3 vLF;
        public int alive;
    }
    private GameObject boxGround;
    private Material particleMaterial;

    /* Computer Shader for GPU */
    private ComputeShader initShader;
    private ComputeShader partitionShader;
    private ComputeShader sortShader;
    private ComputeShader offsetShader;
    private ComputeShader SPHDensity;
    private ComputeShader SPHForce;
    private ComputeShader SPHIntegration;
    // private ComputeShader leapfrogStep;

    /* Kernel IDs */
    private int initializeKi;
    private int partitionKi;
    private int offsetKi;
    private int densityKi1;
    private int densityKi2;
    private int forceKi1;
    private int forceKi2;
    private int integrationKiEULER;
    private int integrationKiLF1;
    private int integrationKiLF2;
    //private int leapfrogKi;

    /* Arrays for Debugging g */
    private FluidParticle[] particlesArray;
    private int[] particlesIndexArray;
    private float[] cellIndexArray;
    private float[] sortedCellIndexArray;
    private int[] offsetArray;
    private float[] densityArray;
    private Vector3[] forceArray;
    private Vector3[] debugForce1;
    private Vector3[] debugForce2;

    /* GPU Buffer */
    private ComputeBuffer particlesBuffer;
    private ComputeBuffer particlesIndexBuffer;
    private ComputeBuffer cellIndexBuffer;
    private ComputeBuffer sortedCellIndexBuffer;
    private ComputeBuffer offsetBuffer;
    private ComputeBuffer densityBuffer;
    private ComputeBuffer forceBuffer;

    private ComputeBuffer debugBuffer1;
    private ComputeBuffer debugBuffer2;

    // Particle bounds for Unity's frustum culling
    private Bounds particleBound;
    // GPU bitonic sort for neigherst neighboors problem
    private MergeSort.BitonicMergeSort bitonicSort;

    private int particleNumber;

    /* Initialize all data structures required for SPH simulation on GPU */
    void OnEnable()
    {
        boxHeight = boxDepth;

        particleNumber = spwWidth * spwHeight * spwDepth;
        float groups = (float)particleNumber / THREADS;
        threadGroups = Mathf.Max(1, Mathf.CeilToInt(groups));

        particleBound = new Bounds(Vector3.zero, Vector3.one);
        boxGround = GameObject.Find("Box/Ground");
        boxGround.transform.localScale = new Vector3(boxWidth, boxThickness, boxDepth);
           
        particleMaterial = Resources.Load<Material>("Materials/Sphere Surface");

        InitializeConstants();
        InitializeArrays();
        InitializeCellIndex();
        
        // spawns all particles in a cube before first update
        if (spawnAsCube)
        {
            particlesAlive = particleNumber;
            InitializeParticlesCube();
        }

        InitializeBuffers();
        LoadShader();
        InitializeShader();
    }

    /* Release GPU memory after terminating simulation */
    void OnDisable()
    {
        particlesBuffer.Release();
        particlesBuffer.Release();
        particlesIndexBuffer.Release();
        cellIndexBuffer.Release();
        sortedCellIndexBuffer.Release();
        offsetBuffer.Release();
        densityBuffer.Release();
        forceBuffer.Release();
        debugBuffer1.Release();
        debugBuffer2.Release();
    }


    /* Arrange the particles in a cube form */
    void InitializeParticlesCube()
    {
        int length = (int)Mathf.Pow(particleNumber, 1f / 3f);
        float rad100 = particleRadius * 1.0f;
        float spawnWidth = (spwWidth - 1) * (particleRadius * 2 + spawnDistance); // add particleRadius * 2 for whole length
        float spawnDepth = (spwDepth - 1) * (particleRadius * 2 + spawnDistance); // add particleRadius * 2 for whole length
        float spawnHeight = (spwHeight - 1) * (particleRadius * 2 + spawnDistance); // add particleRadius * 2 for whole height

        for (int i = 0; i < particleNumber; ++i)
        {

            float x_pos = (i % spwWidth) * (2 * particleRadius + spawnDistance) - spawnWidth / 2 + Random.Range(-rad100, rad100);
            float z_pos = ((i / spwWidth) % spwDepth) * (2 * particleRadius + spawnDistance) - spawnDepth / 2 + Random.Range(-rad100, rad100);
            float y_pos = (i / (spwDepth * spwWidth)) * (2 * particleRadius + spawnDistance) + boxHeight / 2 - spawnHeight / 2 + Random.Range(-rad100, rad100);

            particlesArray[i].pos = new Vector3(x_pos, y_pos, z_pos);
            particlesArray[i].v = new Vector3(0f, 0f, 0f);
            particlesArray[i].posLF = new Vector3(x_pos, y_pos, z_pos);
            particlesArray[i].vLF = new Vector3(0f, 0f, 0f);
            particlesArray[i].alive = 1;
            particlesIndexArray[i] = i;
        }
    }

    /* Initialize cell Index with max float value */
    void InitializeCellIndex()
    {
        for (int i = 0; i < particleNumber; ++i)
        {
            cellIndexArray[i] = 340282300000000000000000000000000000000f;
        }
    }

    /* Initialize constants used on GPU */
    void InitializeConstants()
    {
        mass = (boxWidth * boxDepth * boxHeight)*p0/particleNumber;
        float h_a = ((boxWidth + boxDepth+boxHeight) * 2);
        float h_b = (spwWidth + spwDepth+spwHeight);
        h = h_a/h_b;
        h = h_a/h_b;



        Debug.Log("particle number: "+particleNumber);
        Debug.Log("mass: " +mass);
        Debug.Log("h: " +h);
        hInv = 1 / h;
        h2 = h * h;
        h3 = h * h * h;

        poly6 = 315 / (64 * Mathf.PI * Mathf.Pow(h, 9));
        spiky = -45 / (Mathf.PI * Mathf.Pow(h, 6));
    }

    /* Initialize Arrays for Debugging */
    void InitializeArrays()
    {
        particlesArray = new FluidParticle[particleNumber];
        particlesIndexArray = new int[particleNumber];
        cellIndexArray = new float[particleNumber];
        offsetArray = new int[particleNumber];
        densityArray = new float[particleNumber];
        forceArray = new Vector3[particleNumber];


        debugForce1 = new Vector3[particleNumber];
        debugForce2 = new Vector3[particleNumber];

        sortedCellIndexArray = new float[particleNumber];
    }

    /* Assign arrays on CPU side to GPU compute buffers */
    void InitializeBuffers()
    {
        particlesBuffer = new ComputeBuffer(particlesArray.Length, 12 * sizeof(float) + 4);
        particlesBuffer.SetData(particlesArray);

        particlesIndexBuffer = new ComputeBuffer(particlesIndexArray.Length, sizeof(int));
        particlesIndexBuffer.SetData(particlesIndexArray);

        cellIndexBuffer = new ComputeBuffer(particleNumber, sizeof(float));
        cellIndexBuffer.SetData(cellIndexArray);

        offsetBuffer = new ComputeBuffer(particleNumber, sizeof(int));
        offsetBuffer.SetData(offsetArray);

        densityBuffer = new ComputeBuffer(particleNumber, sizeof(float));
        densityBuffer.SetData(densityArray);

        forceBuffer = new ComputeBuffer(particleNumber, 3 * sizeof(float));
        forceBuffer.SetData(forceArray);

        debugBuffer1 = new ComputeBuffer(particleNumber, 3 * sizeof(float));
        debugBuffer1.SetData(debugForce1);

        debugBuffer2 = new ComputeBuffer(particleNumber, 3 * sizeof(float));
        debugBuffer2.SetData(debugForce2);


        sortedCellIndexBuffer = new ComputeBuffer(particleNumber, sizeof(float));
        sortedCellIndexBuffer.SetData(sortedCellIndexArray);
    }

    /* Load shader from Assets/Resources folder */
    void LoadShader()
    {
        initShader = Resources.Load<ComputeShader>("Shader/SPHInitialize");
        partitionShader = Resources.Load<ComputeShader>("Shader/SPHPartition");
        sortShader = Resources.Load<ComputeShader>("Shader/BitonicMergeSort");
        offsetShader = Resources.Load<ComputeShader>("Shader/SPHOffset");
        SPHDensity = Resources.Load<ComputeShader>("Shader/SPHDensity");
        SPHForce = Resources.Load<ComputeShader>("Shader/SPHForce");
        SPHIntegration = Resources.Load<ComputeShader>("Shader/SPHIntegration");
        //leapfrogStep = Resources.Load<ComputeShader>("Shader/LeapfrogStep");
    }

    /* Assign compute buffers to shaders */
    void InitializeShader()
    {
        initializeKi = initShader.FindKernel("initialize");
        initShader.SetBuffer(initializeKi, "particlesIndexBuffer", particlesIndexBuffer);
        initShader.SetBuffer(initializeKi, "offsetBuffer", offsetBuffer);

        partitionKi = partitionShader.FindKernel("calcCellIndices");
        partitionShader.SetBuffer(partitionKi, "particlesBuffer", particlesBuffer);
        partitionShader.SetBuffer(partitionKi, "particlesIndexBuffer", particlesIndexBuffer);
        partitionShader.SetBuffer(partitionKi, "cellIndexBuffer", cellIndexBuffer);
        partitionShader.SetBuffer(partitionKi, "sortedCellIndexBuffer", sortedCellIndexBuffer);
        partitionShader.SetBuffer(partitionKi, "offsetBuffer", offsetBuffer);
        partitionShader.SetInt("particleCount", particleNumber);
        partitionShader.SetFloat("h_inv", hInv);

        offsetKi = offsetShader.FindKernel("calcOffset");
        offsetShader.SetBuffer(offsetKi, "particlesIndexBuffer", particlesIndexBuffer);
        offsetShader.SetBuffer(offsetKi, "cellIndexBuffer", cellIndexBuffer);
        offsetShader.SetBuffer(offsetKi, "offsetBuffer", offsetBuffer);
        offsetShader.SetBuffer(offsetKi, "particlesBuffer", particlesBuffer);

        densityKi1 = SPHDensity.FindKernel("calcDensity1");
        SPHDensity.SetBuffer(densityKi1, "particlesBuffer", particlesBuffer);
        SPHDensity.SetBuffer(densityKi1, "particlesIndexBuffer", particlesIndexBuffer);
        SPHDensity.SetBuffer(densityKi1, "cellIndexBuffer", sortedCellIndexBuffer);
        SPHDensity.SetBuffer(densityKi1, "offsetBuffer", offsetBuffer);
        SPHDensity.SetBuffer(densityKi1, "densityBuffer", densityBuffer);

        densityKi2 = SPHDensity.FindKernel("calcDensity2");
        SPHDensity.SetBuffer(densityKi2, "particlesBuffer", particlesBuffer);
        SPHDensity.SetBuffer(densityKi2, "particlesIndexBuffer", particlesIndexBuffer);
        SPHDensity.SetBuffer(densityKi2, "cellIndexBuffer", sortedCellIndexBuffer);
        SPHDensity.SetBuffer(densityKi2, "offsetBuffer", offsetBuffer);
        SPHDensity.SetBuffer(densityKi2, "densityBuffer", densityBuffer);

        SPHDensity.SetInt("particleCount", particleNumber);
        SPHDensity.SetFloat("h", h);
        SPHDensity.SetFloat("h_inv", hInv);
        SPHDensity.SetFloat("h2", h2);
        SPHDensity.SetFloat("h3", h3);
        SPHDensity.SetFloat("mass", mass);
        SPHDensity.SetFloat("K", K);
        SPHDensity.SetFloat("p0", p0);
        SPHDensity.SetFloat("e", e);
        SPHDensity.SetFloat("poly6", poly6);
        SPHDensity.SetFloat("spiky", spiky);
        SPHDensity.SetFloat("gamma", 7.0f);

        forceKi1 = SPHForce.FindKernel("calcForce1");
        SPHForce.SetBuffer(forceKi1, "particlesBuffer", particlesBuffer);
        SPHForce.SetBuffer(forceKi1, "particlesIndexBuffer", particlesIndexBuffer);
        SPHForce.SetBuffer(forceKi1, "cellIndexBuffer", sortedCellIndexBuffer);
        SPHForce.SetBuffer(forceKi1, "offsetBuffer", offsetBuffer);
        SPHForce.SetBuffer(forceKi1, "densityBuffer", densityBuffer);
        SPHForce.SetBuffer(forceKi1, "forceBuffer", forceBuffer);
        SPHForce.SetBuffer(forceKi1, "debugForce1", debugBuffer1);
        SPHForce.SetBuffer(forceKi1, "debugForce2", debugBuffer2);

        forceKi2 = SPHForce.FindKernel("calcForce2");
        SPHForce.SetBuffer(forceKi2, "particlesBuffer", particlesBuffer);
        SPHForce.SetBuffer(forceKi2, "particlesIndexBuffer", particlesIndexBuffer);
        SPHForce.SetBuffer(forceKi2, "cellIndexBuffer", sortedCellIndexBuffer);
        SPHForce.SetBuffer(forceKi2, "offsetBuffer", offsetBuffer);
        SPHForce.SetBuffer(forceKi2, "densityBuffer", densityBuffer);
        SPHForce.SetBuffer(forceKi2, "forceBuffer", forceBuffer);

        SPHForce.SetInt("particleCount", particleNumber);
        SPHForce.SetFloat("h", h);
        SPHForce.SetFloat("h_inv", hInv);
        SPHForce.SetFloat("h2", h2);
        SPHForce.SetFloat("h3", h3);
        SPHForce.SetFloat("mass", mass);
        SPHForce.SetFloat("K", K);
        SPHForce.SetFloat("p0", p0);
        SPHForce.SetFloat("e", e);
        SPHForce.SetFloat("poly6", poly6);
        SPHForce.SetFloat("spiky", spiky);
        SPHForce.SetVector("g", new Vector3(0, -9.81f, 0));


        integrationKiEULER = SPHIntegration.FindKernel("calcIntegrationEULER");
        SPHIntegration.SetBuffer(integrationKiEULER, "particlesBuffer", particlesBuffer);
        SPHIntegration.SetBuffer(integrationKiEULER, "particlesIndexBuffer", particlesIndexBuffer);
        SPHIntegration.SetBuffer(integrationKiEULER, "forceBuffer", forceBuffer);


        integrationKiLF1 = SPHIntegration.FindKernel("calcIntegrationLF1");
        SPHIntegration.SetBuffer(integrationKiLF1, "particlesBuffer", particlesBuffer);
        SPHIntegration.SetBuffer(integrationKiLF1, "particlesIndexBuffer", particlesIndexBuffer);
        SPHIntegration.SetBuffer(integrationKiLF1, "forceBuffer", forceBuffer);


        integrationKiLF2 = SPHIntegration.FindKernel("calcIntegrationLF2");
        SPHIntegration.SetBuffer(integrationKiLF2, "particlesBuffer", particlesBuffer);
        SPHIntegration.SetBuffer(integrationKiLF2, "particlesIndexBuffer", particlesIndexBuffer);
        SPHIntegration.SetBuffer(integrationKiLF2, "forceBuffer", forceBuffer);

        SPHIntegration.SetFloat("deltaTime", timeStep);
        SPHIntegration.SetFloat("damping", damping);

        Renderer boxRend = boxGround.gameObject.GetComponent<Renderer>();
        Vector3 radiusVector = new Vector3(particleRadius, particleRadius, particleRadius);
        Vector3 thicknessVector = new Vector3(boxThickness / 2, boxThickness, boxThickness / 2);
        SPHIntegration.SetVector("maxBoxBoundarys", boxRend.bounds.max - thicknessVector - radiusVector);
        SPHIntegration.SetVector("minBoxBoundarys", boxRend.bounds.min + thicknessVector + radiusVector);

        /* Set Values in surface shader required for rendering */

        // calculate reference density that would receive most saturated color
        float refDensity = referenceNeighbors * mass * poly6 * Mathf.Pow(h2, 3);
        Debug.Log("reference Density: " + refDensity);

        particleMaterial.SetBuffer("particlesBuffer", particlesBuffer);
        particleMaterial.SetBuffer("densityBuffer", densityBuffer);
        particleMaterial.SetFloat("particleRadius", particleRadius);
        particleMaterial.SetFloat("refDensity", (float)refDensity);

        /* Initialize bitonic sort class */
        bitonicSort = new MergeSort.BitonicMergeSort(sortShader);
    }

    void InstantiateParticlesFromSpot(int particleId)
    {
        if (particlesArray[particleId].alive == 0)
        {   
            float randX = Random.Range(-particleRadius, particleRadius);
            float randZ = Random.Range(-particleRadius, particleRadius);
            float randY = Random.Range(-particleRadius, particleRadius);
            particlesArray[particleId].pos = new Vector3(randX, boxHeight * 0.9f + randY, randZ);
            particlesArray[particleId].v = new Vector3(0f, 0f, 0f);
            particlesArray[particleId].posLF = new Vector3(randX, boxHeight * 0.9f + randY, randZ);
            particlesArray[particleId].vLF = new Vector3(0f, 0f, 0f);
            particlesArray[particleId].alive = 1;
            particlesIndexArray[particleId] = particleId;
            particlesAlive += 1;
        }
    }


    /* Update is executed on every frame and invokes particle update*/
    void Update()
    {
        if (!spawnAsCube && particlesAlive < particleNumber)
        {
            // TODO
            particlesBuffer.GetData(particlesArray);
            InstantiateParticlesFromSpot(particlesAlive);
            particlesBuffer.SetData(particlesArray);
        }

        if (!debugMode)
        {
            /* Executing one Timestep per frame */
            ExecuteTimeStep();
        }

        // densityBuffer.GetData(densityArray);
        // PrintArray("densityArray\t", densityArray);

        // forceBuffer.GetData(forceArray);
        // PrintArray("forceArray\t", forceArray);

        /* The following is for debugging */
        if (Input.GetKeyDown("0"))
        {
            Debug.Log("Executing one time step ...");
            ExecuteTimeStep();
        }
        else if (Input.GetKeyDown("1"))
        {
            Debug.Log("Executing Initialization Shader ...");
            initShader.Dispatch(initializeKi, threadGroups, 1, 1);
            particlesIndexBuffer.GetData(particlesIndexArray);
            offsetBuffer.GetData(offsetArray);
            /*
            PrintArray("particlesIndexBuffer\t", particlesIndexArray);
            PrintArray("offsetArray\t", offsetArray);*/
        }
        else if (Input.GetKeyDown("2"))
        {
            Debug.Log("Executing Partition Shader ...");
            partitionShader.Dispatch(partitionKi, threadGroups, 1, 1);

            particlesBuffer.GetData(particlesArray);
            particlesIndexBuffer.GetData(particlesIndexArray);
            cellIndexBuffer.GetData(cellIndexArray);
            sortedCellIndexBuffer.GetData(sortedCellIndexArray);
            offsetBuffer.GetData(offsetArray);
            /*
            PrintArray("particlesArray\t", particlesIndexArray);
            PrintArray("cellIndexArray\t", cellIndexArray);
            PrintArray("sortedCellIndexArray", sortedCellIndexArray);
            PrintArray("offsetArray\t", offsetArray);*/
        }
        else if (Input.GetKeyDown("3"))
        {
            Debug.Log("Executing Sort Shader ...");
            bitonicSort.Sort(particlesIndexBuffer, cellIndexBuffer, sortedCellIndexBuffer);
            particlesIndexBuffer.GetData(particlesIndexArray);
            cellIndexBuffer.GetData(cellIndexArray);
            sortedCellIndexBuffer.GetData(sortedCellIndexArray);
            /*
            PrintArray("particlesArray\t", particlesIndexArray);
            PrintArray("cellIndexArray\t", cellIndexArray);
            PrintArray("sortedCellIndexArray", sortedCellIndexArray);*/
        }
        else if (Input.GetKeyDown("4"))
        {
            Debug.Log("Executing Offset Shader ...");
            offsetShader.Dispatch(offsetKi, threadGroups, 1, 1);
            particlesIndexBuffer.GetData(particlesIndexArray);
            sortedCellIndexBuffer.GetData(sortedCellIndexArray);
            offsetBuffer.GetData(offsetArray);
            /*
            PrintArray("particelsIndexBuffer", particlesIndexArray);
            PrintArray("sortedCellIndexArray", sortedCellIndexArray);
            PrintArray("offsetArray\t", offsetArray);*/
        }
        else if (Input.GetKeyDown("5"))
        {
            Debug.Log("Executing Density Shader ...");

            SPHDensity.Dispatch(densityKi1, threadGroups, 1, 1);
            particlesBuffer.GetData(particlesArray);
            densityBuffer.GetData(densityArray);
            
            PrintParticlePos("Positions\t", particlesArray);
            PrintArray("densityArray\t", densityArray);
        }
        else if (Input.GetKeyDown("6"))
        {
            Debug.Log("Executing Force Shader ...");
            SPHForce.Dispatch(forceKi1, threadGroups, 1, 1);
            forceBuffer.GetData(forceArray);

            debugBuffer1.GetData(debugForce1);

            debugBuffer2.GetData(debugForce2);


            PrintArray("forceArray\t", forceArray);
            PrintArray("f_press\t", debugForce1);
            PrintArray("f_visc\t", debugForce2);
        }
        else if (Input.GetKeyDown("7"))
        {
            Debug.Log("Executing Integration Shader (Forward Euler!) ...");
            SPHIntegration.Dispatch(integrationKiEULER, threadGroups, 1, 1);
        }

        /* Draw Meshes on GPU */
        Graphics.DrawMeshInstancedProcedural(particleMesh, 0, particleMaterial, particleBound, particlesAlive);
    }


    /* Assign arrays on CPU side to GPU compute buffers */
    void ExecuteTimeStep()
    {
        initShader.Dispatch(initializeKi, threadGroups, 1, 1);
        partitionShader.Dispatch(partitionKi, threadGroups, 1, 1);
        bitonicSort.Sort(particlesIndexBuffer, cellIndexBuffer, sortedCellIndexBuffer);
        offsetShader.Dispatch(offsetKi, threadGroups, 1, 1);

        switch ((int)integrationMethod)
        {
            case 0: /* Leapfrog */
                //Debug.Log("Leapfrog");
                SPHDensity.Dispatch(densityKi1, threadGroups, 1, 1);
                SPHForce.Dispatch(forceKi1, threadGroups, 1, 1);
                SPHIntegration.Dispatch(integrationKiLF1, threadGroups, 1, 1);

                SPHDensity.Dispatch(densityKi2, threadGroups, 1, 1);
                SPHForce.Dispatch(forceKi2, threadGroups, 1, 1);
                SPHIntegration.Dispatch(integrationKiLF2, threadGroups, 1, 1);
                break;
            case 1: /* Forward Euler */
                //Debug.Log("Forward Euler");
                SPHDensity.Dispatch(densityKi1, threadGroups, 1, 1);
                SPHForce.Dispatch(forceKi1, threadGroups, 1, 1);
                SPHIntegration.Dispatch(integrationKiEULER, threadGroups, 1, 1);
                break;
            case 3: /* Another integration method ...*/
                break;
        }
    }


    /* Function to display arrays in a convenient format */
    void PrintArray<T>(string name, T[] array)
    {
        string str = "";
        for (int i = 0; i < array.Length; ++i)
        {
            str += array[i].ToString() + "\t";
        }
        Debug.Log(name + ":\t\t" + str);
    }


    void PrintParticlePos(string name, FluidParticle[] array)
    {
        string str = "";
        for (int i = 0; i < array.Length; ++i)
        {
            str += array[i].pos + "\t";
        }
        Debug.Log(name + ":\t\t" + str);
    }


    void PrintDebug(string name, Vector3[] array)
    {
        string str = "";
        for (int i = 0; i < array.Length; ++i)
        {
            str += array[i] + "\t";
        }
        Debug.Log(name + ":\t\t" + str);
    }
}