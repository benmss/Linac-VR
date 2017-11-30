using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaserController : MonoBehaviour {

  public GameObject[] laserReceivers;
  HashSet<Material> mats = new HashSet<Material>();
  int testCounter = 0;
  int testMax = 100;
  bool on = false;
  
  
	// Use this for initialization
	void Start () {
		foreach (GameObject g in laserReceivers) {
      if (g) {
        mats.Add(g.GetComponent<Renderer>().material);
      }
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
