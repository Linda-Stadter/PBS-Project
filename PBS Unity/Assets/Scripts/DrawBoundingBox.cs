using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrawBoundingBox : MonoBehaviour
{
    public GameObject ground;

    private GameObject wall;
    private Renderer groundRend;

    void Start()
    {
        drawWalls();
    }

    // draws four walls attached to the ground cube
    public void drawWalls()
    {
        groundRend = ground.gameObject.GetComponent<Renderer>();

        float oldX = ground.transform.position.x;
        float oldZ = ground.transform.position.z;
        float oldY = ground.transform.position.y;

        float newX = (groundRend.bounds.max.x - groundRend.bounds.min.x) / 2;
        float newZ = (groundRend.bounds.max.z - groundRend.bounds.min.z) / 2;

        // the height is currently determined by the length in z direction. Not optimal for rectangular shapes.
        float height = groundRend.bounds.max.z - groundRend.bounds.min.z;

        // create walls on z-axis by copying ground and rotating on x-axis
        wall = GameObject.Instantiate(ground, new Vector3(oldX, oldY + newZ, oldZ + newZ), Quaternion.Euler(-90, 0, 0));
        wall.transform.parent = gameObject.transform;

        wall = GameObject.Instantiate(ground, new Vector3(oldX, oldY + newZ, oldZ - newZ), Quaternion.Euler(-90, 0, 0));
        wall.transform.parent = gameObject.transform;

        // create walls on x-axis by copying ground, rotating on z-axis and scaling on x-axis
        // change position respectively
        // length on z-axis will be used as height
        Vector3 scaling = new Vector3(ground.transform.localScale.z, ground.transform.localScale.y, ground.transform.localScale.z);
        float offsetY = groundRend.bounds.max.z - oldZ;

        wall = GameObject.Instantiate(ground, new Vector3(oldX + newX, oldY + offsetY, oldZ), Quaternion.Euler(0, 0, 90));
        wall.transform.parent = gameObject.transform;
        wall.transform.localScale = scaling;

        wall = GameObject.Instantiate(ground, new Vector3(oldX - newX, oldY + offsetY, oldZ), Quaternion.Euler(0, 0, 90));
        wall.transform.parent = gameObject.transform;
        wall.transform.localScale = scaling;
        
        // create top
        GameObject top = GameObject.Instantiate(ground, new Vector3(oldX, oldY + height, oldZ), Quaternion.identity);
        top.transform.parent = gameObject.transform;

    }

}
