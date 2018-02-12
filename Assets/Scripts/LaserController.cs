using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class LaserController : MonoBehaviour {

  public GameObject[] laserReceivers;
  public GameObject linacAnchor;
  public GameObject bedAnchor;
  HashSet<Material> mats = new HashSet<Material>();
  int testCounter = 0;
  int testMax = 100;
  bool on = false;
  Vector3 bed;
  Vector3 linac;
  float radius = 0.005f;
  
  
	// Use this for initialization
	void Start () {
		foreach (GameObject g in laserReceivers) {
      if (g) {
        mats.Add(g.GetComponent<Renderer>().material);
      }
    }
    
    bed = bedAnchor.transform.position;
    linac = linacAnchor.transform.position;
    AlignLasers();
	}
  
  public void ChangeLasers(float v) {    
    foreach (Material m in mats) {        
      m.SetInt("_ON",((int)Math.Round(v)));
    }
  }
  
  void AlignLasers() {
    // print("X: " + bed.x + ", Y: " + linac.y);    
    float z = (linac.z + bed.z) * 0.5f;
    foreach (Material m in mats) {
      m.SetFloat("_xMin",bed.x-radius);
      m.SetFloat("_xMax",bed.x+radius);
      m.SetFloat("_yMin",linac.y-radius);
      m.SetFloat("_yMax",linac.y+radius);
      m.SetFloat("_zMin",z-radius);
      m.SetFloat("_zMax",z+radius);
    }
  }
  
  
  
  
	
	// Update is called once per frame
	void Update () {
		bool l = Input.GetKeyUp("l");
    bool t = Input.GetKeyUp("t");
    
    if (l) {      
      on = !on;
      foreach (Material m in mats) {        
        m.SetInt("_ON",(on == true ? 1 : 0));
      }
    }
    
    if (t && testCounter == -1) {
      testCounter = 0;
      foreach (Material m in mats) {
        m.SetInt("_OK",1);
      }
    }
    
    if (testCounter == testMax) {
      testCounter = -1;
      foreach (Material m in mats) {
        m.SetInt("_OK",0);
      }
      
    }
    
    if (testCounter >= 0) {
       testCounter++;
    }
    
	}
}
