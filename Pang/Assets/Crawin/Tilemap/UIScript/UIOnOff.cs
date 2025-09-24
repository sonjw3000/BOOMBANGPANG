using UnityEngine;

public class UIOnOff : MonoBehaviour
{
    private bool activate;
    public ref bool activateRef => ref activate;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        activate = false;
        foreach (Transform sibling in transform.parent)
        {
            if (sibling == transform) continue;
            sibling.gameObject.SetActive(activate);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            activate = !activate;
            foreach (Transform sibling in transform.parent)
            {
                if (sibling == transform) continue;
                sibling.gameObject.SetActive(activate);
            }
        }
    }
}
