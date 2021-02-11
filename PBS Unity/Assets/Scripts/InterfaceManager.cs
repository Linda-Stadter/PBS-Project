using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;  

public class InterfaceManager : MonoBehaviour
{
    public GameObject GPUSimulation;
    public GameObject Pipe;
    public GameObject Box;
    public GameObject Interface;

    // Start is called before the first frame update
    void Start()
    {   
        DontDestroyOnLoad(this);
        DontDestroyOnLoad(Box);
        DontDestroyOnLoad(Pipe);
        DontDestroyOnLoad(Interface);

    }


    public void LoadFluidScene()
    {
        SceneManager.LoadScene("FluidScene");
        Box.SetActive(true);
        Pipe.SetActive(true);
        Interface.SetActive(true);
        GPUSimulation.GetComponent<GPURendering>().EnableSimulation();
        GPUSimulation.SetActive(true);
    }
}
