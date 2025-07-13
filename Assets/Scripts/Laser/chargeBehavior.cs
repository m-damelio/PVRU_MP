using System.Collections;
using UnityEngine;

public class chargeBehavior : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public Color colorStart;
    public Color colorEnd;
    public float duration;
    public float fadeStart;
    private bool isFading = false;


    void Start()
    {
        duration = 10.0f;
        fadeStart = 0.0f;
        colorStart = Color.white;
        colorEnd = new Color(0.7479801f, 0,1,1);
        this.gameObject.GetComponent<Renderer>().material.color = colorStart;

    }

    // Update is called once per frame
    void Update()
    {

    }


    public IEnumerator ColourFade()
    {
        if (isFading)
        {
            yield break;
        }
           
        isFading = true;
        fadeStart = 0.0f;
        while (fadeStart < 1.0f)
        {
            fadeStart += Time.deltaTime / duration;
            gameObject.GetComponent<Renderer>().material.color = Color.Lerp(colorStart, colorEnd, fadeStart);
            yield return null;
        }

        isFading = false;
    }

    public IEnumerator ColourDeFade()
    {
        fadeStart = 0.0f;
        while (fadeStart < 1.0f)
        {
            fadeStart += Time.deltaTime / duration;
            gameObject.GetComponent<Renderer>().material.color = Color.Lerp(colorEnd, colorStart, fadeStart);
            yield return null;
        }
    }

}
