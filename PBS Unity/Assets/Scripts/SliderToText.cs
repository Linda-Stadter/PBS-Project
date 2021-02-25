using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class SliderToText : MonoBehaviour
{
    public Slider sliderUI;
    private Text textSliderValue;

    void Start()
    {
        textSliderValue = GetComponent<Text>();
        ShowSliderValue();
    }

    public void ShowSliderValue()
    {
        string sliderMessage = System.Math.Pow(System.Math.Pow(2, sliderUI.value), 3).ToString();
        textSliderValue.text = sliderMessage;
    }

}
