//--------------------------------------------------------------------------------------
// Imports
//--------------------------------------------------------------------------------------

using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;

//--------------------------------------------------------------------------------------
// Classes
//--------------------------------------------------------------------------------------
   
static public class GpuSort
{
    // ---- Constants ----

    private const uint BITONIC_BLOCK_SIZE = 64;
    private const uint TRANSPOSE_BLOCK_SIZE = 1;

    // ---- Members ----

    static private ComputeShader sort32;
    static private ComputeShader sort64;
    static private int kSort32;
    static private int kSort64;
    static private int kTranspose32;
    static private int kTranspose64;
    static private bool init;

    // ---- Structures ----


    // ---- Methods ----

    static private void Init(ComputeShader shader)
    {
        // Acquire compute shaders.
        sort64 = shader;

        // If they were not found, crash!
        if (sort64 == null) Debug.LogError("GpuSort64 not found.");

        // Find kernels
        kSort64 = sort64.FindKernel("BitonicSort");
        kTranspose64 = sort64.FindKernel("MatrixTranspose");

        // Done
        init = true;
    }


    static public void BitonicSort64(ComputeBuffer inBuffer, ComputeBuffer tmpBuffer, ComputeShader shader)
    {
        if (!init) Init(shader);
        BitonicSortGeneric(sort64, kSort64, kTranspose64, inBuffer, tmpBuffer);
    }
    

    static private void BitonicSortGeneric(ComputeShader shader, int kSort, int kTranspose, ComputeBuffer inBuffer, ComputeBuffer tmpBuffer)
    {
        // Determine if valid.
        if ((inBuffer.count % BITONIC_BLOCK_SIZE) != 0)
            Debug.LogError("Input array size should be multiple of the Bitonic block size!");

        // Determine parameters.
        uint NUM_ELEMENTS = (uint) inBuffer.count;
        uint MATRIX_WIDTH = BITONIC_BLOCK_SIZE;
        uint MATRIX_HEIGHT = NUM_ELEMENTS / BITONIC_BLOCK_SIZE;

        // Sort the data
        // First sort the rows for the levels <= to the block size
        for (uint level = 2; level <= BITONIC_BLOCK_SIZE; level <<= 1)
        {
            SetConstants(shader, level, level, MATRIX_HEIGHT, MATRIX_WIDTH);
            
            // Sort the row data
            shader.SetBuffer(kSort, "Data", inBuffer);
            shader.Dispatch(kSort, (int) (NUM_ELEMENTS / BITONIC_BLOCK_SIZE), 1, 1);
        }

        // Then sort the rows and columns for the levels > than the block size
        // Transpose. Sort the Columns. Transpose. Sort the Rows.
        for (uint level = (BITONIC_BLOCK_SIZE << 1); level <= NUM_ELEMENTS; level <<= 1)
        {
            // Transpose the data from buffer 1 into buffer 2
            SetConstants(shader, (level / BITONIC_BLOCK_SIZE), (level & ~NUM_ELEMENTS) / BITONIC_BLOCK_SIZE, MATRIX_WIDTH, MATRIX_HEIGHT);
            shader.SetBuffer(kTranspose, "Input", inBuffer);
            shader.SetBuffer(kTranspose, "Data", tmpBuffer);
            // Debug.Log("\t Matrix Height: " + MATRIX_HEIGHT + "\t Transpose block size: " + TRANSPOSE_BLOCK_SIZE);
            shader.Dispatch(kTranspose, (int) (MATRIX_WIDTH / TRANSPOSE_BLOCK_SIZE), (int) (MATRIX_HEIGHT / TRANSPOSE_BLOCK_SIZE), 1);
            shader.SetBuffer(kSort, "Data", tmpBuffer);
            shader.Dispatch(kSort, (int) (NUM_ELEMENTS / BITONIC_BLOCK_SIZE), 1, 1);

            // Transpose the data from buffer 2 back into buffer 1
            SetConstants(shader, BITONIC_BLOCK_SIZE, level, MATRIX_HEIGHT, MATRIX_WIDTH);
            shader.SetBuffer(kTranspose, "Input", tmpBuffer);
            shader.SetBuffer(kTranspose, "Data", inBuffer);

            shader.Dispatch(kTranspose, (int) (MATRIX_HEIGHT / TRANSPOSE_BLOCK_SIZE), (int) (MATRIX_WIDTH / TRANSPOSE_BLOCK_SIZE), 1);
            shader.SetBuffer(kSort, "Data", inBuffer);
            shader.Dispatch(kSort, (int) (NUM_ELEMENTS / BITONIC_BLOCK_SIZE), 1, 1);
        }
    }

    static private void SetConstants(ComputeShader shader, uint iLevel, uint iLevelMask, uint iWidth, uint iHeight)
    {
        shader.SetInt("g_iLevel", (int) iLevel);
        shader.SetInt("g_iLevelMask", (int) iLevelMask);
        shader.SetInt("g_iWidth", (int) iWidth);
        shader.SetInt("g_iHeight", (int) iHeight);
    }
}
