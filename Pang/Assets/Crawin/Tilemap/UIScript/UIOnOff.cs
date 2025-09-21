using UnityEditor.Animations;
using UnityEngine;

public class UIOnOff : MonoBehaviour
{
    bool activate;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        activate = false;
        foreach (Transform child in transform)
        {
            child.gameObject.SetActive(activate);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            activate = !activate;
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(activate);
            }
        }
    }
}
