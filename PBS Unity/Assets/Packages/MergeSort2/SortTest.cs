using UnityEngine;
using System.Collections;

public class SortTest : MonoBehaviour
{
    private static ComputeBuffer inBuffer, tempBuffer;

    public static void Test(ComputeShader shader)
    {
        uint[] inArray = new uint[128];

        for (int i = 0; i < inArray.Length; i++)
            inArray[i] = (uint) (inArray.Length - 1 - i);

        Debug.Log("Unsorted");
        string str = "";
        for (int i = 0; i < inArray.Length; ++i) {
            str += inArray[i].ToString() + "\t";
        }
        Debug.Log(str);
        
        inBuffer = new ComputeBuffer(inArray.Length, 4);
        tempBuffer = new ComputeBuffer(inArray.Length, 4);

        inBuffer.SetData(inArray);
        GpuSort.BitonicSort64(inBuffer, tempBuffer, shader);
        inBuffer.GetData(inArray);

        Debug.Log("Sorted" + inArray);
        string str2 = "";
        for (int i = 0; i < inArray.Length; ++i) {
            str2 += inArray[i].ToString() + "\t";
        }
        Debug.Log(str2);
    }

    void Print(string name, uint[] array)
    {
        string values = "";
        string problems = "";

        for (int i = 0; i < array.Length; i++)
        {
            if ((i != 0) && (array[i - 1] > array[i]))
                problems += "Discontinuity found at " + i + "!! \n";

            values += array[i] + " ";
        }

        Debug.Log(name + " :  " + values + "\n" + problems);
    }

    void OnDisable()
    {
        if (inBuffer != null)
            inBuffer.Dispose();
        if (tempBuffer != null)
            tempBuffer.Dispose();

        inBuffer = null;
        tempBuffer = null;
    }
}
