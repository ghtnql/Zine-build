using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.UI;

public class ColorCtrl : MonoBehaviour
{
    [SerializeField]
    GameObject PanelColor;

    [SerializeField]
    Slider redSlider;
    [SerializeField]
    Slider greenSlider;
    [SerializeField]
    Slider blueSlider;

    Material HeadMaterial;
    Material bodyMaterial;
    float red;
    float green;
    float blue;

    Color nowColor;
    Color lateColor;


    void Start()
    {
        HeadMaterial = this.GetComponent<Renderer>().material;
        bodyMaterial = (Resources.Load("Body") as GameObject).GetComponent<Renderer>().sharedMaterial;
        redSlider.value = HeadMaterial.color.r;
        greenSlider.value = HeadMaterial.color.g;
        blueSlider.value = HeadMaterial.color.b;
        nowColor = HeadMaterial.color;

        //StartCoroutine(ColorChange());
    }

    void Update()
    {
        red = redSlider.value;
        green = greenSlider.value;
        blue = blueSlider.value;
        nowColor = new Color(red, green, blue);
        if (nowColor == lateColor) return;
        nowColor = bodyMaterial.color = HeadMaterial.color = nowColor;
    }

    void LateUpdate()
    {
        if(lateColor != nowColor)
        lateColor = nowColor;
    }

    //IEnumerator ColorChange()
    //{
    //    while (PanelColor.activeSelf)
    //    {
    //        red = redSlider.value;
    //        green = greenSlider.value;
    //        blue = blueSlider.value;
    //        bodyMaterial.color = HeadMaterial.color = new Color(red, green, blue);
    //        yield return null;
    //    }
    //}
}
