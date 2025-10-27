using UnityEngine;

public class InsertButton : MonoBehaviour
{
    public GameObject Inventory;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void InsertClicked()
    {
        //Debug.Log("Insert ´­·È´ç");
        Inventory.SetActive(true);
    }

    private void OnEnable()
    {
        Inventory.SetActive(false);
    }
}
